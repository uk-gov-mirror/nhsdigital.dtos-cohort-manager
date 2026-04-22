namespace Common;

using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Model;

public class AuditLogClient : IAuditLogClient
{
    private const string AuditBlobContainer = "participant-audit";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IQueueClient _queueClient;
    private readonly IBlobStorageHelper _blobStorageHelper;
    private readonly AuditClientConfig _config;
    private readonly ILogger<AuditLogClient> _logger;

    public AuditLogClient(
        IQueueClient queueClient,
        IBlobStorageHelper blobStorageHelper,
        IOptions<AuditClientConfig> config,
        ILogger<AuditLogClient> logger)
    {
        _queueClient = queueClient;
        _blobStorageHelper = blobStorageHelper;
        _config = config.Value;
        _logger = logger;
    }

    public async Task AddAsync(ParticipantAuditMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);

        message.RawDataRef = await WriteSnapshotToBlobAsync(message);

        var sent = await _queueClient.AddAsync(message, _config.AuditTopicName);
        if (!sent)
        {
            _logger.LogError(
                "Failed to enqueue audit message | Source: {Source} | Correlation: {CorrelationId}",
                message.Source, message.CorrelationId);
            return;
        }

        _logger.LogInformation(
            "Audit enqueued | Source: {Source} | Correlation: {CorrelationId}",
            message.Source, message.CorrelationId);
    }

    public async Task<int> AddBatchAsync(IEnumerable<ParticipantAuditMessage> messages)
    {
        ArgumentNullException.ThrowIfNull(messages);

        var messageList = messages.ToList();
        foreach (var message in messageList)
        {
            message.RawDataRef = await WriteSnapshotToBlobAsync(message);
        }

        return await _queueClient.AddBatchAsync(messageList, _config.AuditTopicName);
    }

    private async Task<string?> WriteSnapshotToBlobAsync(ParticipantAuditMessage message)
    {
        if (message.RequestSnapshot is null)
        {
            return null;
        }

        try
        {
            var blobPath = $"{message.CreatedDatetime:dd-MM-yyyy}/{message.CorrelationId}.json";
            var payload = JsonSerializer.SerializeToUtf8Bytes(message.RequestSnapshot, JsonOptions);

            var blobFile = new BlobFile(payload, blobPath);
            var uri = await _blobStorageHelper.UploadFileToBlobStorageAndGetUri(
                _config.AzureWebJobsStorage, AuditBlobContainer, blobFile, overwrite: true);

            if (uri is null)
            {
                _logger.LogError(
                    "Blob write returned null URI for CorrelationId {CorrelationId}. " +
                    "Audit will be persisted without a blob reference.",
                    message.CorrelationId);
            }

            return uri;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to write audit snapshot to blob for CorrelationId {CorrelationId}. " +
                "Audit will be persisted without a blob reference.",
                message.CorrelationId);
            return null;
        }
    }
}

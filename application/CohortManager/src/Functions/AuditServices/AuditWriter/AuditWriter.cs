namespace NHS.CohortManager.AuditServices;

using System.Text.Json;
using Common;
using DataServices.Database;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Model;

public class AuditWriter
{
    private const string AuditBlobContainer = "participant-audit";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly DataServicesContext _dbContext;
    private readonly IBlobStorageHelper _blobStorageHelper;
    private readonly AuditConfig _config;
    private readonly ILogger<AuditWriter> _logger;

    public AuditWriter(
        DataServicesContext dbContext,
        IBlobStorageHelper blobStorageHelper,
        IOptions<AuditConfig> config,
        ILogger<AuditWriter> logger)
    {
        _dbContext = dbContext;
        _blobStorageHelper = blobStorageHelper;
        _config = config.Value;
        _logger = logger;
    }

    [Function(nameof(AuditWriter))]
    public async Task Run(
        [ServiceBusTrigger(topicName: "%AuditTopicName%", subscriptionName: "%AuditSubscription%", Connection = "ServiceBusConnectionString")] string messageText, FunctionContext context)
    {
        ParticipantAuditMessage? audit;
        try
        {
            audit = JsonSerializer.Deserialize<ParticipantAuditMessage>(messageText, JsonOptions);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to deserialise audit message.");
            return;
        }

        if (audit is null)
        {
            _logger.LogError("Audit message deserialised to null.");
            return;
        }

        var rawDataRef = await WriteSnapshotToBlobAsync(audit);

        var auditLog = new ParticipantAuditLog
        {
            CorrelationId = audit.CorrelationId,
            NhsNumber = audit.NhsNumber,
            BatchId = audit.BatchId,
            CreatedDatetime = audit.CreatedDatetime,
            RecordSource = (int)audit.Source,
            RecordSourceDesc = audit.RecordSourceDesc,
            CreatedBy = audit.CreatedBy,
            ScreeningId = audit.ScreeningId,
            RawDataRef = rawDataRef
        };

        try
        {
            _dbContext.participantAuditLogs.Add(auditLog);
            var rowsAffected = await _dbContext.SaveChangesAsync();

            if (rowsAffected <= 0)
            {
                _logger.LogError(
                    "SaveChangesAsync reported 0 rows affected for CorrelationId {CorrelationId}.",
                    audit.CorrelationId);
                return;
            }

            _logger.LogInformation(
                "Audit written | Source: {Source} | Correlation: {CorrelationId} | Rows: {Rows}",
                audit.Source, audit.CorrelationId, rowsAffected);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to persist audit log for CorrelationId {CorrelationId}.",
                audit.CorrelationId);
        }
    }

    private async Task<string?> WriteSnapshotToBlobAsync(ParticipantAuditMessage message)
    {
        if (message.RequestSnapshot is null)
        {
            return null;
        }

        var blobPath = $"{message.CreatedDatetime:dd-MM-yyyy}/{message.CorrelationId}.json";
        var payload = JsonSerializer.SerializeToUtf8Bytes(message.RequestSnapshot, JsonOptions);
        var blobFile = new BlobFile(payload, blobPath);

        try
        {
            var uri = await _blobStorageHelper.UploadFileToBlobStorageAndGetUri(
                _config.AzureWebJobsStorage,
                AuditBlobContainer,
                blobFile,
                overwrite: true);

            if (uri is null)
            {
                _logger.LogError(
                    "Blob write returned null URI for CorrelationId {CorrelationId}.",
                    message.CorrelationId);
                throw new InvalidOperationException(
                    $"Blob write returned null URI for CorrelationId {message.CorrelationId}.");
            }

            return uri;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to write audit snapshot to blob for CorrelationId {CorrelationId}.",
                message.CorrelationId);
            throw;
        }
    }
}

namespace Common;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Model;

public class AuditLogClient : IAuditLogClient
{
    private readonly IQueueClient _queueClient;
    private readonly AuditClientConfig _config;
    private readonly ILogger<AuditLogClient> _logger;

    public AuditLogClient(
        [FromKeyedServices("AuditWriter")] IQueueClient queueClient,
        IOptions<AuditClientConfig> config,
        ILogger<AuditLogClient> logger)
    {
        _queueClient = queueClient;
        _config = config.Value;
        _logger = logger;
    }

    public async Task AddAsync(ParticipantAuditMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);

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
        return await _queueClient.AddBatchAsync(messageList, _config.AuditTopicName);
    }
}

namespace NHS.CohortManager.AuditServices;

using System.Text.Json;
using DataServices.Database;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Model;

public class AuditWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly DataServicesContext _dbContext;
    private readonly ILogger<AuditWriter> _logger;

    public AuditWriter(DataServicesContext dbContext, ILogger<AuditWriter> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    [Function(nameof(AuditWriter))]
    public async Task Run(
        [ServiceBusTrigger(topicName: "%AuditTopicName%", subscriptionName: "%AuditSubscription%", Connection = "ServiceBusConnectionString_client_internal")] string messageText, FunctionContext context)
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
            RawDataRef = audit.RawDataRef
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
}

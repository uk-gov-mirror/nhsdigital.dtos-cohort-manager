namespace ReferenceDataUpdater;

using System.Text.Json;
using Azure.Messaging.ServiceBus;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Model;

public class ReferenceDataUpdaterFunction
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IReferenceDataInsertHandler _insertHandler;
    private readonly ILogger<ReferenceDataUpdaterFunction> _logger;

    public ReferenceDataUpdaterFunction(
        IReferenceDataInsertHandler insertHandler,
        ILogger<ReferenceDataUpdaterFunction> logger)
    {
        _insertHandler = insertHandler;
        _logger = logger;
    }

    [Function(nameof(ReferenceDataUpdaterFunction))]
    public async Task Run(
        [ServiceBusTrigger(
            topicName: "%ReferenceDataTopicName%",
            subscriptionName: "%ReferenceDataSubscription%",
            Connection = "ServiceBusConnectionString",
            AutoCompleteMessages = false)]
        ServiceBusReceivedMessage message,
        ServiceBusMessageActions messageActions)
    {
        ReferenceDataUpdateMessage? updateMessage;
        try
        {
            updateMessage = JsonSerializer.Deserialize<ReferenceDataUpdateMessage>(message.Body, JsonOptions);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to deserialise reference data update message.");
            await messageActions.DeadLetterMessageAsync(message);
            return;
        }

        if (updateMessage is null || string.IsNullOrWhiteSpace(updateMessage.DataType))
        {
            _logger.LogError("Reference data update message was null or missing DataType.");
            await messageActions.DeadLetterMessageAsync(message);
            return;
        }

        _logger.LogInformation(
            "Processing reference data update | Type: {DataType} | CorrelationId: {CorrelationId}",
            updateMessage.DataType, updateMessage.CorrelationId);

        try
        {
            var success = await _insertHandler.ProcessRecord(updateMessage.DataType, updateMessage.Data);

            if (!success)
            {
                _logger.LogError(
                    "Failed to process reference data update for type {DataType}.",
                    updateMessage.DataType);
                await messageActions.DeadLetterMessageAsync(message);
                return;
            }

            await messageActions.CompleteMessageAsync(message);
            _logger.LogInformation(
                "Reference data update completed | Type: {DataType} | CorrelationId: {CorrelationId}",
                updateMessage.DataType, updateMessage.CorrelationId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Unexpected error processing reference data update for type {DataType}. CorrelationId: {CorrelationId}",
                updateMessage.DataType, updateMessage.CorrelationId);
            await messageActions.DeadLetterMessageAsync(message);
        }
    }
}

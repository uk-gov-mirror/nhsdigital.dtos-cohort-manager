namespace Common;

using System.Collections.Concurrent;
using System.Text.Json;
using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Logging;

public class AzureServiceBusClient : IQueueClient
{
    private readonly ServiceBusClient _serviceBusClient;
    private readonly ILogger<AzureServiceBusClient> _logger;
    private readonly ConcurrentDictionary<string, ServiceBusSender> _senders = new();

    public AzureServiceBusClient(ILogger<AzureServiceBusClient> logger, ServiceBusClient serviceBusClient)
    {
        _logger = logger;
        _serviceBusClient = serviceBusClient;
    }

    /// <summary>
    /// will send a message to a queue/ topic
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="message"></param>
    /// <param name="queueName"></param>
    /// <returns></returns>
    public async Task<bool> AddAsync<T>(T message, string queueName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(queueName);

        var sender = _senders.GetOrAdd(queueName, _serviceBusClient.CreateSender);

        try
        {
            string jsonMessage = JsonSerializer.Serialize(message);
            ServiceBusMessage serviceBusMessage = new(jsonMessage);

            _logger.LogInformation("sending message to service bus queue or topic");

            await sender.SendMessageAsync(serviceBusMessage);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "There was an error sending message to service bus queue {QueueName} {ErrorMessage}", queueName, ex.Message);
            return false;
        }
    }

    public async Task<int> AddBatchAsync<T>(IEnumerable<T> messages, string queueName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(queueName);

        var pending = new Queue<ServiceBusMessage>(
            messages.Select(message => new ServiceBusMessage(JsonSerializer.Serialize(message))));

        if (pending.Count == 0) return 0;

        var sender = _senders.GetOrAdd(queueName, _serviceBusClient.CreateSender);
        var failCount = 0;

        while (pending.Count > 0)
        {
            try
            {
                failCount += await SendNextBatchAsync(sender, pending, queueName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending batch to Service Bus queue {QueueName}", queueName);
                failCount += pending.Count;
                break;
            }
        }

        return failCount;
    }

    private async Task<int> SendNextBatchAsync(
        ServiceBusSender sender,
        Queue<ServiceBusMessage> pending,
        string queueName)
    {
        using var batch = await sender.CreateMessageBatchAsync();

        while (pending.Count > 0 && batch.TryAddMessage(pending.Peek()))
        {
            pending.Dequeue();
        }

        if (batch.Count == 0)
        {
            // Head message can't fit an empty batch — oversized, drop it
            _logger.LogError(
                "Message exceeds max Service Bus batch size for queue {QueueName}. Skipping.",
                queueName);
            pending.Dequeue();
            return 1;
        }

        await sender.SendMessagesAsync(batch);
        return 0;
    }
}

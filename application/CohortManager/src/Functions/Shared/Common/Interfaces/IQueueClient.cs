namespace Common;

public interface IQueueClient
{
    Task<bool> AddAsync<T>(T message, string queueName);
    Task<int> AddBatchAsync<T>(IEnumerable<T> messages, string queueName);
}

namespace Common;

using Model;

public interface IAuditLogClient
{
    Task AddAsync(ParticipantAuditMessage message);
    Task<int> AddBatchAsync(IEnumerable<ParticipantAuditMessage> messages);
}

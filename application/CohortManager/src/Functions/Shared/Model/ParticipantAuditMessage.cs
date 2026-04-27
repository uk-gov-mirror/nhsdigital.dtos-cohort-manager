namespace Model;

using Model.Enums;

public class ParticipantAuditMessage
{
    public Guid CorrelationId { get; set; } = Guid.NewGuid();
    public Guid? BatchId { get; set; }
    public required string NhsNumber { get; set; }
    public required AuditSource Source { get; set; }
    public string? RecordSourceDesc { get; set; }
    public DateTime CreatedDatetime { get; set; } = DateTime.UtcNow;
    public required string CreatedBy { get; set; }
    public int? ScreeningId { get; set; }
    public string? RawDataRef { get; set; }
    public object? RequestSnapshot { get; set; }
}

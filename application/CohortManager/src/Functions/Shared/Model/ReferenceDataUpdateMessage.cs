namespace Model;

using System.Text.Json;

public class ReferenceDataUpdateMessage
{
    public required string DataType { get; set; }
    public required JsonElement Data { get; set; }
    public string? CorrelationId { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

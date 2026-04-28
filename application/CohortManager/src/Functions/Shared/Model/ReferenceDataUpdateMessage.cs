namespace Model;

using System.Text.Json;

public class ReferenceDataUpdateMessage
{
    public string DataType { get; set; }
    public JsonElement Data { get; set; }
    public string CorrelationId { get; set; }
    public DateTime Timestamp { get; set; }
}

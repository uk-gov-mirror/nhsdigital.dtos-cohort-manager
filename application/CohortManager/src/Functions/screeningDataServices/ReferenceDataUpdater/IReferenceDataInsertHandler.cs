namespace ReferenceDataUpdater;

using System.Text.Json;

public interface IReferenceDataInsertHandler
{
    Task<bool> ProcessRecord(string dataType, JsonElement data);
}

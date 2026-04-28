namespace ReferenceDataUpdater;

using System.Text;
using System.Text.Json;
using Common;
using DataServices.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Model;

public class ReferenceDataInsertHandler : IReferenceDataInsertHandler
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    private static readonly Dictionary<string, (Type EntityType, string BlobFileName)> TypeRegistry = new(StringComparer.OrdinalIgnoreCase)
    {
        ["BsSelectGpPractice"] = (typeof(BsSelectGpPractice), "BsSelectGpPractice.json"),
        ["BsSelectOutCode"] = (typeof(BsSelectOutCode), "BsSelectOutCode.json"),
        ["CurrentPosting"] = (typeof(CurrentPosting), "CurrentPosting.json"),
        ["ExcludedSMULookup"] = (typeof(ExcludedSMULookup), "ExcludedSMULookup.json"),
        ["LanguageCode"] = (typeof(LanguageCode), "LanguageCode.json"),
        ["ScreeningLkp"] = (typeof(ScreeningLkp), "ScreeningLkp.json"),
        ["GeneCodeLkp"] = (typeof(GeneCodeLkp), "GeneCodeLkp.json"),
        ["HigherRiskReferralReasonLkp"] = (typeof(HigherRiskReferralReasonLkp), "HigherRiskReferralReasonLkp.json"),
        ["BsoOrganisation"] = (typeof(BsoOrganisation), "BsoOrganisation.json"),
        ["GenderMaster"] = (typeof(GenderMaster), "GenderMaster.json"),
    };

    private readonly IServiceProvider _serviceProvider;
    private readonly IBlobStorageHelper _blobStorageHelper;
    private readonly ILogger<ReferenceDataInsertHandler> _logger;

    public ReferenceDataInsertHandler(
        IServiceProvider serviceProvider,
        IBlobStorageHelper blobStorageHelper,
        ILogger<ReferenceDataInsertHandler> logger)
    {
        _serviceProvider = serviceProvider;
        _blobStorageHelper = blobStorageHelper;
        _logger = logger;
    }

    public async Task<bool> ProcessRecord(string dataType, JsonElement data)
    {
        if (!TypeRegistry.TryGetValue(dataType, out var registration))
        {
            _logger.LogError("Unknown reference data type: {DataType}", dataType);
            return false;
        }

        var entity = JsonSerializer.Deserialize(data.GetRawText(), registration.EntityType, JsonOptions);
        if (entity is null)
        {
            _logger.LogError("Failed to deserialise payload for type {DataType}.", dataType);
            return false;
        }

        var dbInserted = await InsertIntoDatabase(dataType, registration.EntityType, entity);
        await AppendToBlob(dataType, registration.BlobFileName, data);

        return dbInserted;
    }

    private async Task<bool> InsertIntoDatabase(string dataType, Type entityType, object entity)
    {
        try
        {
            var accessorType = typeof(IDataServiceAccessor<>).MakeGenericType(entityType);
            var accessor = _serviceProvider.GetRequiredService(accessorType);

            var insertMethod = accessorType.GetMethod("InsertSingle")!;
            var task = (Task<bool>)insertMethod.Invoke(accessor, new[] { entity })!;
            var result = await task;

            if (!result)
            {
                _logger.LogWarning("InsertSingle returned false for type {DataType}. Record may be a duplicate.", dataType);
            }

            return true;
        }
        catch (DbUpdateException ex) when (IsPrimaryKeyViolation(ex))
        {
            _logger.LogWarning("Duplicate record detected for type {DataType}. Skipping insert.", dataType);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to insert record into database for type {DataType}.", dataType);
            return false;
        }
    }

    private async Task AppendToBlob(string dataType, string blobFileName, JsonElement newRecord)
    {
        var connectionString = Environment.GetEnvironmentVariable("AzureWebJobsStorage");
        var containerName = Environment.GetEnvironmentVariable("SeedDataBlobContainer") ?? "seed-data";

        try
        {
            var existingRecords = new List<JsonElement>();

            var existingBlob = await _blobStorageHelper.GetFileFromBlobStorage(connectionString!, containerName, blobFileName);
            if (existingBlob?.Data != null)
            {
                existingBlob.Data.Position = 0;
                using var reader = new StreamReader(existingBlob.Data);
                var existingJson = await reader.ReadToEndAsync();
                existingRecords = JsonSerializer.Deserialize<List<JsonElement>>(existingJson, JsonOptions) ?? new List<JsonElement>();
            }

            existingRecords.Add(newRecord);

            var updatedJson = JsonSerializer.Serialize(existingRecords, JsonOptions);
            var bytes = Encoding.UTF8.GetBytes(updatedJson);
            var blobFile = new BlobFile(bytes, blobFileName);

            await _blobStorageHelper.UploadFileToBlobStorage(connectionString!, containerName, blobFile, overwrite: true);

            _logger.LogInformation(
                "Appended record to blob {BlobFileName} for type {DataType}. Total records: {Count}",
                blobFileName, dataType, existingRecords.Count);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Failed to append record to blob for type {DataType}. DB insert was successful; blob is out of sync.",
                dataType);
        }
    }

    private static bool IsPrimaryKeyViolation(DbUpdateException ex)
    {
        return ex.InnerException?.Message?.Contains("duplicate key", StringComparison.OrdinalIgnoreCase) == true
            || ex.InnerException?.Message?.Contains("violation of primary key", StringComparison.OrdinalIgnoreCase) == true
            || ex.InnerException?.Message?.Contains("unique constraint", StringComparison.OrdinalIgnoreCase) == true;
    }
}

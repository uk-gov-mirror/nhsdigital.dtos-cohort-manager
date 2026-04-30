namespace ReferenceDataUpdater;

using System.Collections.Concurrent;
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
    private readonly string _storageConnectionString;
    private readonly string _seedDataContainerName;
    private static readonly ConcurrentDictionary<Type, Func<object, object, Task<bool>>> _insertDelegates = new();

    public ReferenceDataInsertHandler(
        IServiceProvider serviceProvider,
        IBlobStorageHelper blobStorageHelper,
        ILogger<ReferenceDataInsertHandler> logger)
    {
        _serviceProvider = serviceProvider;
        _blobStorageHelper = blobStorageHelper;
        _logger = logger;
        _storageConnectionString = Environment.GetEnvironmentVariable("AzureWebJobsStorage")
            ?? throw new InvalidOperationException("AzureWebJobsStorage environment variable is not set.");
        _seedDataContainerName = Environment.GetEnvironmentVariable("SeedDataBlobContainer") ?? "seed-data";
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

            var invoker = _insertDelegates.GetOrAdd(entityType, t =>
            {
                var aType = typeof(IDataServiceAccessor<>).MakeGenericType(t);
                var method = aType.GetMethod("InsertSingle")!;

                var accessorParam = System.Linq.Expressions.Expression.Parameter(typeof(object), "a");
                var entityParam = System.Linq.Expressions.Expression.Parameter(typeof(object), "e");

                var call = System.Linq.Expressions.Expression.Call(
                    System.Linq.Expressions.Expression.Convert(accessorParam, aType),
                    method,
                    System.Linq.Expressions.Expression.Convert(entityParam, t));

                return System.Linq.Expressions.Expression.Lambda<Func<object, object, Task<bool>>>(
                    call, accessorParam, entityParam).Compile();
            });

            var result = await invoker(accessor, entity);

            if (!result)
            {
                _logger.LogWarning("InsertSingle returned false for type {DataType}. Record may not have been inserted, possibly because it already exists.", dataType);
            }

            return result;
        }
        catch (DbUpdateException ex) when (IsPrimaryKeyViolation(ex))
        {
            _logger.LogWarning(ex, "Duplicate record detected for type {DataType}. Skipping insert.", dataType);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to insert record into database for type {DataType}.", dataType);
            return false;
        }
    }

    private async Task AppendToBlob(string dataType, string blobFileName, JsonElement newRecord)
    {
        try
        {
            var existingRecords = new List<JsonElement>();

            var existingBlob = await _blobStorageHelper.GetFileFromBlobStorage(_storageConnectionString, _seedDataContainerName, blobFileName);
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

            await _blobStorageHelper.UploadFileToBlobStorage(_storageConnectionString, _seedDataContainerName, blobFile, overwrite: true);

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
        const int SqlServerPrimaryKeyViolation = 2627;
        const int SqlServerUniqueConstraintViolation = 2601;

        for (Exception? current = ex.InnerException; current is not null; current = current.InnerException)
        {
            var numberProperty = current.GetType().GetProperty("Number");
            if (numberProperty?.PropertyType == typeof(int))
            {
                var errorNumber = (int)numberProperty.GetValue(current)!;
                if (errorNumber == SqlServerPrimaryKeyViolation || errorNumber == SqlServerUniqueConstraintViolation)
                {
                    return true;
                }
            }
        }

        return false;
    }
}

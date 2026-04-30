namespace NHS.CohortManager.Tests.UnitTests.ScreeningDataServicesTests;

using System.Text;
using System.Text.Json;
using Common;
using DataServices.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Model;
using Moq;
using ReferenceDataUpdater;

[TestClass]
public class ReferenceDataInsertHandlerTests
{
    private readonly Mock<IServiceProvider> _serviceProviderMock = new();
    private readonly Mock<IBlobStorageHelper> _blobStorageHelperMock = new();
    private readonly Mock<ILogger<ReferenceDataInsertHandler>> _loggerMock = new();
    private readonly ReferenceDataInsertHandler _handler;
    private readonly string? _originalAzureWebJobsStorage;
    private readonly string? _originalSeedDataBlobContainer;

    public ReferenceDataInsertHandlerTests()
    {
        _originalAzureWebJobsStorage = Environment.GetEnvironmentVariable("AzureWebJobsStorage");
        _originalSeedDataBlobContainer = Environment.GetEnvironmentVariable("SeedDataBlobContainer");

        Environment.SetEnvironmentVariable("AzureWebJobsStorage", "UseDevelopmentStorage=true");
        Environment.SetEnvironmentVariable("SeedDataBlobContainer", "seed-data");

        _handler = new ReferenceDataInsertHandler(
            _serviceProviderMock.Object,
            _blobStorageHelperMock.Object,
            _loggerMock.Object);
    }

    [TestCleanup]
    public void Cleanup()
    {
        Environment.SetEnvironmentVariable("AzureWebJobsStorage", _originalAzureWebJobsStorage);
        Environment.SetEnvironmentVariable("SeedDataBlobContainer", _originalSeedDataBlobContainer);
    }

    private Mock<IDataServiceAccessor<T>> SetupAccessor<T>(bool insertResult = true) where T : class
    {
        var accessorMock = new Mock<IDataServiceAccessor<T>>();
        accessorMock.Setup(a => a.InsertSingle(It.IsAny<T>())).ReturnsAsync(insertResult);
        _serviceProviderMock.Setup(sp => sp.GetService(typeof(IDataServiceAccessor<T>)))
            .Returns(accessorMock.Object);
        return accessorMock;
    }

    private void SetupBlobStorageDefaults(string blobFileName)
    {
        _blobStorageHelperMock.Setup(b => b.GetFileFromBlobStorage(It.IsAny<string>(), It.IsAny<string>(), blobFileName))
            .ReturnsAsync((BlobFile)null!);
        _blobStorageHelperMock.Setup(b => b.UploadFileToBlobStorage(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<BlobFile>(), true))
            .ReturnsAsync(true);
    }

    private static JsonElement CreatePayload(object data)
    {
        return JsonSerializer.SerializeToElement(data);
    }

    [TestMethod]
    public async Task ProcessRecord_UnknownDataType_ReturnsFalse()
    {
        // Arrange
        var data = CreatePayload(new { Id = 1 });

        // Act
        var result = await _handler.ProcessRecord("NonExistentType", data);

        // Assert
        Assert.IsFalse(result);
    }

    [TestMethod]
    public async Task ProcessRecord_ValidRecord_InsertsAndAppendsToBlob_ReturnsTrue()
    {
        // Arrange
        var data = CreatePayload(new { GpPracticeCode = "Y12345" });
        var accessorMock = SetupAccessor<BsSelectGpPractice>();
        SetupBlobStorageDefaults("BsSelectGpPractice.json");

        // Act
        var result = await _handler.ProcessRecord("BsSelectGpPractice", data);

        // Assert
        Assert.IsTrue(result);
        accessorMock.Verify(a => a.InsertSingle(It.IsAny<BsSelectGpPractice>()), Times.Once);
        _blobStorageHelperMock.Verify(b => b.UploadFileToBlobStorage(It.IsAny<string>(), "seed-data", It.IsAny<BlobFile>(), true), Times.Once);
    }

    [TestMethod]
    public async Task ProcessRecord_DatabaseInsertThrows_ReturnsFalse()
    {
        // Arrange
        var data = CreatePayload(new { ScreeningLkpId = 1 });
        var accessorMock = new Mock<IDataServiceAccessor<ScreeningLkp>>();
        accessorMock.Setup(a => a.InsertSingle(It.IsAny<ScreeningLkp>()))
            .ThrowsAsync(new Exception("Database connection failed"));
        _serviceProviderMock.Setup(sp => sp.GetService(typeof(IDataServiceAccessor<ScreeningLkp>)))
            .Returns(accessorMock.Object);

        // Act
        var result = await _handler.ProcessRecord("ScreeningLkp", data);

        // Assert
        Assert.IsFalse(result);
    }

    [TestMethod]
    public async Task ProcessRecord_PrimaryKeyViolation_ReturnsFalse()
    {
        // Arrange
        var data = CreatePayload(new { LanguageCodeId = "EN", LanguageDescription = "English" });
        var sqlException = new SqlExceptionWithNumber(2627);
        var dbUpdateException = new DbUpdateException("An error occurred", sqlException);

        var accessorMock = new Mock<IDataServiceAccessor<LanguageCode>>();
        accessorMock.Setup(a => a.InsertSingle(It.IsAny<LanguageCode>())).ThrowsAsync(dbUpdateException);
        _serviceProviderMock.Setup(sp => sp.GetService(typeof(IDataServiceAccessor<LanguageCode>)))
            .Returns(accessorMock.Object);
        SetupBlobStorageDefaults("LanguageCode.json");

        // Act
        var result = await _handler.ProcessRecord("LanguageCode", data);

        // Assert
        Assert.IsFalse(result);
    }

    [TestMethod]
    public async Task ProcessRecord_UniqueConstraintViolation_ReturnsFalse()
    {
        // Arrange
        var data = CreatePayload(new { GenderCode = "M" });
        var sqlException = new SqlExceptionWithNumber(2601);
        var dbUpdateException = new DbUpdateException("An error occurred", sqlException);

        var accessorMock = new Mock<IDataServiceAccessor<GenderMaster>>();
        accessorMock.Setup(a => a.InsertSingle(It.IsAny<GenderMaster>())).ThrowsAsync(dbUpdateException);
        _serviceProviderMock.Setup(sp => sp.GetService(typeof(IDataServiceAccessor<GenderMaster>)))
            .Returns(accessorMock.Object);
        SetupBlobStorageDefaults("GenderMaster.json");

        // Act
        var result = await _handler.ProcessRecord("GenderMaster", data);

        // Assert
        Assert.IsFalse(result);
    }

    [TestMethod]
    public async Task ProcessRecord_BlobAppendFails_ReturnsTrue()
    {
        // Arrange
        var data = CreatePayload(new { OutCode = "AB1" });
        SetupAccessor<BsSelectOutCode>();
        _blobStorageHelperMock.Setup(b => b.GetFileFromBlobStorage(It.IsAny<string>(), It.IsAny<string>(), "BsSelectOutCode.json"))
            .ThrowsAsync(new Exception("Blob storage unavailable"));

        // Act
        var result = await _handler.ProcessRecord("BsSelectOutCode", data);

        // Assert
        Assert.IsTrue(result);
    }

    [TestMethod]
    public async Task ProcessRecord_ExistingBlobRecords_AppendsNewRecord()
    {
        // Arrange
        var existingJson = JsonSerializer.Serialize(new[] { new { PostingId = 1 } });
        var existingBlob = new BlobFile(Encoding.UTF8.GetBytes(existingJson), "CurrentPosting.json");
        var data = CreatePayload(new { PostingId = 2 });

        SetupAccessor<CurrentPosting>();
        _blobStorageHelperMock.Setup(b => b.GetFileFromBlobStorage(It.IsAny<string>(), It.IsAny<string>(), "CurrentPosting.json"))
            .ReturnsAsync(existingBlob);

        BlobFile? uploadedBlob = null;
        _blobStorageHelperMock.Setup(b => b.UploadFileToBlobStorage(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<BlobFile>(), true))
            .Callback<string, string, BlobFile, bool>((_, _, blob, _) => uploadedBlob = blob)
            .ReturnsAsync(true);

        // Act
        var result = await _handler.ProcessRecord("CurrentPosting", data);

        // Assert
        Assert.IsTrue(result);
        Assert.IsNotNull(uploadedBlob);

        uploadedBlob.Data.Position = 0;
        using var reader = new StreamReader(uploadedBlob.Data);
        var uploadedJson = await reader.ReadToEndAsync();
        var records = JsonSerializer.Deserialize<List<JsonElement>>(uploadedJson);
        Assert.AreEqual(2, records!.Count);
    }

    [TestMethod]
    public async Task ProcessRecord_NullJsonPayload_ReturnsFalse()
    {
        // Arrange
        var data = JsonSerializer.SerializeToElement<object>(null!);

        // Act
        var result = await _handler.ProcessRecord("BsSelectGpPractice", data);

        // Assert
        Assert.IsFalse(result);
    }

    [DataRow("BsSelectGpPractice", DisplayName = "BsSelectGpPractice is a registered type")]
    [DataRow("BsSelectOutCode", DisplayName = "BsSelectOutCode is a registered type")]
    [DataRow("CurrentPosting", DisplayName = "CurrentPosting is a registered type")]
    [DataRow("ExcludedSMULookup", DisplayName = "ExcludedSMULookup is a registered type")]
    [DataRow("LanguageCode", DisplayName = "LanguageCode is a registered type")]
    [DataRow("ScreeningLkp", DisplayName = "ScreeningLkp is a registered type")]
    [DataRow("GeneCodeLkp", DisplayName = "GeneCodeLkp is a registered type")]
    [DataRow("HigherRiskReferralReasonLkp", DisplayName = "HigherRiskReferralReasonLkp is a registered type")]
    [DataRow("BsoOrganisation", DisplayName = "BsoOrganisation is a registered type")]
    [DataRow("GenderMaster", DisplayName = "GenderMaster is a registered type")]
    [TestMethod]
    public async Task ProcessRecord_RegisteredDataType_DoesNotLogUnknownType(string dataType)
    {
        // Arrange
        var data = CreatePayload(new { Id = 1 });
        _serviceProviderMock.Setup(sp => sp.GetService(It.IsAny<Type>())).Returns(null!);

        // Act
        await _handler.ProcessRecord(dataType, data);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Unknown reference data type")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()
            ), Times.Never);
    }

    [TestMethod]
    public async Task ProcessRecord_CaseInsensitiveDataType_ReturnsTrue()
    {
        // Arrange
        var data = CreatePayload(new { GpPracticeCode = "Y12345" });
        var accessorMock = SetupAccessor<BsSelectGpPractice>();
        SetupBlobStorageDefaults("BsSelectGpPractice.json");

        // Act
        var result = await _handler.ProcessRecord("bsselectgppractice", data);

        // Assert
        Assert.IsTrue(result);
        accessorMock.Verify(a => a.InsertSingle(It.IsAny<BsSelectGpPractice>()), Times.Once);
    }

    [TestMethod]
    public async Task ProcessRecord_InsertSingleReturnsFalse_ReturnsFalse()
    {
        // Arrange
        var data = CreatePayload(new { GpPracticeCode = "Y99999" });
        SetupAccessor<BsSelectGpPractice>(insertResult: false);
        SetupBlobStorageDefaults("BsSelectGpPractice.json");

        // Act
        var result = await _handler.ProcessRecord("BsSelectGpPractice", data);

        // Assert
        Assert.IsFalse(result);
    }
}

internal class SqlExceptionWithNumber(int number) : Exception($"SQL error {number}")
{
    public int Number { get; } = number;
}

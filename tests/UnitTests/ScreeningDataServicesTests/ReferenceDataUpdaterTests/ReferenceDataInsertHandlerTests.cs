namespace NHS.CohortManager.Tests.UnitTests.ScreeningDataServicesTests;

using System.Text;
using System.Text.Json;
using Common;
using DataServices.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
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

    public ReferenceDataInsertHandlerTests()
    {
        Environment.SetEnvironmentVariable("AzureWebJobsStorage", "UseDevelopmentStorage=true");
        Environment.SetEnvironmentVariable("SeedDataBlobContainer", "seed-data");

        _handler = new ReferenceDataInsertHandler(
            _serviceProviderMock.Object,
            _blobStorageHelperMock.Object,
            _loggerMock.Object);
    }

    [TestMethod]
    public async Task ProcessRecord_UnknownDataType_ReturnsFalse()
    {
        // Arrange
        var data = JsonSerializer.SerializeToElement(new { Id = 1 });

        // Act
        var result = await _handler.ProcessRecord("NonExistentType", data);

        // Assert
        Assert.IsFalse(result);
    }

    [TestMethod]
    public async Task ProcessRecord_ValidDataType_InsertsIntoDatabaseAndAppendsToBlob_ReturnsTrue()
    {
        // Arrange
        var gpPractice = new { GpPracticeCode = "Y12345", GpPracticeName = "Test Practice" };
        var data = JsonSerializer.SerializeToElement(gpPractice);

        var accessorMock = new Mock<IDataServiceAccessor<BsSelectGpPractice>>();
        accessorMock.Setup(a => a.InsertSingle(It.IsAny<BsSelectGpPractice>())).ReturnsAsync(true);
        _serviceProviderMock.Setup(sp => sp.GetService(typeof(IDataServiceAccessor<BsSelectGpPractice>)))
            .Returns(accessorMock.Object);

        _blobStorageHelperMock.Setup(b => b.GetFileFromBlobStorage(It.IsAny<string>(), It.IsAny<string>(), "BsSelectGpPractice.json"))
            .ReturnsAsync((BlobFile)null!);
        _blobStorageHelperMock.Setup(b => b.UploadFileToBlobStorage(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<BlobFile>(), true))
            .ReturnsAsync(true);

        // Act
        var result = await _handler.ProcessRecord("BsSelectGpPractice", data);

        // Assert
        Assert.IsTrue(result);
        accessorMock.Verify(a => a.InsertSingle(It.IsAny<BsSelectGpPractice>()), Times.Once);
        _blobStorageHelperMock.Verify(b => b.UploadFileToBlobStorage(It.IsAny<string>(), "seed-data", It.IsAny<BlobFile>(), true), Times.Once);
    }

    [TestMethod]
    public async Task ProcessRecord_DatabaseInsertFails_ReturnsFalse()
    {
        // Arrange
        var data = JsonSerializer.SerializeToElement(new { ScreeningLkpId = 1 });

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
    public async Task ProcessRecord_DuplicateKeyViolation_ReturnsTrue()
    {
        // Arrange
        var data = JsonSerializer.SerializeToElement(new { LanguageCodeId = "EN", LanguageDescription = "English" });

        var innerException = new Exception("Violation of PRIMARY KEY constraint");
        var dbUpdateException = new DbUpdateException("An error occurred", innerException);

        var accessorMock = new Mock<IDataServiceAccessor<LanguageCode>>();
        accessorMock.Setup(a => a.InsertSingle(It.IsAny<LanguageCode>()))
            .ThrowsAsync(dbUpdateException);
        _serviceProviderMock.Setup(sp => sp.GetService(typeof(IDataServiceAccessor<LanguageCode>)))
            .Returns(accessorMock.Object);

        _blobStorageHelperMock.Setup(b => b.GetFileFromBlobStorage(It.IsAny<string>(), It.IsAny<string>(), "LanguageCode.json"))
            .ReturnsAsync((BlobFile)null!);
        _blobStorageHelperMock.Setup(b => b.UploadFileToBlobStorage(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<BlobFile>(), true))
            .ReturnsAsync(true);

        // Act
        var result = await _handler.ProcessRecord("LanguageCode", data);

        // Assert
        Assert.IsTrue(result);
    }

    [TestMethod]
    public async Task ProcessRecord_DuplicateKey_UniqueConstraintMessage_ReturnsTrue()
    {
        // Arrange
        var data = JsonSerializer.SerializeToElement(new { GenderCode = "M" });

        var innerException = new Exception("unique constraint violation on table");
        var dbUpdateException = new DbUpdateException("An error occurred", innerException);

        var accessorMock = new Mock<IDataServiceAccessor<GenderMaster>>();
        accessorMock.Setup(a => a.InsertSingle(It.IsAny<GenderMaster>()))
            .ThrowsAsync(dbUpdateException);
        _serviceProviderMock.Setup(sp => sp.GetService(typeof(IDataServiceAccessor<GenderMaster>)))
            .Returns(accessorMock.Object);

        _blobStorageHelperMock.Setup(b => b.GetFileFromBlobStorage(It.IsAny<string>(), It.IsAny<string>(), "GenderMaster.json"))
            .ReturnsAsync((BlobFile)null!);
        _blobStorageHelperMock.Setup(b => b.UploadFileToBlobStorage(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<BlobFile>(), true))
            .ReturnsAsync(true);

        // Act
        var result = await _handler.ProcessRecord("GenderMaster", data);

        // Assert
        Assert.IsTrue(result);
    }

    [TestMethod]
    public async Task ProcessRecord_BlobAppendFails_StillReturnsTrue()
    {
        // Arrange
        var data = JsonSerializer.SerializeToElement(new { OutCode = "AB1" });

        var accessorMock = new Mock<IDataServiceAccessor<BsSelectOutCode>>();
        accessorMock.Setup(a => a.InsertSingle(It.IsAny<BsSelectOutCode>())).ReturnsAsync(true);
        _serviceProviderMock.Setup(sp => sp.GetService(typeof(IDataServiceAccessor<BsSelectOutCode>)))
            .Returns(accessorMock.Object);

        _blobStorageHelperMock.Setup(b => b.GetFileFromBlobStorage(It.IsAny<string>(), It.IsAny<string>(), "BsSelectOutCode.json"))
            .ThrowsAsync(new Exception("Blob storage unavailable"));

        // Act
        var result = await _handler.ProcessRecord("BsSelectOutCode", data);

        // Assert
        Assert.IsTrue(result);
    }

    [TestMethod]
    public async Task ProcessRecord_ExistingBlobData_AppendsNewRecord()
    {
        // Arrange
        var existingRecords = new[] { new { PostingId = 1 } };
        var existingJson = JsonSerializer.Serialize(existingRecords);
        var existingBlob = new BlobFile(Encoding.UTF8.GetBytes(existingJson), "CurrentPosting.json");

        var data = JsonSerializer.SerializeToElement(new { PostingId = 2 });

        var accessorMock = new Mock<IDataServiceAccessor<CurrentPosting>>();
        accessorMock.Setup(a => a.InsertSingle(It.IsAny<CurrentPosting>())).ReturnsAsync(true);
        _serviceProviderMock.Setup(sp => sp.GetService(typeof(IDataServiceAccessor<CurrentPosting>)))
            .Returns(accessorMock.Object);

        _blobStorageHelperMock.Setup(b => b.GetFileFromBlobStorage(It.IsAny<string>(), It.IsAny<string>(), "CurrentPosting.json"))
            .ReturnsAsync(existingBlob);
        _blobStorageHelperMock.Setup(b => b.UploadFileToBlobStorage(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<BlobFile>(), true))
            .ReturnsAsync(true);

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
    public async Task ProcessRecord_NullDeserialisation_ReturnsFalse()
    {
        // Arrange — a payload that deserialises to something but is "null" in a JSON sense
        var data = JsonSerializer.SerializeToElement<object>(null!);

        // Act
        var result = await _handler.ProcessRecord("BsSelectGpPractice", data);

        // Assert
        Assert.IsFalse(result);
    }

    [TestMethod]
    [DataRow("BsSelectGpPractice")]
    [DataRow("BsSelectOutCode")]
    [DataRow("CurrentPosting")]
    [DataRow("ExcludedSMULookup")]
    [DataRow("LanguageCode")]
    [DataRow("ScreeningLkp")]
    [DataRow("GeneCodeLkp")]
    [DataRow("HigherRiskReferralReasonLkp")]
    [DataRow("BsoOrganisation")]
    [DataRow("GenderMaster")]
    public async Task ProcessRecord_AllRegisteredTypes_AreRecognised(string dataType)
    {
        // Arrange — just verify the type is recognised (will fail on deserialise but not on lookup)
        var data = JsonSerializer.SerializeToElement(new { Id = 1 });

        // We need a mock accessor for whatever type this maps to.
        // Since we can't easily set up all 10, verify it doesn't return false for "unknown type".
        // The handler will attempt to resolve from DI and fail, but that's a different error path.
        _serviceProviderMock.Setup(sp => sp.GetService(It.IsAny<Type>())).Returns(null!);

        // Act
        var result = await _handler.ProcessRecord(dataType, data);

        // Assert — will be false because DI can't resolve the accessor, but it should NOT be
        // false because the type was unknown. We verify no "Unknown reference data type" log.
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
    public async Task ProcessRecord_DataTypeLookup_IsCaseInsensitive()
    {
        // Arrange
        var data = JsonSerializer.SerializeToElement(new { GpPracticeCode = "Y12345" });

        var accessorMock = new Mock<IDataServiceAccessor<BsSelectGpPractice>>();
        accessorMock.Setup(a => a.InsertSingle(It.IsAny<BsSelectGpPractice>())).ReturnsAsync(true);
        _serviceProviderMock.Setup(sp => sp.GetService(typeof(IDataServiceAccessor<BsSelectGpPractice>)))
            .Returns(accessorMock.Object);

        _blobStorageHelperMock.Setup(b => b.GetFileFromBlobStorage(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync((BlobFile)null!);
        _blobStorageHelperMock.Setup(b => b.UploadFileToBlobStorage(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<BlobFile>(), true))
            .ReturnsAsync(true);

        // Act
        var result = await _handler.ProcessRecord("bsselectgppractice", data);

        // Assert
        Assert.IsTrue(result);
        accessorMock.Verify(a => a.InsertSingle(It.IsAny<BsSelectGpPractice>()), Times.Once);
    }

    [TestMethod]
    public async Task ProcessRecord_InsertSingleReturnsFalse_StillReturnsTrue()
    {
        // Arrange — InsertSingle returns false (e.g. no rows affected) but no exception
        var data = JsonSerializer.SerializeToElement(new { GpPracticeCode = "Y99999" });

        var accessorMock = new Mock<IDataServiceAccessor<BsSelectGpPractice>>();
        accessorMock.Setup(a => a.InsertSingle(It.IsAny<BsSelectGpPractice>())).ReturnsAsync(false);
        _serviceProviderMock.Setup(sp => sp.GetService(typeof(IDataServiceAccessor<BsSelectGpPractice>)))
            .Returns(accessorMock.Object);

        _blobStorageHelperMock.Setup(b => b.GetFileFromBlobStorage(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync((BlobFile)null!);
        _blobStorageHelperMock.Setup(b => b.UploadFileToBlobStorage(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<BlobFile>(), true))
            .ReturnsAsync(true);

        // Act
        var result = await _handler.ProcessRecord("BsSelectGpPractice", data);

        // Assert — returns true because InsertSingle returning false is treated as "maybe duplicate" not failure
        Assert.IsTrue(result);
    }
}

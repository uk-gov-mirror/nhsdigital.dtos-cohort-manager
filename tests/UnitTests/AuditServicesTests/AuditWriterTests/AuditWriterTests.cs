namespace NHS.CohortManager.Tests.UnitTests.AuditServicesTests;

using System.Text.Json;
using DataServices.Database;
using Microsoft.Azure.Functions.Worker;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Common;
using Model;
using Model.Enums;
using Moq;
using NHS.CohortManager.AuditServices;

[TestClass]
public class AuditWriterTests
{
    private Mock<DataServicesContext> _mockDbContext;
    private Mock<DbSet<ParticipantAuditLog>> _mockDbSet;
    private Mock<IBlobStorageHelper> _mockBlobStorageHelper;
    private Mock<ILogger<AuditWriter>> _mockLogger;
    private Mock<FunctionContext> _mockFunctionContext;
    private AuditWriter _auditWriterService;
    private List<ParticipantAuditLog> _addedEntities;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    [TestInitialize]
    public void Setup()
    {
        _addedEntities = [];

        _mockDbContext = new Mock<DataServicesContext>(new DbContextOptions<DataServicesContext>());
        _mockDbContext.Setup(x => x.SaveChangesAsync(default)).ReturnsAsync(1);

        _mockDbSet = new Mock<DbSet<ParticipantAuditLog>>();
        _mockDbSet.Setup(x => x.Add(It.IsAny<ParticipantAuditLog>()))
            .Callback<ParticipantAuditLog>(entity => _addedEntities.Add(entity));

        _mockDbContext.Object.participantAuditLogs = _mockDbSet.Object;

        _mockBlobStorageHelper = new Mock<IBlobStorageHelper>();
        _mockBlobStorageHelper
            .Setup(x => x.UploadFileToBlobStorageAndGetUri(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<BlobFile>(),
                It.IsAny<bool>()))
            .ReturnsAsync("https://storage.blob.core.windows.net/participant-audit/test.json");

        _mockLogger = new Mock<ILogger<AuditWriter>>();
        _mockFunctionContext = new Mock<FunctionContext>();

        var config = Options.Create(new AuditConfig
        {
            ServiceBusConnectionString = "Endpoint=sb://unit-test/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=test=",
            AzureWebJobsStorage = "UseDevelopmentStorage=true",
            AuditTopicName = "audit-topic"
        });

        _auditWriterService = new AuditWriter(
            _mockDbContext.Object,
            _mockBlobStorageHelper.Object,
            config,
            _mockLogger.Object);
    }

    [TestMethod]
    public async Task Run_ValidMessage_PersistsAuditLog()
    {
        // Arrange
        var audit = CreateAuditMessage();
        audit.RequestSnapshot = new { NhsNumber = audit.NhsNumber, Event = "Created" };
        var messageText = JsonSerializer.Serialize(audit, JsonOptions);

        // Act
        await _auditWriterService.Run(messageText, _mockFunctionContext.Object);

        // Assert
        Assert.AreEqual(1, _addedEntities.Count);
        var saved = _addedEntities[0];
        Assert.AreEqual(audit.NhsNumber, saved.NhsNumber);
        Assert.AreEqual(audit.CorrelationId, saved.CorrelationId);
        Assert.AreEqual((int)audit.Source, saved.RecordSource);
        Assert.AreEqual(audit.CreatedBy, saved.CreatedBy);
        Assert.AreEqual("https://storage.blob.core.windows.net/participant-audit/test.json", saved.RawDataRef);
        _mockDbContext.Verify(x => x.SaveChangesAsync(default), Times.Once);
    }

    [TestMethod]
    public async Task Run_ValidMessage_PersistsBlobRefFromBlobWrite()
    {
        // Arrange
        var audit = CreateAuditMessage();
        audit.RequestSnapshot = new { Field = "value" };
        var messageText = JsonSerializer.Serialize(audit, JsonOptions);

        // Act
        await _auditWriterService.Run(messageText, _mockFunctionContext.Object);

        // Assert
        Assert.AreEqual(1, _addedEntities.Count);
        Assert.AreEqual("https://storage.blob.core.windows.net/participant-audit/test.json", _addedEntities[0].RawDataRef);
        _mockBlobStorageHelper.Verify(
            x => x.UploadFileToBlobStorageAndGetUri(
                It.IsAny<string>(),
                "participant-audit",
                It.IsAny<BlobFile>(),
                true),
            Times.Once);
    }

    [TestMethod]
    public async Task Run_WhenRawDataRefIsNull_SavesNullRefAndContinues()
    {
        // Arrange
        var audit = CreateAuditMessage();
        var messageText = JsonSerializer.Serialize(audit, JsonOptions);

        // Act
        await _auditWriterService.Run(messageText, _mockFunctionContext.Object);

        // Assert
        Assert.AreEqual(1, _addedEntities.Count);
        Assert.IsNull(_addedEntities[0].RawDataRef);
        _mockDbContext.Verify(x => x.SaveChangesAsync(default), Times.Once);
        _mockBlobStorageHelper.Verify(
            x => x.UploadFileToBlobStorageAndGetUri(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<BlobFile>(),
                It.IsAny<bool>()),
            Times.Never);
    }

    [TestMethod]
    public async Task Run_WhenBlobWriteReturnsNull_ThrowsAndDoesNotPersist()
    {
        // Arrange
        _mockBlobStorageHelper
            .Setup(x => x.UploadFileToBlobStorageAndGetUri(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<BlobFile>(),
                It.IsAny<bool>()))
            .ReturnsAsync((string?)null);

        var audit = CreateAuditMessage();
        audit.RequestSnapshot = new { Field = "value" };
        var messageText = JsonSerializer.Serialize(audit, JsonOptions);

        // Act & Assert
        await Assert.ThrowsExceptionAsync<InvalidOperationException>(
            async () => await _auditWriterService.Run(messageText, _mockFunctionContext.Object));
        _mockDbContext.Verify(x => x.SaveChangesAsync(default), Times.Never);
    }

    [TestMethod]
    public async Task Run_WhenBlobWriteThrows_ThrowsAndDoesNotPersist()
    {
        // Arrange
        _mockBlobStorageHelper
            .Setup(x => x.UploadFileToBlobStorageAndGetUri(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<BlobFile>(),
                It.IsAny<bool>()))
            .ThrowsAsync(new InvalidOperationException("Blob unavailable"));

        var audit = CreateAuditMessage();
        audit.RequestSnapshot = new { Field = "value" };
        var messageText = JsonSerializer.Serialize(audit, JsonOptions);

        // Act & Assert
        await Assert.ThrowsExceptionAsync<InvalidOperationException>(
            async () => await _auditWriterService.Run(messageText, _mockFunctionContext.Object));
        _mockDbContext.Verify(x => x.SaveChangesAsync(default), Times.Never);
    }

    [TestMethod]
    public async Task Run_InvalidJson_LogsErrorAndReturns()
    {
        // Arrange
        var messageText = "not-valid-json!!!";

        // Act
        await _auditWriterService.Run(messageText, _mockFunctionContext.Object);

        // Assert
        Assert.AreEqual(0, _addedEntities.Count);
        _mockDbContext.Verify(x => x.SaveChangesAsync(default), Times.Never);
    }

    [TestMethod]
    public async Task Run_ValidMessage_MapsAllFieldsCorrectly()
    {
        // Arrange
        var batchId = Guid.NewGuid();
        var audit = new ParticipantAuditMessage
        {
            CorrelationId = Guid.NewGuid(),
            NhsNumber = "9876543210",
            Source = AuditSource.ParquetFile,
            BatchId = batchId,
            RecordSourceDesc = "Parquet file import",
            CreatedDatetime = new DateTime(2026, 4, 8, 10, 30, 0, DateTimeKind.Utc),
            CreatedBy = "TestFunction",
            ScreeningId = 1,
            RequestSnapshot = new { ScreeningId = 1, Event = "Import" }
        };
        var messageText = JsonSerializer.Serialize(audit, JsonOptions);

        // Act
        await _auditWriterService.Run(messageText, _mockFunctionContext.Object);

        // Assert
        Assert.AreEqual(1, _addedEntities.Count);
        var saved = _addedEntities[0];
        Assert.AreEqual(audit.CorrelationId, saved.CorrelationId);
        Assert.AreEqual("9876543210", saved.NhsNumber);
        Assert.AreEqual((int)AuditSource.ParquetFile, saved.RecordSource);
        Assert.AreEqual("Parquet file import", saved.RecordSourceDesc);
        Assert.AreEqual("TestFunction", saved.CreatedBy);
        Assert.AreEqual(1, saved.ScreeningId);
        Assert.AreEqual(batchId, saved.BatchId);
        Assert.AreEqual("https://storage.blob.core.windows.net/participant-audit/test.json", saved.RawDataRef);
    }

    [TestMethod]
    public async Task Run_WhenSaveChangesThrows_LogsErrorAndReturns()
    {
        // Arrange
        _mockDbContext.Setup(x => x.SaveChangesAsync(default))
            .ThrowsAsync(new InvalidOperationException("DB connection lost"));

        var audit = CreateAuditMessage();
        var messageText = JsonSerializer.Serialize(audit, JsonOptions);

        // Act
        await _auditWriterService.Run(messageText, _mockFunctionContext.Object);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => true),
                It.IsAny<InvalidOperationException>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    private static ParticipantAuditMessage CreateAuditMessage()
    {
        return new ParticipantAuditMessage
        {
            CorrelationId = Guid.NewGuid(),
            NhsNumber = "1234567890",
            Source = AuditSource.ManualAdd,
            RecordSourceDesc = "Test audit",
            CreatedDatetime = DateTime.UtcNow,
            CreatedBy = "UnitTest"
        };
    }
}

namespace NHS.CohortManager.Tests.UnitTests.SharedTests;

using Common;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Model;
using Model.Enums;
using Moq;

[TestClass]
public class AuditLogClientTests
{
    private Mock<IQueueClient> _mockQueueClient;
    private Mock<ILogger<AuditLogClient>> _mockLogger;
    private AuditLogClient _client;

    [TestInitialize]
    public void Setup()
    {
        _mockQueueClient = new Mock<IQueueClient>();
        _mockLogger = new Mock<ILogger<AuditLogClient>>();

        var config = Options.Create(new AuditClientConfig
        {
            AuditTopicName = "audit-topic",
            AzureWebJobsStorage = "UseDevelopmentStorage=true"
        });

        _client = new AuditLogClient(
            _mockQueueClient.Object,
            config,
            _mockLogger.Object);
    }

    [TestMethod]
    public async Task AddAsync_EnqueuesMessageWithoutMutatingRawDataRef()
    {
        // Arrange
        var audit = CreateAuditMessage();
        audit.RawDataRef = "existing-ref";
        audit.RequestSnapshot = new { Field = "value" };

        _mockQueueClient
            .Setup(x => x.AddAsync(audit, "audit-topic"))
            .ReturnsAsync(true);

        // Act
        await _client.AddAsync(audit);

        // Assert
        Assert.AreEqual("existing-ref", audit.RawDataRef);
        _mockQueueClient.Verify(x => x.AddAsync(audit, "audit-topic"), Times.Once);
    }

    [TestMethod]
    public async Task AddBatchAsync_EnqueuesMessagesWithoutMutatingRawDataRef()
    {
        // Arrange
        var messages = new List<ParticipantAuditMessage>
        {
            CreateAuditMessage(),
            CreateAuditMessage()
        };

        messages[0].RawDataRef = "ref-1";
        messages[1].RawDataRef = null;
        messages[0].RequestSnapshot = new { Field = "a" };
        messages[1].RequestSnapshot = new { Field = "b" };

        _mockQueueClient
            .Setup(x => x.AddBatchAsync(It.IsAny<IEnumerable<ParticipantAuditMessage>>(), "audit-topic"))
            .ReturnsAsync(0);

        // Act
        var result = await _client.AddBatchAsync(messages);

        // Assert
        Assert.AreEqual(0, result);
        Assert.AreEqual("ref-1", messages[0].RawDataRef);
        Assert.IsNull(messages[1].RawDataRef);
        _mockQueueClient.Verify(
            x => x.AddBatchAsync(
                It.Is<IEnumerable<ParticipantAuditMessage>>(m => m.Count() == 2),
                "audit-topic"),
            Times.Once);
    }

    [TestMethod]
    public async Task AddAsync_WhenQueueSendFails_LogsError()
    {
        // Arrange
        var audit = CreateAuditMessage();
        _mockQueueClient
            .Setup(x => x.AddAsync(audit, "audit-topic"))
            .ReturnsAsync(false);

        // Act
        await _client.AddAsync(audit);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => true),
                It.IsAny<Exception>(),
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
            RecordSourceDesc = "Unit test",
            CreatedDatetime = DateTime.UtcNow,
            CreatedBy = "UnitTest"
        };
    }
}

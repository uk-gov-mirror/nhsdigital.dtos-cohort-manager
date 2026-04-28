namespace NHS.CohortManager.Tests.UnitTests.ScreeningDataServicesTests;

using System.Text.Json;
using Azure.Messaging.ServiceBus;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Moq;
using Model;
using ReferenceDataUpdater;
using NHS.CohortManager.Tests.TestUtils;

[TestClass]
public class ReferenceDataUpdaterFunctionTests
{
    private readonly Mock<IReferenceDataInsertHandler> _insertHandlerMock = new();
    private readonly Mock<ILogger<ReferenceDataUpdaterFunction>> _loggerMock = new();
    private readonly Mock<ServiceBusMessageActions> _messageActionsMock = new();
    private readonly ReferenceDataUpdaterFunction _function;

    public ReferenceDataUpdaterFunctionTests()
    {
        _function = new ReferenceDataUpdaterFunction(_insertHandlerMock.Object, _loggerMock.Object);
    }

    private static ServiceBusReceivedMessage CreateServiceBusMessage(object body)
    {
        var json = JsonSerializer.Serialize(body);
        return ServiceBusModelFactory.ServiceBusReceivedMessage(
            body: new BinaryData(json),
            messageId: "test-message-id",
            correlationId: "test-correlation-id"
        );
    }

    private static ServiceBusReceivedMessage CreateServiceBusMessage(string rawBody)
    {
        return ServiceBusModelFactory.ServiceBusReceivedMessage(
            body: new BinaryData(rawBody),
            messageId: "test-message-id",
            correlationId: "test-correlation-id"
        );
    }

    [TestMethod]
    public async Task Run_ValidMessage_ProcessRecordSucceeds_CompletesMessage()
    {
        // Arrange
        var updateMessage = new ReferenceDataUpdateMessage
        {
            DataType = "BsSelectGpPractice",
            Data = JsonSerializer.SerializeToElement(new { Code = "Y12345", Name = "Test Practice" }),
            CorrelationId = "corr-001",
            Timestamp = DateTime.UtcNow
        };

        var message = CreateServiceBusMessage(updateMessage);
        _insertHandlerMock.Setup(h => h.ProcessRecord("BsSelectGpPractice", It.IsAny<JsonElement>()))
            .ReturnsAsync(true);

        // Act
        await _function.Run(message, _messageActionsMock.Object);

        // Assert
        _messageActionsMock.Verify(
            x => x.CompleteMessageAsync(It.IsAny<ServiceBusReceivedMessage>(), CancellationToken.None),
            Times.Once);
        _messageActionsMock.Verify(
            x => x.DeadLetterMessageAsync(It.IsAny<ServiceBusReceivedMessage>(), null, null, null, CancellationToken.None),
            Times.Never);
    }

    [TestMethod]
    public async Task Run_ValidMessage_ProcessRecordFails_DeadLettersMessage()
    {
        // Arrange
        var updateMessage = new ReferenceDataUpdateMessage
        {
            DataType = "UnknownType",
            Data = JsonSerializer.SerializeToElement(new { Id = 1 }),
            CorrelationId = "corr-002",
            Timestamp = DateTime.UtcNow
        };

        var message = CreateServiceBusMessage(updateMessage);
        _insertHandlerMock.Setup(h => h.ProcessRecord("UnknownType", It.IsAny<JsonElement>()))
            .ReturnsAsync(false);

        // Act
        await _function.Run(message, _messageActionsMock.Object);

        // Assert
        _messageActionsMock.Verify(
            x => x.DeadLetterMessageAsync(It.IsAny<ServiceBusReceivedMessage>(), null, null, null, CancellationToken.None),
            Times.Once);
        _messageActionsMock.Verify(
            x => x.CompleteMessageAsync(It.IsAny<ServiceBusReceivedMessage>(), CancellationToken.None),
            Times.Never);
    }

    [TestMethod]
    public async Task Run_InvalidJson_DeadLettersMessage()
    {
        // Arrange
        var message = CreateServiceBusMessage("not valid json {{{");

        // Act
        await _function.Run(message, _messageActionsMock.Object);

        // Assert
        _messageActionsMock.Verify(
            x => x.DeadLetterMessageAsync(It.IsAny<ServiceBusReceivedMessage>(), null, null, null, CancellationToken.None),
            Times.Once);
        _messageActionsMock.Verify(
            x => x.CompleteMessageAsync(It.IsAny<ServiceBusReceivedMessage>(), CancellationToken.None),
            Times.Never);
    }

    [TestMethod]
    public async Task Run_NullDataType_DeadLettersMessage()
    {
        // Arrange
        var updateMessage = new ReferenceDataUpdateMessage
        {
            DataType = null!,
            Data = JsonSerializer.SerializeToElement(new { Id = 1 }),
            CorrelationId = "corr-003",
            Timestamp = DateTime.UtcNow
        };

        var message = CreateServiceBusMessage(updateMessage);

        // Act
        await _function.Run(message, _messageActionsMock.Object);

        // Assert
        _messageActionsMock.Verify(
            x => x.DeadLetterMessageAsync(It.IsAny<ServiceBusReceivedMessage>(), null, null, null, CancellationToken.None),
            Times.Once);
        _insertHandlerMock.Verify(
            h => h.ProcessRecord(It.IsAny<string>(), It.IsAny<JsonElement>()),
            Times.Never);
    }

    [TestMethod]
    public async Task Run_EmptyDataType_DeadLettersMessage()
    {
        // Arrange
        var updateMessage = new ReferenceDataUpdateMessage
        {
            DataType = "   ",
            Data = JsonSerializer.SerializeToElement(new { Id = 1 }),
            CorrelationId = "corr-004",
            Timestamp = DateTime.UtcNow
        };

        var message = CreateServiceBusMessage(updateMessage);

        // Act
        await _function.Run(message, _messageActionsMock.Object);

        // Assert
        _messageActionsMock.Verify(
            x => x.DeadLetterMessageAsync(It.IsAny<ServiceBusReceivedMessage>(), null, null, null, CancellationToken.None),
            Times.Once);
        _insertHandlerMock.Verify(
            h => h.ProcessRecord(It.IsAny<string>(), It.IsAny<JsonElement>()),
            Times.Never);
    }

    [TestMethod]
    public async Task Run_ProcessRecordThrowsException_DeadLettersMessage()
    {
        // Arrange
        var updateMessage = new ReferenceDataUpdateMessage
        {
            DataType = "BsSelectGpPractice",
            Data = JsonSerializer.SerializeToElement(new { Code = "Y12345" }),
            CorrelationId = "corr-005",
            Timestamp = DateTime.UtcNow
        };

        var message = CreateServiceBusMessage(updateMessage);
        _insertHandlerMock.Setup(h => h.ProcessRecord("BsSelectGpPractice", It.IsAny<JsonElement>()))
            .ThrowsAsync(new InvalidOperationException("Something went wrong"));

        // Act
        await _function.Run(message, _messageActionsMock.Object);

        // Assert
        _messageActionsMock.Verify(
            x => x.DeadLetterMessageAsync(It.IsAny<ServiceBusReceivedMessage>(), null, null, null, CancellationToken.None),
            Times.Once);
        _messageActionsMock.Verify(
            x => x.CompleteMessageAsync(It.IsAny<ServiceBusReceivedMessage>(), CancellationToken.None),
            Times.Never);
    }

    [TestMethod]
    public async Task Run_EmptyMessageBody_DeadLettersMessage()
    {
        // Arrange
        var message = ServiceBusModelFactory.ServiceBusReceivedMessage(
            body: new BinaryData("null"),
            messageId: "test-message-id"
        );

        // Act
        await _function.Run(message, _messageActionsMock.Object);

        // Assert
        _messageActionsMock.Verify(
            x => x.DeadLetterMessageAsync(It.IsAny<ServiceBusReceivedMessage>(), null, null, null, CancellationToken.None),
            Times.Once);
    }
}

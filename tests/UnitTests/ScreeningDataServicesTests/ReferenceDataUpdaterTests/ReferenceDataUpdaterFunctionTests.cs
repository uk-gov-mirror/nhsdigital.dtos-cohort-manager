namespace NHS.CohortManager.Tests.UnitTests.ScreeningDataServicesTests;

using System.Text.Json;
using Azure.Messaging.ServiceBus;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Moq;
using Model;
using ReferenceDataUpdater;

[TestClass]
public class ReferenceDataUpdaterFunctionTests
{
    private readonly Mock<IReferenceDataInsertHandler> _insertHandlerMock = new();
    private readonly Mock<ILogger<ReferenceDataUpdaterFunction>> _loggerMock = new();
    private readonly Mock<ServiceBusMessageActions> _messageActionsMock = new();
    private readonly ReferenceDataUpdaterFunction _function;

    private readonly ReferenceDataUpdateMessage _validUpdateMessage = new()
    {
        DataType = "BsSelectGpPractice",
        Data = JsonSerializer.SerializeToElement(new { GpPracticeCode = "Y12345" }),
        CorrelationId = "test-correlation-id",
        Timestamp = DateTime.UtcNow
    };

    public ReferenceDataUpdaterFunctionTests()
    {
        _function = new ReferenceDataUpdaterFunction(_insertHandlerMock.Object, _loggerMock.Object);
    }

    private static ServiceBusReceivedMessage CreateMessage(object body)
    {
        return ServiceBusModelFactory.ServiceBusReceivedMessage(
            body: new BinaryData(JsonSerializer.Serialize(body)),
            messageId: "test-message-id",
            correlationId: "test-correlation-id"
        );
    }

    private static ServiceBusReceivedMessage CreateMessage(string rawBody)
    {
        return ServiceBusModelFactory.ServiceBusReceivedMessage(
            body: new BinaryData(rawBody),
            messageId: "test-message-id",
            correlationId: "test-correlation-id"
        );
    }

    private void VerifyMessageCompleted()
    {
        _messageActionsMock.Verify(
            x => x.CompleteMessageAsync(It.IsAny<ServiceBusReceivedMessage>(), CancellationToken.None),
            Times.Once);
        _messageActionsMock.Verify(
            x => x.DeadLetterMessageAsync(It.IsAny<ServiceBusReceivedMessage>(), null, null, null, CancellationToken.None),
            Times.Never);
    }

    private void VerifyMessageDeadLettered()
    {
        _messageActionsMock.Verify(
            x => x.DeadLetterMessageAsync(It.IsAny<ServiceBusReceivedMessage>(), null, null, null, CancellationToken.None),
            Times.Once);
        _messageActionsMock.Verify(
            x => x.CompleteMessageAsync(It.IsAny<ServiceBusReceivedMessage>(), CancellationToken.None),
            Times.Never);
    }

    [TestMethod]
    public async Task Run_ProcessRecordSucceeds_CompletesMessage()
    {
        // Arrange
        var message = CreateMessage(_validUpdateMessage);
        _insertHandlerMock.Setup(h => h.ProcessRecord(_validUpdateMessage.DataType, It.IsAny<JsonElement>()))
            .ReturnsAsync(true);

        // Act
        await _function.Run(message, _messageActionsMock.Object);

        // Assert
        VerifyMessageCompleted();
    }

    [TestMethod]
    public async Task Run_ProcessRecordReturnsFalse_DeadLettersMessage()
    {
        // Arrange
        var message = CreateMessage(_validUpdateMessage);
        _insertHandlerMock.Setup(h => h.ProcessRecord(_validUpdateMessage.DataType, It.IsAny<JsonElement>()))
            .ReturnsAsync(false);

        // Act
        await _function.Run(message, _messageActionsMock.Object);

        // Assert
        VerifyMessageDeadLettered();
    }

    [TestMethod]
    public async Task Run_InvalidJsonBody_DeadLettersMessage()
    {
        // Arrange
        var message = CreateMessage("not valid json {{{");

        // Act
        await _function.Run(message, _messageActionsMock.Object);

        // Assert
        VerifyMessageDeadLettered();
    }

    [TestMethod]
    public async Task Run_NullDataType_DeadLettersMessage()
    {
        // Arrange
        var updateMessage = new ReferenceDataUpdateMessage
        {
            DataType = null!,
            Data = _validUpdateMessage.Data,
            CorrelationId = "test-correlation-id",
            Timestamp = DateTime.UtcNow
        };
        var message = CreateMessage(updateMessage);

        // Act
        await _function.Run(message, _messageActionsMock.Object);

        // Assert
        VerifyMessageDeadLettered();
        _insertHandlerMock.Verify(h => h.ProcessRecord(It.IsAny<string>(), It.IsAny<JsonElement>()), Times.Never);
    }

    [TestMethod]
    public async Task Run_WhitespaceDataType_DeadLettersMessage()
    {
        // Arrange
        var updateMessage = new ReferenceDataUpdateMessage
        {
            DataType = "   ",
            Data = _validUpdateMessage.Data,
            CorrelationId = "test-correlation-id",
            Timestamp = DateTime.UtcNow
        };
        var message = CreateMessage(updateMessage);

        // Act
        await _function.Run(message, _messageActionsMock.Object);

        // Assert
        VerifyMessageDeadLettered();
        _insertHandlerMock.Verify(h => h.ProcessRecord(It.IsAny<string>(), It.IsAny<JsonElement>()), Times.Never);
    }

    [TestMethod]
    public async Task Run_ProcessRecordThrowsException_DeadLettersMessage()
    {
        // Arrange
        var message = CreateMessage(_validUpdateMessage);
        _insertHandlerMock.Setup(h => h.ProcessRecord(_validUpdateMessage.DataType, It.IsAny<JsonElement>()))
            .ThrowsAsync(new InvalidOperationException("Something went wrong"));

        // Act
        await _function.Run(message, _messageActionsMock.Object);

        // Assert
        VerifyMessageDeadLettered();
    }

    [TestMethod]
    public async Task Run_NullMessageBody_DeadLettersMessage()
    {
        // Arrange
        var message = CreateMessage("null");

        // Act
        await _function.Run(message, _messageActionsMock.Object);

        // Assert
        _messageActionsMock.Verify(
            x => x.DeadLetterMessageAsync(It.IsAny<ServiceBusReceivedMessage>(), null, null, null, CancellationToken.None),
            Times.Once);
    }
}

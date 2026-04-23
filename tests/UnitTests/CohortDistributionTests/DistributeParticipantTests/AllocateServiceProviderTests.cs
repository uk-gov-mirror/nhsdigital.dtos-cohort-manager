namespace NHS.CohortManager.Tests.CohortDistributionServiceTests;

using DataServices.Client;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Model;
using Model.Enums;
using Moq;
using NHS.CohortManager.CohortDistributionServices;
using Common;

[TestClass]
public class AllocateServiceProviderTests
{
    private readonly DistributeParticipantActivities _distributionParticipantActivities;
    private readonly Mock<IAllocationConfigProvider> _allocationConfigProvider = new();
    private readonly Mock<IDataServiceClient<CohortDistribution>> _cohortDistributionClient = new();
    private readonly Mock<IDataServiceClient<ParticipantManagement>> _participantManagementClient = new();
    private readonly Mock<IDataServiceClient<ParticipantDemographic>> _participantDemographicClient = new();
    private readonly Mock<IHttpClientFunction> _httpClientFunction = new();
    private readonly Mock<IOptions<DistributeParticipantConfig>> _config = new();

    public AllocateServiceProviderTests()
    {
        DistributeParticipantConfig config = new()
        {
            LookupValidationURL = "LookupValidationURL",
            StaticValidationURL = "StaticValidationURL",
            TransformDataServiceURL = "TransformDataServiceURL",
            ParticipantManagementUrl = "ParticipantManagementUrl",
            CohortDistributionDataServiceUrl = "CohortDistributionDataServiceUrl",
            ParticipantDemographicDataServiceUrl = "ParticipantDemographicDataServiceUrl",
            CohortDistributionTopic = "cohort-distribution-topic",
            DistributeParticipantSubscription = "distribute-participant-sub",
            RemoveOldValidationRecordUrl = "RemoveOldValidationRecordUrl",
            SendServiceNowMessageURL = "SendServiceNowMessageURL"
        };

        _config.Setup(x => x.Value).Returns(config);

        _distributionParticipantActivities = new(
            _cohortDistributionClient.Object,
            _participantManagementClient.Object,
            _participantDemographicClient.Object,
            _config.Object,
            NullLogger<DistributeParticipantActivities>.Instance,
            _httpClientFunction.Object,
            _allocationConfigProvider.Object
        );
    }


    [TestMethod]
    public async Task AllocateServiceProvider_PostcodeIsNull_ReturnsBSSDefault()
    {
        // Arrange
        var participant = new CohortDistributionParticipant { Postcode = null, ScreeningAcronym = "BSS" };

        // Act
        var result = await _distributionParticipantActivities.AllocateServiceProvider(participant);

        // Assert
        Assert.AreEqual(EnumHelper.GetDisplayName(ServiceProvider.BSS), result);
    }

    [TestMethod]
    public async Task AllocateServiceProvider_PostcodeIsEmpty_ReturnsBSSDefault()
    {
        // Arrange
        var participant = new CohortDistributionParticipant { Postcode = "", ScreeningAcronym = "BSS" };

        // Act
        var result = await _distributionParticipantActivities.AllocateServiceProvider(participant);

        // Assert
        Assert.AreEqual(EnumHelper.GetDisplayName(ServiceProvider.BSS), result);
    }

    [TestMethod]
    public async Task AllocateServiceProvider_ScreeningAcronymIsNull_ReturnsBSSDefault()
    {
        // Arrange
        var participant = new CohortDistributionParticipant { Postcode = "AB1 2CD", ScreeningAcronym = null };

        // Act
        var result = await _distributionParticipantActivities.AllocateServiceProvider(participant);

        // Assert
        Assert.AreEqual(EnumHelper.GetDisplayName(ServiceProvider.BSS), result);
    }

    [TestMethod]
    public async Task AllocateServiceProvider_ScreeningAcronymIsEmpty_ReturnsBSSDefault()
    {
        // Arrange
        var participant = new CohortDistributionParticipant { Postcode = "AB1 2CD", ScreeningAcronym = "" };

        // Act
        var result = await _distributionParticipantActivities.AllocateServiceProvider(participant);

        // Assert
        Assert.AreEqual(EnumHelper.GetDisplayName(ServiceProvider.BSS), result);
    }

    [TestMethod]
    public async Task AllocateServiceProvider_MatchingPostcodeAndScreeningService_ReturnsConfiguredProvider()
    {
        // Arrange
        var participant = new CohortDistributionParticipant { Postcode = "AB1 2CD", ScreeningAcronym = "BSS" };
        var configData = new AllocationConfigDataList
        {
            ConfigDataList =
            [
                new AllocationConfigData { Postcode = "AB", ScreeningService = "BSS", ServiceProvider = "TestServiceProvider" }
            ]
        };
        _allocationConfigProvider.Setup(x => x.GetConfig()).Returns(configData);

        // Act
        var result = await _distributionParticipantActivities.AllocateServiceProvider(participant);

        // Assert
        Assert.AreEqual("TestServiceProvider", result);
    }

    [TestMethod]
    public async Task AllocateServiceProvider_NoMatchingPostcode_ReturnsBSSelect()
    {
        // Arrange
        var participant = new CohortDistributionParticipant { Postcode = "ZZ1 2CD", ScreeningAcronym = "BSS" };
        var configData = new AllocationConfigDataList
        {
            ConfigDataList =
            [
                new AllocationConfigData { Postcode = "AB", ScreeningService = "BSS", ServiceProvider = "TestServiceProvider" }
            ]
        };
        _allocationConfigProvider.Setup(x => x.GetConfig()).Returns(configData);

        // Act
        var result = await _distributionParticipantActivities.AllocateServiceProvider(participant);

        // Assert
        Assert.AreEqual("BS SELECT", result);
    }

    [TestMethod]
    public async Task AllocateServiceProvider_NoMatchingScreeningService_ReturnsBSSelect()
    {
        // Arrange
        var participant = new CohortDistributionParticipant { Postcode = "AB1 2CD", ScreeningAcronym = "CSS" };
        var configData = new AllocationConfigDataList
        {
            ConfigDataList =
            [
                new AllocationConfigData { Postcode = "AB", ScreeningService = "BSS", ServiceProvider = "TestServiceProvider" }
            ]
        };
        _allocationConfigProvider.Setup(x => x.GetConfig()).Returns(configData);

        // Act
        var result = await _distributionParticipantActivities.AllocateServiceProvider(participant);

        // Assert
        Assert.AreEqual("BS SELECT", result);
    }

    [TestMethod]
    public async Task AllocateServiceProvider_MultipleMatches_ReturnsLongestPostcodeMatch()
    {
        // Arrange
        var participant = new CohortDistributionParticipant { Postcode = "AB1 2CD", ScreeningAcronym = "BSS" };
        var configData = new AllocationConfigDataList
        {
            ConfigDataList =
            [
                new AllocationConfigData { Postcode = "A", ScreeningService = "BSS", ServiceProvider = "Short Match" },
                new AllocationConfigData { Postcode = "AB", ScreeningService = "BSS", ServiceProvider = "Longer Match" },
                new AllocationConfigData { Postcode = "AB1", ScreeningService = "BSS", ServiceProvider = "Longest Match" }
            ]
        };
        _allocationConfigProvider.Setup(x => x.GetConfig()).Returns(configData);

        // Act
        var result = await _distributionParticipantActivities.AllocateServiceProvider(participant);

        // Assert
        Assert.AreEqual("Longest Match", result);
    }

    [TestMethod]
    public async Task AllocateServiceProvider_PostcodeMatchIsCaseInsensitive_ReturnsMatch()
    {
        // Arrange
        var participant = new CohortDistributionParticipant { Postcode = "ab1 2cd", ScreeningAcronym = "BSS" };
        var configData = new AllocationConfigDataList
        {
            ConfigDataList =
            [
                new AllocationConfigData { Postcode = "AB", ScreeningService = "BSS", ServiceProvider = "TestServiceProvider" }
            ]
        };
        _allocationConfigProvider.Setup(x => x.GetConfig()).Returns(configData);

        // Act
        var result = await _distributionParticipantActivities.AllocateServiceProvider(participant);

        // Assert
        Assert.AreEqual("TestServiceProvider", result);
    }

    [TestMethod]
    public async Task AllocateServiceProvider_ScreeningAcronymMatchIsCaseInsensitive_ReturnsMatch()
    {
        // Arrange
        var participant = new CohortDistributionParticipant { Postcode = "AB1 2CD", ScreeningAcronym = "bss" };
        var configData = new AllocationConfigDataList
        {
            ConfigDataList =
            [
                new AllocationConfigData { Postcode = "AB", ScreeningService = "BSS", ServiceProvider = "TestServiceProvider" }
            ]
        };
        _allocationConfigProvider.Setup(x => x.GetConfig()).Returns(configData);

        // Act
        var result = await _distributionParticipantActivities.AllocateServiceProvider(participant);

        // Assert
        Assert.AreEqual("TestServiceProvider", result);
    }

    [TestMethod]
    public async Task AllocateServiceProvider_EmptyConfigDataList_ReturnsBSSelect()
    {
        // Arrange
        var participant = new CohortDistributionParticipant { Postcode = "AB1 2CD", ScreeningAcronym = "BSS" };
        var configData = new AllocationConfigDataList { ConfigDataList = [] };
        _allocationConfigProvider.Setup(x => x.GetConfig()).Returns(configData);

        // Act
        var result = await _distributionParticipantActivities.AllocateServiceProvider(participant);

        // Assert
        Assert.AreEqual("BS SELECT", result);
    }
}

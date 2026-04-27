namespace NHS.CohortManager.Tests.UnitTests.AuthenticationTests;

using System.Text.Json;
using Common;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Middleware;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

[TestClass]
public class AuthenticationTests
{
    [TestMethod]
    public async Task Cis2AuthMiddleware_BypassAuthentication_CallsNext()
    {
        // Arrange
        var logger = new Mock<ILogger<Cis2AuthMiddleware>>();
        var createResponse = new Mock<ICreateResponse>();
        var authService = new Mock<IAuthenticationService>();
        var cis2UserService = new Mock<ICis2UserService>();
        var options = Options.Create(new AuthConfig
        {
            AuthMetaDataUrl = "https://example.com/.well-known/openid-configuration",
            AuthClientId = "test-client",
            UserInfoUrl = "https://example.com/userinfo",
            ByPassAuthentication = true
        });

        var sut = new Cis2AuthMiddleware(logger.Object, createResponse.Object, authService.Object, cis2UserService.Object, options);
        var context = new Mock<FunctionContext>();
        var nextCalled = false;
        FunctionExecutionDelegate next = _ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        };

        // Act
        await sut.Invoke(context.Object, next);

        // Assert
        Assert.IsTrue(nextCalled);
    }

    [TestMethod]
    public async Task PermissionsMiddleware_BypassAuthentication_CallsNext()
    {
        // Arrange
        var createResponse = new Mock<ICreateResponse>();
        var roleManager = new Mock<IRoleManager>();
        var logger = new Mock<ILogger<PermissionsMiddleware>>();
        var options = Options.Create(new AuthConfig
        {
            AuthMetaDataUrl = "https://example.com/.well-known/openid-configuration",
            AuthClientId = "test-client",
            UserInfoUrl = "https://example.com/userinfo",
            ByPassAuthentication = true
        });

        var sut = new PermissionsMiddleware(createResponse.Object, roleManager.Object, logger.Object, options);
        var context = new Mock<FunctionContext>();
        var nextCalled = false;
        FunctionExecutionDelegate next = _ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        };

        // Act
        await sut.Invoke(context.Object, next);

        // Assert
        Assert.IsTrue(nextCalled);
    }

    [TestMethod]
    public void AuthHelper_WithInvalidHeaders_ReturnsFalse()
    {
        // Arrange
        var context = BuildFunctionContextWithHeaders(123);

        // Act
        var result = AuthHelper.TryGetIdTokenFromHeaders(context.Object, out _);

        // Assert
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void AuthHelper_WithValidHeaders_ReturnsTrueAndExtractsTokens()
    {
        // Arrange
        var headersJson = JsonSerializer.Serialize(new Dictionary<string, string>
        {
            ["Authorization"] = "Bearer id-token-value",
            ["X-Access-Token"] = "Bearer access-token-value"
        });
        var context = BuildFunctionContextWithHeaders(headersJson);

        // Act
        var idTokenResult = AuthHelper.TryGetIdTokenFromHeaders(context.Object, out var idToken);
        var accessTokenResult = AuthHelper.TryGetAccessTokenFromHeaders(context.Object, out var accessToken);

        // Assert
        Assert.IsTrue(idTokenResult);
        Assert.AreEqual("id-token-value", idToken);
        Assert.IsTrue(accessTokenResult);
        Assert.AreEqual("access-token-value", accessToken);
    }

    [TestMethod]
    public async Task Cis2UserService_WithValidResponse_ReturnsUser()
    {
        // Arrange
        var logger = new Mock<ILogger<Cis2UserService>>();
        var httpClient = new Mock<IHttpClientFunction>();
        var options = Options.Create(new AuthConfig
        {
            AuthMetaDataUrl = "https://example.com/.well-known/openid-configuration",
            AuthClientId = "test-client",
            UserInfoUrl = "https://example.com/userinfo",
            ByPassAuthentication = false
        });

        var responseJson = JsonSerializer.Serialize(new
        {
            nhsid_useruid = "u1",
            name = "Test User",
            nhsid_nrbac_roles = new[]
            {
                new
                {
                    person_orgid = "org",
                    person_roleid = "role",
                    org_code = "org-code",
                    role_name = "role-name",
                    role_code = "role-code",
                    workgroups = new[] { "wg" },
                    workgroups_codes = new[] { "wg-code" }
                }
            },
            given_name = "Test",
            family_name = "User",
            uid = "uid-1",
            sub = "sub-1"
        });

        httpClient.Setup(x => x.SendGetOrThrowAsync(options.Value.UserInfoUrl)).ReturnsAsync(responseJson);

        var sut = new Cis2UserService(logger.Object, httpClient.Object, options);

        // Act
        var result = await sut.GetUserFromToken("access-token");

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual("uid-1", result.Uid);
        httpClient.Verify(x => x.SetBearerToken("access-token"), Times.Once);
        httpClient.Verify(x => x.SendGetOrThrowAsync(options.Value.UserInfoUrl), Times.Once);
    }

    [TestMethod]
    public async Task Cis2UserService_WhenClientThrows_ReturnsNull()
    {
        // Arrange
        var logger = new Mock<ILogger<Cis2UserService>>();
        var httpClient = new Mock<IHttpClientFunction>();
        var options = Options.Create(new AuthConfig
        {
            AuthMetaDataUrl = "https://example.com/.well-known/openid-configuration",
            AuthClientId = "test-client",
            UserInfoUrl = "https://example.com/userinfo",
            ByPassAuthentication = false
        });

        httpClient.Setup(x => x.SendGetOrThrowAsync(options.Value.UserInfoUrl)).ThrowsAsync(new Exception("boom"));

        var sut = new Cis2UserService(logger.Object, httpClient.Object, options);

        // Act
        var result = await sut.GetUserFromToken("access-token");

        // Assert
        Assert.IsNull(result);
    }

    [TestMethod]
    public void RoleManager_WithMatchingWorkgroup_ReturnsTrue()
    {
        // Arrange
        const string expectedWorkgroup = "wg-user";
        var config = Options.Create(new RoleConfig
        {
            CohortManagerUserWorkgroupId = expectedWorkgroup,
            CohortManagerDummyGpRemovalWorkgroupId = "wg-dummy"
        });

        var user = CreateUserWithWorkgroupCodes(expectedWorkgroup);
        var sut = new RoleManager(config);

        // Act
        var result = sut.ValidateRole(user, Role.CohortManagerUser);

        // Assert
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void RoleManager_WithNonMatchingWorkgroup_ReturnsFalse()
    {
        // Arrange
        var config = Options.Create(new RoleConfig
        {
            CohortManagerUserWorkgroupId = "wg-user",
            CohortManagerDummyGpRemovalWorkgroupId = "wg-dummy"
        });

        var user = CreateUserWithWorkgroupCodes("different-workgroup");
        var sut = new RoleManager(config);

        // Act
        var result = sut.ValidateRole(user, Role.CohortManagerUser);

        // Assert
        Assert.IsFalse(result);
    }

    [DataTestMethod]
    [DataRow("")]
    [DataRow("not-a-jwt")]
    public async Task JwtAuthentication_WithInvalidToken_ReturnsFalse(string token)
    {
        // Arrange
        var logger = new Mock<ILogger<JwtAuthentication>>();
        var options = Options.Create(new AuthConfig
        {
            AuthMetaDataUrl = "https://example.com/.well-known/openid-configuration",
            AuthClientId = "test-client",
            UserInfoUrl = "https://example.com/userinfo",
            ByPassAuthentication = false
        });
        var sut = new JwtAuthentication(options, logger.Object);

        // Act
        var result = await sut.ValidateTokenAsync(token);

        // Assert
        Assert.IsFalse(result);
    }

    private static Mock<FunctionContext> BuildFunctionContextWithHeaders(object? headers)
    {
        var bindingData = new Dictionary<string, object?>();
        if (headers != null)
        {
            bindingData["Headers"] = headers;
        }

        var bindingContext = new Mock<BindingContext>();
        bindingContext.SetupGet(x => x.BindingData).Returns(bindingData);

        var context = new Mock<FunctionContext>();
        context.SetupGet(x => x.BindingContext).Returns(bindingContext.Object);
        return context;
    }

    private static Cis2User CreateUserWithWorkgroupCodes(params string[] workgroupCodes)
    {
        return new Cis2User
        {
            NhsidUseruid = "user-uid",
            Name = "Test User",
            GivenName = "Test",
            FamilyName = "User",
            Uid = "uid-1",
            Sub = "sub-1",
            NhsidNrbacRoles =
            [
                new NhsidNrbacRole
                {
                    PersonOrgid = "org",
                    PersonRoleid = "role",
                    OrgCode = "org-code",
                    RoleName = "role-name",
                    RoleCode = "role-code",
                    Workgroups = ["wg-name"],
                    WorkgroupsCodes = workgroupCodes.ToList()
                }
            ]
        };
    }
}

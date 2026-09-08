using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using CMS.API.Models;
using CMS.API.Security;
using Xunit;

namespace CMS.API.Tests;

public class AppUsersControllerTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private static readonly string DefaultPasswordHash =
        PasswordHasher.Sha256Hex(FakeSysConfigRepository.DefaultPassword);

    // ---------- Hashing contract ----------

    /// <summary>The format the one live row uses: SHA-256, 64 lowercase hex characters.</summary>
    [Fact]
    public void Sha256Hex_ProducesLowercaseHexOfUtf8Bytes()
    {
        var hash = PasswordHasher.Sha256Hex("abc");

        Assert.Equal("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad", hash);
        Assert.Equal(64, hash.Length);
    }

    // ---------- List / filter ----------

    [Fact]
    public async Task GetAll_ReturnsAllUsers_OrderedByUserId()
    {
        using var factory = new AppUserApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/app-users");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var users = await ReadUsersAsync(response);
        Assert.Equal(["helen", "Jenny_Tsao", "miles@uuu.com.tw"], users.Select(u => u.UserId));
    }

    [Fact]
    public async Task GetAll_IncludesRoleCount_AndNoRoleIds()
    {
        using var factory = new AppUserApiFactory();
        using var client = factory.CreateClient();

        var users = await ReadUsersAsync(await client.GetAsync("/api/app-users"));

        Assert.Equal(2, users.Single(u => u.UserId == "miles@uuu.com.tw").RoleCount);
        Assert.Equal(1, users.Single(u => u.UserId == "helen").RoleCount);
        Assert.Equal(0, users.Single(u => u.UserId == "Jenny_Tsao").RoleCount);
        Assert.All(users, u => Assert.Empty(u.RoleIds));
    }

    /// <summary>The contract: PasswordHash never crosses the API. Assert on the raw JSON.</summary>
    [Fact]
    public async Task GetAll_NeverSerializesPasswordHash()
    {
        using var factory = new AppUserApiFactory();
        using var client = factory.CreateClient();

        var json = await client.GetStringAsync("/api/app-users");

        using var document = JsonDocument.Parse(json);
        foreach (var element in document.RootElement.EnumerateArray())
        {
            foreach (var property in element.EnumerateObject())
            {
                // PasswordUpdatedTime is the one legitimate "password" property.
                if (property.Name.Equals("passwordUpdatedTime", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                Assert.DoesNotContain("password", property.Name, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("hash", property.Name, StringComparison.OrdinalIgnoreCase);
            }
        }
    }

    [Fact]
    public async Task GetAll_SerializesPasswordUpdatedTime_NullWhenUnset()
    {
        using var factory = new AppUserApiFactory();
        using var client = factory.CreateClient();

        var users = await ReadUsersAsync(await client.GetAsync("/api/app-users"));

        Assert.Equal(new DateTime(2026, 3, 1, 9, 30, 0), users.Single(u => u.UserId == "helen").PasswordUpdatedTime);
        Assert.Null(users.Single(u => u.UserId == "miles@uuu.com.tw").PasswordUpdatedTime);
    }

    [Fact]
    public async Task Query_WithKeyword_MatchesUserId()
    {
        using var factory = new AppUserApiFactory();
        using var client = factory.CreateClient();

        var users = await QueryAsync(client, new AppUserQuery { Keyword = "uuu.com" });

        Assert.Equal("miles@uuu.com.tw", Assert.Single(users).UserId);
    }

    /// <summary>The keyword spans UserName too, not just the login id.</summary>
    [Fact]
    public async Task Query_WithKeyword_MatchesUserName()
    {
        using var factory = new AppUserApiFactory();
        using var client = factory.CreateClient();

        var users = await QueryAsync(client, new AppUserQuery { Keyword = "Miles" });

        Assert.Equal("miles@uuu.com.tw", Assert.Single(users).UserId);
    }

    [Fact]
    public async Task Query_WithIsActiveFalse_ReturnsOnlyInactiveUsers()
    {
        using var factory = new AppUserApiFactory();
        using var client = factory.CreateClient();

        var users = await QueryAsync(client, new AppUserQuery { IsActive = false });

        Assert.Equal("helen", Assert.Single(users).UserId);
    }

    [Fact]
    public async Task Query_WithIsActiveNull_DoesNotFilter()
    {
        using var factory = new AppUserApiFactory();
        using var client = factory.CreateClient();

        var users = await QueryAsync(client, new AppUserQuery { IsActive = null });

        Assert.Equal(3, users.Count);
    }

    [Fact]
    public async Task Query_WithRoleId_ReturnsUsersHoldingThatRole()
    {
        using var factory = new AppUserApiFactory();
        using var client = factory.CreateClient();

        var admins = await QueryAsync(client, new AppUserQuery { RoleId = "Admin" });
        var plainUsers = await QueryAsync(client, new AppUserQuery { RoleId = "User" });

        Assert.Equal("miles@uuu.com.tw", Assert.Single(admins).UserId);
        Assert.Equal(["helen", "miles@uuu.com.tw"], plainUsers.Select(u => u.UserId));
    }

    [Fact]
    public async Task Query_WithPasswordUpdatedRange_IsInclusiveOfTheToDay()
    {
        using var factory = new AppUserApiFactory();
        using var client = factory.CreateClient();

        // helen's time is 2026-03-01 09:30; a To bound of the same date must still include it.
        var hit = await QueryAsync(client, new AppUserQuery
        {
            PasswordUpdatedFrom = new DateOnly(2026, 3, 1),
            PasswordUpdatedTo = new DateOnly(2026, 3, 1)
        });
        var miss = await QueryAsync(client, new AppUserQuery
        {
            PasswordUpdatedFrom = new DateOnly(2026, 3, 2)
        });

        Assert.Equal("helen", Assert.Single(hit).UserId);
        Assert.Empty(miss);
    }

    [Fact]
    public async Task Query_WithEmptyFilter_ReturnsAllUsers()
    {
        using var factory = new AppUserApiFactory();
        using var client = factory.CreateClient();

        var users = await QueryAsync(client, new AppUserQuery());

        Assert.Equal(3, users.Count);
    }

    [Fact]
    public async Task Query_WithNoMatch_ReturnsEmptyList()
    {
        using var factory = new AppUserApiFactory();
        using var client = factory.CreateClient();

        var users = await QueryAsync(client, new AppUserQuery { Keyword = "no-such-user" });

        Assert.Empty(users);
    }

    // ---------- View ----------

    [Fact]
    public async Task GetById_ReturnsTheUserWithRoleIds()
    {
        using var factory = new AppUserApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/app-users/1");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var user = await response.Content.ReadFromJsonAsync<AppUser>(JsonOptions);
        Assert.NotNull(user);
        Assert.Equal(1, user.Pkid);
        Assert.Equal("miles@uuu.com.tw", user.UserId);
        Assert.Equal("Miles Sun", user.UserName);
        Assert.True(user.IsActive);
        Assert.Null(user.PasswordUpdatedTime);
        Assert.Equal(2, user.RoleCount);
        Assert.Equal(["Admin", "User"], user.RoleIds);
    }

    [Fact]
    public async Task GetById_UnknownPkid_Returns404()
    {
        using var factory = new AppUserApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/app-users/999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ---------- Add ----------

    [Fact]
    public async Task Create_AssignsAnIdentityPkid_AndReturnsCreated()
    {
        using var factory = new AppUserApiFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/app-users", Request("bob", "Bob Chen"), JsonOptions);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<AppUser>(JsonOptions);
        Assert.NotNull(created);
        Assert.Equal(4, created.Pkid);
        Assert.Equal("bob", created.UserId);
        Assert.Equal("Bob Chen", created.UserName);
        Assert.True(created.IsActive);
        Assert.Equal($"/api/app-users/{created.Pkid}", response.Headers.Location?.AbsolutePath);
    }

    /// <summary>The password rule: SHA-256 of SysConfig's defaultPassword, and PasswordUpdatedTime NULL.</summary>
    [Fact]
    public async Task Create_StoresTheHashedDefaultPassword_AndNullUpdatedTime()
    {
        using var factory = new AppUserApiFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/app-users", Request("bob", "Bob Chen"), JsonOptions);
        var created = await response.Content.ReadFromJsonAsync<AppUser>(JsonOptions);

        Assert.Equal(DefaultPasswordHash, factory.Repository.PasswordHashOf(created!.Pkid));
        Assert.Null(created.PasswordUpdatedTime);
    }

    [Fact]
    public async Task Create_IgnoresAnyPkidSuppliedInTheBody()
    {
        using var factory = new AppUserApiFactory();
        using var client = factory.CreateClient();

        var request = Request("bob", "Bob Chen");
        request.Pkid = 99;

        var response = await client.PostAsJsonAsync("/api/app-users", request, JsonOptions);

        var created = await response.Content.ReadFromJsonAsync<AppUser>(JsonOptions);
        Assert.Equal(4, created!.Pkid);
    }

    /// <summary>A password in the body is not a model property; the JSON binder ignores it and nothing uses it.</summary>
    [Fact]
    public async Task Create_IgnoresAPasswordSuppliedInTheBody()
    {
        using var factory = new AppUserApiFactory();
        using var client = factory.CreateClient();

        var body = new StringContent(
            """{"userId":"bob","userName":"Bob Chen","isActive":true,"roleIds":[],"password":"hunter2","passwordHash":"deadbeef"}""",
            Encoding.UTF8,
            "application/json");

        var response = await client.PostAsync("/api/app-users", body);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<AppUser>(JsonOptions);
        Assert.Equal(DefaultPasswordHash, factory.Repository.PasswordHashOf(created!.Pkid));
    }

    [Fact]
    public async Task Create_SyncsRoles_DedupedCaseInsensitively()
    {
        using var factory = new AppUserApiFactory();
        using var client = factory.CreateClient();

        var request = Request("bob", "Bob Chen");
        request.RoleIds = ["User", " user ", "Admin", ""];

        var response = await client.PostAsJsonAsync("/api/app-users", request, JsonOptions);

        var created = await response.Content.ReadFromJsonAsync<AppUser>(JsonOptions);
        Assert.Equal(["Admin", "User"], created!.RoleIds);
        Assert.Equal(2, created.RoleCount);
    }

    [Fact]
    public async Task Create_WithIsActiveFalse_StoresFalse()
    {
        using var factory = new AppUserApiFactory();
        using var client = factory.CreateClient();

        var request = Request("bob", "Bob Chen");
        request.IsActive = false;

        var created = await (await client.PostAsJsonAsync("/api/app-users", request, JsonOptions))
            .Content.ReadFromJsonAsync<AppUser>(JsonOptions);

        Assert.False(created!.IsActive);
    }

    /// <summary>UserId is the clustered PK under a CI collation — a different-case duplicate still collides.</summary>
    [Fact]
    public async Task Create_WithAUserIdThatAlreadyExists_Returns409_CaseInsensitively()
    {
        using var factory = new AppUserApiFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/app-users", Request("HELEN", "Someone Else"), JsonOptions);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetailsDto>(JsonOptions);
        Assert.Equal("帳號重複", problem?.Title);

        Assert.Equal(3, (await ReadUsersAsync(await client.GetAsync("/api/app-users"))).Count);
    }

    /// <summary>UserName carries no uniqueness rule — the seed already holds two "Helen"s.</summary>
    [Fact]
    public async Task Create_WithAUserNameThatAlreadyExists_Returns201NotConflict()
    {
        using var factory = new AppUserApiFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/app-users", Request("helen2", "Helen"), JsonOptions);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var users = await ReadUsersAsync(await client.GetAsync("/api/app-users"));
        Assert.Equal(3, users.Count(u => u.UserName == "Helen"));
    }

    [Theory]
    [InlineData("", "Bob Chen")]
    [InlineData("   ", "Bob Chen")]
    [InlineData("bob", "")]
    public async Task Create_WithABlankRequiredField_Returns400(string userId, string userName)
    {
        using var factory = new AppUserApiFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/app-users", Request(userId, userName), JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_WithAnOverlongUserId_Returns400()
    {
        using var factory = new AppUserApiFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/app-users", Request(new string('x', 201), "Bob Chen"), JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>A missing default password is a deployment fault — the request must not create a half-configured account.</summary>
    [Fact]
    public async Task Create_WhenDefaultPasswordIsMissing_Returns500AndCreatesNothing()
    {
        using var factory = new AppUserApiFactory();
        factory.SysConfig.Config = null;
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/app-users", Request("bob", "Bob Chen"), JsonOptions);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal(3, (await ReadUsersAsync(await client.GetAsync("/api/app-users"))).Count);
    }

    // ---------- Edit ----------

    [Fact]
    public async Task Update_ChangesUserNameIsActiveAndRoles_AndReturnsTheUpdatedRow()
    {
        using var factory = new AppUserApiFactory();
        using var client = factory.CreateClient();

        var response = await client.PutAsJsonAsync("/api/app-users", new AppUserRequest
        {
            Pkid = 3,
            UserId = "Jenny_Tsao",
            UserName = "Jenny Tsao",
            IsActive = false,
            RoleIds = ["User"]
        }, JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content.ReadFromJsonAsync<AppUser>(JsonOptions);
        Assert.NotNull(updated);
        Assert.Equal(3, updated.Pkid);
        Assert.Equal("Jenny Tsao", updated.UserName);
        Assert.False(updated.IsActive);
        Assert.Equal(["User"], updated.RoleIds);
        Assert.Equal(1, updated.RoleCount);
    }

    /// <summary>UserId is immutable: a changed value in the body is ignored, not applied and not a 409.</summary>
    [Fact]
    public async Task Update_WithAChangedUserId_KeepsTheOriginal()
    {
        using var factory = new AppUserApiFactory();
        using var client = factory.CreateClient();

        var request = Request("renamed@example.com", "Miles Sun");
        request.Pkid = 1;
        request.RoleIds = ["Admin", "User"];

        var response = await client.PutAsJsonAsync("/api/app-users", request, JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content.ReadFromJsonAsync<AppUser>(JsonOptions);
        Assert.Equal("miles@uuu.com.tw", updated!.UserId);
    }

    /// <summary>Even a body carrying another row's UserId is fine — the column is never written.</summary>
    [Fact]
    public async Task Update_WithAnotherRowsUserId_Returns200NotConflict()
    {
        using var factory = new AppUserApiFactory();
        using var client = factory.CreateClient();

        var request = Request("helen", "Miles Sun");
        request.Pkid = 1;

        var response = await client.PutAsJsonAsync("/api/app-users", request, JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var users = await ReadUsersAsync(await client.GetAsync("/api/app-users"));
        Assert.Equal(["helen", "Jenny_Tsao", "miles@uuu.com.tw"], users.Select(u => u.UserId));
    }

    [Fact]
    public async Task Update_DoesNotTouchThePasswordHashOrUpdatedTime()
    {
        using var factory = new AppUserApiFactory();
        using var client = factory.CreateClient();
        var hashBefore = factory.Repository.PasswordHashOf(2);

        var request = Request("helen", "Helen Renamed");
        request.Pkid = 2;

        var response = await client.PutAsJsonAsync("/api/app-users", request, JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(hashBefore, factory.Repository.PasswordHashOf(2));
        var updated = await response.Content.ReadFromJsonAsync<AppUser>(JsonOptions);
        Assert.Equal(new DateTime(2026, 3, 1, 9, 30, 0), updated!.PasswordUpdatedTime);
    }

    [Fact]
    public async Task Update_WithEmptyRoles_RemovesEveryJunctionRow()
    {
        using var factory = new AppUserApiFactory();
        using var client = factory.CreateClient();

        var request = Request("miles@uuu.com.tw", "Miles Sun");
        request.Pkid = 1;
        request.RoleIds = [];

        var updated = await (await client.PutAsJsonAsync("/api/app-users", request, JsonOptions))
            .Content.ReadFromJsonAsync<AppUser>(JsonOptions);

        Assert.Empty(updated!.RoleIds);
        Assert.Equal(0, updated.RoleCount);
    }

    [Fact]
    public async Task Update_WithoutPkid_Returns400AndDoesNotTouchRepository()
    {
        using var factory = new AppUserApiFactory();
        using var client = factory.CreateClient();

        var request = Request("nobody", "No Pkid");
        request.Pkid = 0;

        var response = await client.PutAsJsonAsync("/api/app-users", request, JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, factory.Repository.UpdateCallCount);
    }

    [Fact]
    public async Task Update_UnknownPkid_Returns404()
    {
        using var factory = new AppUserApiFactory();
        using var client = factory.CreateClient();

        var request = Request("ghost", "Ghost");
        request.Pkid = 999;

        var response = await client.PutAsJsonAsync("/api/app-users", request, JsonOptions);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Update_WithBlankUserName_Returns400()
    {
        using var factory = new AppUserApiFactory();
        using var client = factory.CreateClient();

        var request = Request("helen", string.Empty);
        request.Pkid = 2;

        var response = await client.PutAsJsonAsync("/api/app-users", request, JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ---------- Delete ----------

    [Fact]
    public async Task Delete_Returns204_ThenGetReturns404_AndJunctionRowsAreGone()
    {
        using var factory = new AppUserApiFactory();
        using var client = factory.CreateClient();

        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync("/api/app-users/1")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/app-users/1")).StatusCode);
        Assert.Equal(2, (await ReadUsersAsync(await client.GetAsync("/api/app-users"))).Count);
        Assert.Empty(factory.Repository.RoleIdsOf("miles@uuu.com.tw"));
    }

    [Fact]
    public async Task Delete_UnknownPkid_Returns404()
    {
        using var factory = new AppUserApiFactory();
        using var client = factory.CreateClient();

        var response = await client.DeleteAsync("/api/app-users/999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ---------- Reset password ----------

    [Fact]
    public async Task ResetPassword_StoresTheDefaultHash_ClearsUpdatedTime_AndReturnsTheRow()
    {
        using var factory = new AppUserApiFactory();
        using var client = factory.CreateClient();
        Assert.NotEqual(DefaultPasswordHash, factory.Repository.PasswordHashOf(2));

        var response = await client.PostAsync("/api/app-users/2/reset-password", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var user = await response.Content.ReadFromJsonAsync<AppUser>(JsonOptions);
        Assert.NotNull(user);
        Assert.Equal(2, user.Pkid);
        Assert.Null(user.PasswordUpdatedTime);
        Assert.Equal(["User"], user.RoleIds);
        Assert.Equal(DefaultPasswordHash, factory.Repository.PasswordHashOf(2));
    }

    [Fact]
    public async Task ResetPassword_UnknownPkid_Returns404()
    {
        using var factory = new AppUserApiFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsync("/api/app-users/999/reset-password", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ResetPassword_WhenDefaultPasswordIsMissing_Returns500AndKeepsTheHash()
    {
        using var factory = new AppUserApiFactory();
        factory.SysConfig.Config = null;
        using var client = factory.CreateClient();
        var hashBefore = factory.Repository.PasswordHashOf(2);

        var response = await client.PostAsync("/api/app-users/2/reset-password", null);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal(hashBefore, factory.Repository.PasswordHashOf(2));
    }

    // ---------- Helpers ----------

    private static AppUserRequest Request(string userId, string userName) => new()
    {
        UserId = userId,
        UserName = userName,
        IsActive = true,
        RoleIds = []
    };

    private static async Task<List<AppUser>> QueryAsync(HttpClient client, AppUserQuery query)
    {
        var content = new StringContent(
            JsonSerializer.Serialize(query, JsonOptions), Encoding.UTF8, "application/json");
        var response = await client.PostAsync("/api/app-users/query", content);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await ReadUsersAsync(response);
    }

    private static async Task<List<AppUser>> ReadUsersAsync(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<List<AppUser>>(JsonOptions) ?? [];

    private sealed class ProblemDetailsDto
    {
        public string? Title { get; set; }

        public string? Detail { get; set; }
    }
}

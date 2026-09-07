using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using CMS.API.Models;
using Xunit;

namespace CMS.API.Tests;

public class AppRolesControllerTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    // ---------- List / filter ----------

    [Fact]
    public async Task GetAll_ReturnsAllRoles_OrderedByRoleId()
    {
        using var factory = new AppRoleApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/app-roles");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var roles = await ReadRolesAsync(response);
        Assert.Equal(["Admin", "User"], roles.Select(r => r.RoleId));
    }

    [Fact]
    public async Task GetAll_IncludesUserCountFromAppUserRole()
    {
        using var factory = new AppRoleApiFactory();
        using var client = factory.CreateClient();

        var roles = await ReadRolesAsync(await client.GetAsync("/api/app-roles"));

        Assert.Equal(3, roles.Single(r => r.RoleId == "Admin").UserCount);
        Assert.Equal(1, roles.Single(r => r.RoleId == "User").UserCount);
    }

    [Fact]
    public async Task Query_WithKeyword_MatchesRoleIdRoleNameAndDescription()
    {
        using var factory = new AppRoleApiFactory();
        using var client = factory.CreateClient();

        var byRoleId = await QueryAsync(client, new AppRoleQuery { Keyword = "adm" });
        var byDescription = await QueryAsync(client, new AppRoleQuery { Keyword = "一般" });

        Assert.Equal("Admin", Assert.Single(byRoleId).RoleId);
        Assert.Equal("User", Assert.Single(byDescription).RoleId);
    }

    [Fact]
    public async Task Query_WithPermissionLevelRange_FiltersInclusively()
    {
        using var factory = new AppRoleApiFactory();
        using var client = factory.CreateClient();

        var upTo50 = await QueryAsync(client, new AppRoleQuery { PermissionLevelTo = 50 });
        var exactly100 = await QueryAsync(client, new AppRoleQuery { PermissionLevelFrom = 100, PermissionLevelTo = 100 });

        Assert.Equal("Admin", Assert.Single(upTo50).RoleId);
        Assert.Equal("User", Assert.Single(exactly100).RoleId);
    }

    [Fact]
    public async Task Query_WithEmptyFilter_ReturnsAllRoles()
    {
        using var factory = new AppRoleApiFactory();
        using var client = factory.CreateClient();

        var roles = await QueryAsync(client, new AppRoleQuery());

        Assert.Equal(2, roles.Count);
    }

    [Fact]
    public async Task Query_WithNoMatch_ReturnsEmptyList()
    {
        using var factory = new AppRoleApiFactory();
        using var client = factory.CreateClient();

        var roles = await QueryAsync(client, new AppRoleQuery { Keyword = "no-such-role" });

        Assert.Empty(roles);
    }

    // ---------- View ----------

    [Fact]
    public async Task GetById_ReturnsRoleWithUserIds()
    {
        using var factory = new AppRoleApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/app-roles/1");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var role = await response.Content.ReadFromJsonAsync<AppRole>(JsonOptions);
        Assert.NotNull(role);
        Assert.Equal("Admin", role.RoleId);
        Assert.Equal("Administrator", role.RoleName);
        Assert.Equal(1, role.PermissionLevel);
        Assert.Equal("系統管理員", role.Description);
        Assert.Equal(["Jenny_Tsao", "helen", "miles@uuu.com.tw"], role.UserIds);
    }

    [Fact]
    public async Task GetById_UnknownPkid_Returns404()
    {
        using var factory = new AppRoleApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/app-roles/999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ---------- Add ----------

    [Fact]
    public async Task Create_ReturnsCreatedWithLocationAndPersistsRole()
    {
        using var factory = new AppRoleApiFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/app-roles", new AppRoleRequest
        {
            RoleId = "Editor",
            RoleName = "Content Editor",
            PermissionLevel = 50,
            Description = "內容編輯",
            UserIds = ["helen", "bob"]
        }, JsonOptions);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<AppRole>(JsonOptions);
        Assert.NotNull(created);
        Assert.Equal("Editor", created.RoleId);
        Assert.Equal(2, created.UserCount);
        Assert.Equal(["bob", "helen"], created.UserIds);
        Assert.Equal($"/api/app-roles/{created.Pkid}", response.Headers.Location?.AbsolutePath);

        var fetched = await client.GetFromJsonAsync<AppRole>($"/api/app-roles/{created.Pkid}", JsonOptions);
        Assert.Equal("Content Editor", fetched!.RoleName);
    }

    [Fact]
    public async Task Create_WithDuplicateRoleId_Returns409()
    {
        using var factory = new AppRoleApiFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/app-roles", new AppRoleRequest
        {
            RoleId = "Admin",
            RoleName = "Duplicate",
            PermissionLevel = 1
        }, JsonOptions);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(2, (await ReadRolesAsync(await client.GetAsync("/api/app-roles"))).Count);
    }

    [Theory]
    [InlineData("", "Name")]      // RoleId required
    [InlineData("Editor", "")]    // RoleName required
    public async Task Create_WithMissingRequiredField_Returns400(string roleId, string roleName)
    {
        using var factory = new AppRoleApiFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/app-roles", new AppRoleRequest
        {
            RoleId = roleId,
            RoleName = roleName,
            PermissionLevel = 100
        }, JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_WithNegativePermissionLevel_Returns400()
    {
        using var factory = new AppRoleApiFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/app-roles", new AppRoleRequest
        {
            RoleId = "Bad",
            RoleName = "Bad",
            PermissionLevel = -1
        }, JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ---------- Edit ----------

    [Fact]
    public async Task Update_ChangesScalarFieldsAndReturnsUpdatedRole()
    {
        using var factory = new AppRoleApiFactory();
        using var client = factory.CreateClient();

        var response = await client.PutAsJsonAsync("/api/app-roles", new AppRoleRequest
        {
            Pkid = 2,
            RoleId = "User",
            RoleName = "General User",
            PermissionLevel = 200,
            Description = "更新後的描述",
            UserIds = ["bob", "helen"]
        }, JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content.ReadFromJsonAsync<AppRole>(JsonOptions);
        Assert.NotNull(updated);
        Assert.Equal("General User", updated.RoleName);
        Assert.Equal(200, updated.PermissionLevel);
        Assert.Equal("更新後的描述", updated.Description);
        Assert.Equal(["bob", "helen"], updated.UserIds);
        Assert.Equal(2, updated.UserCount);
    }

    [Fact]
    public async Task Update_ReplacesUserAssignments()
    {
        using var factory = new AppRoleApiFactory();
        using var client = factory.CreateClient();

        await client.PutAsJsonAsync("/api/app-roles", new AppRoleRequest
        {
            Pkid = 1,
            RoleId = "Admin",
            RoleName = "Administrator",
            PermissionLevel = 1,
            Description = "系統管理員",
            UserIds = ["helen"]
        }, JsonOptions);

        var role = await client.GetFromJsonAsync<AppRole>("/api/app-roles/1", JsonOptions);
        Assert.Equal(["helen"], role!.UserIds);
        Assert.Equal(1, role.UserCount);
    }

    [Fact]
    public async Task Update_WithoutPkid_Returns400AndDoesNotTouchRepository()
    {
        using var factory = new AppRoleApiFactory();
        using var client = factory.CreateClient();

        var response = await client.PutAsJsonAsync("/api/app-roles", new AppRoleRequest
        {
            Pkid = 0,
            RoleId = "User",
            RoleName = "User",
            PermissionLevel = 100
        }, JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, factory.Repository.UpdateCallCount);
    }

    [Fact]
    public async Task Update_UnknownPkid_Returns404()
    {
        using var factory = new AppRoleApiFactory();
        using var client = factory.CreateClient();

        var response = await client.PutAsJsonAsync("/api/app-roles", new AppRoleRequest
        {
            Pkid = 999,
            RoleId = "Ghost",
            RoleName = "Ghost",
            PermissionLevel = 100
        }, JsonOptions);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Update_WithRoleIdOwnedByAnotherRole_Returns409()
    {
        using var factory = new AppRoleApiFactory();
        using var client = factory.CreateClient();

        var response = await client.PutAsJsonAsync("/api/app-roles", new AppRoleRequest
        {
            Pkid = 2,
            RoleId = "Admin",
            RoleName = "User",
            PermissionLevel = 100
        }, JsonOptions);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    // ---------- Delete ----------

    [Fact]
    public async Task Delete_RemovesRole_ThenReturns404()
    {
        using var factory = new AppRoleApiFactory();
        using var client = factory.CreateClient();

        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync("/api/app-roles/2")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.DeleteAsync("/api/app-roles/2")).StatusCode);
        Assert.Single(await ReadRolesAsync(await client.GetAsync("/api/app-roles")));
    }

    // ---------- Helpers ----------

    private static async Task<List<AppRole>> QueryAsync(HttpClient client, AppRoleQuery query)
    {
        var content = new StringContent(
            JsonSerializer.Serialize(query, JsonOptions), Encoding.UTF8, "application/json");
        var response = await client.PostAsync("/api/app-roles/query", content);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await ReadRolesAsync(response);
    }

    private static async Task<List<AppRole>> ReadRolesAsync(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<List<AppRole>>(JsonOptions) ?? [];
}

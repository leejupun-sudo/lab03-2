using System.Text.Json;
using CMS.API.Data;
using CMS.API.Models;
using Dapper;

namespace CMS.API.Repositories;

public class SysConfigRepository : ISysConfigRepository
{
    private const string AppConfigKey = "appConfig";

    // The database runs at compatibility level 100, so OPENJSON is unavailable — parse in C#.
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IDbConnectionFactory _connectionFactory;

    public SysConfigRepository(IDbConnectionFactory connectionFactory) => _connectionFactory = connectionFactory;

    public async Task<AppConfig?> GetAppConfigAsync(CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT c.configValue FROM SysConfig c WHERE c.configKey = @ConfigKey";

        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var json = await connection.QuerySingleOrDefaultAsync<string>(new CommandDefinition(
            sql, new { ConfigKey = AppConfigKey }, cancellationToken: cancellationToken));

        return string.IsNullOrWhiteSpace(json)
            ? null
            : JsonSerializer.Deserialize<AppConfig>(json, JsonOptions);
    }
}

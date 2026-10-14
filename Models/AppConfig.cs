namespace PostgresRedisManager.Models;

internal sealed class AppConfig
{
    public PostgreSqlConfig PostgreSQL { get; set; } = new();
    public RedisConfig Redis { get; set; } = new();
    public List<NodeAppConfig> NodeApps { get; set; } = [];
    public List<PhpAppConfig> PHPApps { get; set; } = [];

    public static AppConfig CreateDefault() => new()
    {
        PostgreSQL = new PostgreSqlConfig
        {
            PgCtlPath = @"D:\postgreSQL\bin\pg_ctl.exe",
            DataDirectory = @"D:\postgreSQL\data",
            Host = "127.0.0.1",
            Port = 5432
        },
        Redis = new RedisConfig
        {
            ServiceName = "Redis",
            ServerPath = @"D:\Software\Redis\redis-server.exe",
            CliPath = @"D:\Software\Redis\redis-cli.exe",
            Host = "127.0.0.1",
            Port = 6379
        }
    };
}

internal sealed class NodeAppConfig
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public string Directory { get; set; } = "";
    public string RunCommand { get; set; } = "pnpm run start";
}

internal sealed class PhpAppConfig
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public string Directory { get; set; } = "";
    public string RunCommand { get; set; } = "php spark serve";
}

internal sealed record AppProcessConfig(Guid Id, string Name, string Directory, string RunCommand);

internal sealed class PostgreSqlConfig
{
    public string PgCtlPath { get; set; } = "";
    public string DataDirectory { get; set; } = "";
    public string Host { get; set; } = "127.0.0.1";
    public int Port { get; set; } = 5432;
}

internal sealed class RedisConfig
{
    public string ServiceName { get; set; } = "Redis";
    public string ServerPath { get; set; } = "";
    public string CliPath { get; set; } = "";
    public string Host { get; set; } = "127.0.0.1";
    public int Port { get; set; } = 6379;
}

namespace Ledgr.Api.Data;

public sealed class DatabaseOptions
{
    public const string SectionName = "Database";

    public string ConnectionString { get; set; } = "Data Source=ledgr.db";

    /// <summary>Apply pending migrations during startup. Intended for development only.</summary>
    public bool MigrateOnStartup { get; set; }

    /// <summary>Insert the sample catalogue when the database is empty.</summary>
    public bool Seed { get; set; }
}

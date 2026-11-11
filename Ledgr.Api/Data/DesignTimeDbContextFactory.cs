using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Ledgr.Api.Data;

/// <summary>
/// Used by <c>dotnet ef</c> only. Keeping it separate stops the design-time tooling from
/// running the application's startup path (which would migrate and seed the real database).
/// </summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite("Data Source=ledgr.design.db")
            .Options;

        return new ApplicationDbContext(options);
    }
}

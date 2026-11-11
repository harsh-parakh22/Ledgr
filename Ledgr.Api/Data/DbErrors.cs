using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Ledgr.Api.Data;

internal static class DbErrors
{
    private const int ConstraintUnique = 2067;
    private const int ConstraintPrimaryKey = 1555;

    /// <summary>
    /// True when the failure was a unique index violation. The index hint keeps callers from
    /// treating an unrelated constraint (for example a foreign key) as a duplicate.
    /// </summary>
    public static bool IsUniqueViolation(DbUpdateException exception, string indexHint)
    {
        if (exception.InnerException is not SqliteException sqlite)
        {
            return false;
        }

        if (sqlite.SqliteExtendedErrorCode is not (ConstraintUnique or ConstraintPrimaryKey))
        {
            return false;
        }

        return sqlite.Message.Contains(indexHint, StringComparison.OrdinalIgnoreCase);
    }
}

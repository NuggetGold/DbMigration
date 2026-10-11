namespace DbMigration.Core.Security;

public sealed record SqlGuardVerdict(SqlGuardLevel Level, string Message)
{
    public bool IsDestructive => Level == SqlGuardLevel.Destructive;

    public bool RequiresConfirmation => Level != SqlGuardLevel.Safe;
}


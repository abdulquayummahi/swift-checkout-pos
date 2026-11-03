using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace SwiftCheckoutPOS.DAL.Context;

/// <summary>
/// Used ONLY by `dotnet ef migrations add/script` to build the model. Those commands never open
/// this connection. Applying migrations to a real database is an operator step (DATA_RECOVERY_PLAN.md),
/// never done from design-time tooling against the shop database.
/// </summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer(@"Server=(localdb)\MSSQLLocalDB;Database=SwiftCheckoutPOS_DesignTimeOnly;Integrated Security=True",
                sql => sql.EnableRetryOnFailure())
            .Options);
}

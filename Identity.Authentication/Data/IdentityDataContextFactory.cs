using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Identity.Authentication.Data
{
    // Used only by `dotnet ef migrations add` to build the model at design time;
    // the connection string here is never actually connected to.
    public class IdentityDataContextFactory : IDesignTimeDbContextFactory<IdentityDataContext>
    {
        public IdentityDataContext CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<IdentityDataContext>();
            optionsBuilder.UseSqlServer(
                "Server=localhost,1433;Database=Store;User Id=sa;Password=P@ssw0rd;TrustServerCertificate=True;",
                x => x.MigrationsAssembly(typeof(IdentityDataContext).Assembly.GetName().Name)
                    .MigrationsHistoryTable("__EFMigrationsHistory_Identity"));

            return new IdentityDataContext(optionsBuilder.Options);
        }
    }
}

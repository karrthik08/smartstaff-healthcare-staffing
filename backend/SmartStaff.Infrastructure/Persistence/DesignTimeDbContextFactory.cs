using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace SmartStaff.Infrastructure.Persistence;

public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        var database = DatabaseConfigurationReader.ReadFromEnvironment();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(NpgsqlConnectionStringFactory.Create(database))
            .Options;

        return new ApplicationDbContext(options);
    }
}

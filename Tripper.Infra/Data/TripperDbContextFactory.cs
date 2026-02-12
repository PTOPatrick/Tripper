using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Tripper.Infra.Data;

public class TripperDbContextFactory : IDesignTimeDbContextFactory<TripperDbContext>
{
    public TripperDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<TripperDbContext>();
        optionsBuilder.UseNpgsql("Host=localhost;Port=5432;Database=TripperDb;Username=postgres;Password=postgres");

        return new TripperDbContext(optionsBuilder.Options);
    }
}

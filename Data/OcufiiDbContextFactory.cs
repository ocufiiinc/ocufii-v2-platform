using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore;

namespace OcufiiAPI.Data
{
    public class OcufiiDbContextFactory : IDesignTimeDbContextFactory<OcufiiDbContext>
    {
        public OcufiiDbContext CreateDbContext(string[] args)
        {
            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json")
                .Build();

            var optionsBuilder = new DbContextOptionsBuilder<OcufiiDbContext>();
            optionsBuilder.UseNpgsql(configuration.GetConnectionString("OcufiiConnection"));

            return new OcufiiDbContext(optionsBuilder.Options);
        }
    }
}

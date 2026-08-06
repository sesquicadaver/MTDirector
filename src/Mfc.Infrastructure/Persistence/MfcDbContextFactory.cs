using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Mfc.Infrastructure.Persistence;

/// <summary>
/// Design-time factory for <c>dotnet ef</c>. Uses env <c>MFC__Database__ConnectionString</c> or a local default.
/// </summary>
public sealed class MfcDbContextFactory : IDesignTimeDbContextFactory<MfcDbContext>
{
    public MfcDbContext CreateDbContext(string[] args)
    {
        string connectionString =
            Environment.GetEnvironmentVariable("MFC__Database__ConnectionString")
            ?? "Host=127.0.0.1;Port=5432;Database=mfc;Username=mfc;Password=mfc_dev_only";

        DbContextOptionsBuilder<MfcDbContext> optionsBuilder = new();
        optionsBuilder.UseNpgsql(connectionString, npgsql =>
        {
            npgsql.MigrationsAssembly(typeof(MfcDbContext).Assembly.FullName);
        });

        return new MfcDbContext(optionsBuilder.Options);
    }
}

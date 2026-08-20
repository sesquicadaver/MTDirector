using System.Text;
using Mfc.Application.Abstractions.ConnectionProfiles;
using Mfc.Application.Abstractions.Secrets;
using Mfc.Controller;
using Mfc.Domain.Inventory;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Infrastructure.Persistence;
using Mfc.Infrastructure.Persistence.Entities;
using Mfc.Infrastructure.Secrets;
using Mfc.IntegrationTests.Fixtures;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace Mfc.IntegrationTests.Security;

[Collection(PostgresSharedFixtureDefinition.Name)]
public sealed class ConnectionProfileSecurityTests
{
    private readonly PostgresFixture _postgres;

    public ConnectionProfileSecurityTests(PostgresFixture postgres)
    {
        _postgres = postgres;
        AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);
    }

    [Fact]
    public async Task UpsertStoresCiphertextOnlyAndPinChangeWritesAudit()
    {
        string connectionString = await _postgres.CreateFreshDatabaseAsync();
        await using WebApplication app = BuildApp(connectionString);
        await app.Services.MigrateAsync();

        await using AsyncServiceScope scope = app.Services.CreateAsyncScope();
        MfcDbContext db = scope.ServiceProvider.GetRequiredService<MfcDbContext>();
        IConnectionProfileService profiles = scope.ServiceProvider.GetRequiredService<IConnectionProfileService>();
        ISecretProtector protector = scope.ServiceProvider.GetRequiredService<ISecretProtector>();

        Guid deviceId = await SeedDeviceAsync(db);
        const string password = "plaintext-must-not-persist";
        Hash256 pin = Hash256.Create(Enumerable.Repeat((byte)0xAB, 32).ToArray());

        ConnectionProfileView view = await profiles.UpsertAsync(new UpsertConnectionProfileCommand
        {
            DeviceId = deviceId,
            Username = "readonly",
            PasswordUtf8 = Encoding.UTF8.GetBytes(password),
            TrustMode = CertificateTrustMode.SpkiPin,
            PinnedSpkiSha256 = pin,
            Actor = "admin@test",
        });

        Assert.Equal(deviceId, view.DeviceId);
        Assert.Equal(pin.ToString(), view.PinnedSpkiSha256Hex);

        EncryptedSecretEntity secret = await db.EncryptedSecrets.SingleAsync(s => s.Id == view.SecretReference);
        Assert.Equal(ProtectedSecretMaterial.Aes256GcmAlgorithm, secret.Algorithm);
        string cipherAsText = Encoding.UTF8.GetString(secret.Ciphertext);
        Assert.DoesNotContain(password, cipherAsText, StringComparison.Ordinal);

        await using (NpgsqlConnection conn = new(connectionString))
        {
            await conn.OpenAsync();
            await using NpgsqlCommand cmd = conn.CreateCommand();
            cmd.CommandText = """
                SELECT convert_from(ciphertext, 'UTF8')
                FROM encrypted_secrets
                WHERE "Id" = @id;
                """;
            // ciphertext is binary; search via encode
            cmd.CommandText = """
                SELECT encode("Ciphertext", 'escape') LIKE @needle
                   OR encode("WrappedDek", 'escape') LIKE @needle
                FROM encrypted_secrets
                WHERE "Id" = @id;
                """;
            cmd.Parameters.AddWithValue("id", view.SecretReference);
            cmd.Parameters.AddWithValue("needle", "%" + password + "%");
            bool found = (bool)(await cmd.ExecuteScalarAsync() ?? false);
            Assert.False(found);
        }

        using (SecretLease lease = protector.Unprotect(new ProtectedSecretMaterial
        {
            Ciphertext = secret.Ciphertext,
            WrappedDek = secret.WrappedDek,
            Algorithm = secret.Algorithm,
        }))
        {
            Assert.Equal(password, Encoding.UTF8.GetString(lease.Plaintext));
        }

        Hash256 nextPin = Hash256.Create(Enumerable.Repeat((byte)0xCD, 32).ToArray());
        await profiles.ChangeSpkiPinAsync(deviceId, nextPin, "admin@test");

        AuditEventEntity audit = await db.AuditEvents.SingleAsync(a => a.Action == ConnectionProfileService.SpkiPinChangedAction);
        Assert.Contains(nextPin.ToString(), audit.PayloadJson, StringComparison.Ordinal);
        Assert.DoesNotContain(password, audit.PayloadJson, StringComparison.Ordinal);

        ConnectionProfileView? again = await profiles.GetViewAsync(deviceId);
        Assert.NotNull(again);
        Assert.Equal(nextPin.ToString(), again.PinnedSpkiSha256Hex);

        PropertyInfoAssertNoPassword(typeof(ConnectionProfileView));
    }

    [Fact]
    public async Task RotatePasswordKeepsDeviceIdentity()
    {
        string connectionString = await _postgres.CreateFreshDatabaseAsync();
        await using WebApplication app = BuildApp(connectionString);
        await app.Services.MigrateAsync();

        await using AsyncServiceScope scope = app.Services.CreateAsyncScope();
        MfcDbContext db = scope.ServiceProvider.GetRequiredService<MfcDbContext>();
        IConnectionProfileService profiles = scope.ServiceProvider.GetRequiredService<IConnectionProfileService>();

        Guid deviceId = await SeedDeviceAsync(db);
        ConnectionProfileView first = await profiles.UpsertAsync(new UpsertConnectionProfileCommand
        {
            DeviceId = deviceId,
            Username = "readonly",
            PasswordUtf8 = Encoding.UTF8.GetBytes("one"),
            TrustMode = CertificateTrustMode.InternalCa,
            CaProfileRef = "lab-ca",
            Actor = "admin@test",
        });

        ConnectionProfileView rotated = await profiles.RotatePasswordAsync(
            deviceId,
            Encoding.UTF8.GetBytes("two"),
            "admin@test");

        Assert.Equal(first.DeviceId, rotated.DeviceId);
        Assert.NotEqual(first.SecretReference, rotated.SecretReference);
        Assert.Equal(2, await db.EncryptedSecrets.CountAsync());
        Assert.Contains(
            await db.AuditEvents.Select(a => a.Action).ToListAsync(),
            a => a == ConnectionProfileService.SecretRotatedAction);
    }

    private static async Task<Guid> SeedDeviceAsync(MfcDbContext db)
    {
        SiteEntity site = new()
        {
            Id = Guid.NewGuid(),
            Code = "SEC01",
            Name = "Security lab",
            Status = 0,
            RowVersion = 1,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow,
        };
        NodeEntity node = new()
        {
            Id = Guid.NewGuid(),
            SiteId = site.Id,
            Name = "r1",
            DeclaredKind = 0,
            DeclaredUplinkMode = 1,
            Status = 0,
            RowVersion = 1,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow,
        };
        DeviceEntity device = new()
        {
            Id = Guid.NewGuid(),
            NodeId = node.Id,
            DisplayName = "r1",
            ManagementHost = "203.0.113.10",
            ManagementHostKind = 0,
            ManagementPort = 8729,
            Enabled = true,
            RowVersion = 1,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow,
        };
        db.Sites.Add(site);
        db.Nodes.Add(node);
        db.Devices.Add(device);
        await db.SaveChangesAsync();
        return device.Id;
    }

    private static void PropertyInfoAssertNoPassword(Type type)
    {
        Assert.DoesNotContain(
            type.GetProperties(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public),
            p => p.Name.Contains("Password", StringComparison.OrdinalIgnoreCase));
    }

    private static WebApplication BuildApp(string connectionString)
    {
        string url = $"http://127.0.0.1:{GetFreeTcpPort()}";
        return Program.BuildHost(
            [
                "--environment", "Development",
                $"--Mfc:Grpc:ListenAddress={url}",
                "--Mfc:Grpc:AllowInsecureLoopback=true",
                "--Mfc:Grpc:ShutdownTimeoutSeconds=5",
                "--Mfc:Security:RequireTls=true",
                "--Mfc:Security:MasterKeyProvider=Development",
                "--Mfc:Authentication:AllowDevelopmentAuthentication=true",
                "--Mfc:OperationalJobs:Enabled=false",
                $"--Mfc:Database:ConnectionString={connectionString}",
            ]);
    }

    private static int GetFreeTcpPort()
    {
        System.Net.Sockets.TcpListener listener = new(System.Net.IPAddress.Loopback, 0);
        listener.Start();
        int port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }
}

using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SwiftCheckoutPOS.BLL.Configuration;
using SwiftCheckoutPOS.BLL.DTOs;
using SwiftCheckoutPOS.BLL.Media;
using SwiftCheckoutPOS.BLL.Services;
using SwiftCheckoutPOS.DAL.Repositories;
using SwiftCheckoutPOS.Tests.Integration.Infrastructure;

namespace SwiftCheckoutPOS.Tests.Integration.Recovery;

/// <summary>Temporary data root + fake install folder, removed after each test.</summary>
internal sealed class TempRoot : IDisposable
{
    public TempRoot()
    {
        Root = Path.Combine(Path.GetTempPath(), "scpos-test-" + Guid.NewGuid().ToString("N")[..10]);
        Directory.CreateDirectory(Path.Combine(Root, "install"));
        Paths = new DataPaths(Path.Combine(Root, "data"), Path.Combine(Root, "install"));
        Paths.EnsureCreated();
    }
    public string Root { get; }
    public DataPaths Paths { get; }
    public void Dispose() { try { Directory.Delete(Root, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { } }
}

public sealed class BackupRulesTests
{
    private static BackupManifestDTO Set(DateTime at, bool verified = true, string kind = "Daily") => new()
    {
        SetName = $"SwiftCheckoutPOS_{at:yyyyMMdd_HHmmss}_{kind}", Kind = kind, CreatedAt = at, Verified = verified,
        BackupFile = $"SwiftCheckoutPOS_{at:yyyyMMdd_HHmmss}_{kind}.bak"
    };

    [Fact]
    [Trait("Fixes", "BP-05")]
    public void Approved_Retention_Keeps30DaysAnd12MonthlyAndNewest_PRQ55()
    {
        var sets = Enumerable.Range(0, 365).Select(d => Set(new DateTime(2025, 1, 1, 23, 0, 0).AddDays(d))).ToList();
        sets.Add(Set(new DateTime(2025, 12, 31, 10, 0, 0), kind: "Manual"));          // same day as newest: kept
        sets.Add(Set(new DateTime(2025, 6, 1, 9, 0, 0), verified: false));             // old failure record: removed
        sets.Add(new BackupManifestDTO { SetName = "SwiftCheckoutPOS_Backup_20240101_000000", Legacy = true, CreatedAt = new DateTime(2024, 1, 1) });
        var delete = BackupService.SelectForDeletion(sets).Select(s => s.SetName).ToHashSet();
        var kept = sets.Where(s => !delete.Contains(s.SetName)).ToList();
        Assert.Equal(30 + 1 + 12 + 1, kept.Count); // 30 days (Dec 2–31) + same-day manual + 12 month-firsts + legacy
        Assert.All(Enumerable.Range(1, 12), m => Assert.Contains(kept, s => s.CreatedAt == new DateTime(2025, m, 1, 23, 0, 0)));
        Assert.Contains(kept, s => s.CreatedAt == new DateTime(2025, 12, 31, 23, 0, 0));
        Assert.Contains(kept, s => s.Legacy);
        Assert.DoesNotContain(kept, s => !s.Verified && !s.Legacy);
        Assert.Empty(BackupService.SelectForDeletion(new[] { Set(new DateTime(2020, 1, 1)) })); // a lone backup is never deleted
    }

    [Fact]
    [Trait("Fixes", "BP-05")]
    public void Approved_Schedule_RunsAt2300AndCatchesUpAfterPcWasOff_PRQ53_PRQ89()
    {
        Assert.Equal(new DateTime(2026, 11, 3, 23, 0, 0), BackupService.LastSlot(new DateTime(2026, 11, 4, 9, 0, 0), new TimeSpan(23, 0, 0)));
        Assert.Equal(new DateTime(2026, 11, 4, 23, 0, 0), BackupService.LastSlot(new DateTime(2026, 11, 4, 23, 0, 0), new TimeSpan(23, 0, 0)));
        using var temp = new TempRoot();
        var now = new DateTime(2026, 11, 4, 22, 59, 0);
        var service = new BackupService(new BackupRepository("Server=(localdb)\\none;Database=Nothing"), temp.Paths, clock: () => now);
        Assert.True(service.IsScheduledBackupDue()); // no backup at all: catch-up
        File.WriteAllText(Path.Combine(temp.Paths.Backups, "SwiftCheckoutPOS_20261103_230500_Daily.json"),
            JsonSerializer.Serialize(Set(new DateTime(2026, 11, 3, 23, 5, 0))));
        Assert.False(service.IsScheduledBackupDue()); // last night's backup covers until 23:00 today
        now = new DateTime(2026, 11, 4, 23, 0, 30);
        Assert.True(service.IsScheduledBackupDue());
        now = new DateTime(2026, 11, 6, 8, 0, 0); // PC was off at 23:00 on the 4th and 5th
        Assert.True(service.IsScheduledBackupDue());
        service.SaveConfiguration(new BackupConfigDTO { IsAutoBackupEnabled = false });
        Assert.False(service.IsScheduledBackupDue());
    }

    [Fact]
    [Trait("Fixes", "IMM-10")]
    public void Approved_SettingsValidatedAndLegacyFilePreserved_BP12()
    {
        using var temp = new TempRoot();
        var legacy = Path.Combine(temp.Paths.InstallDirectory, "backup_config.json");
        File.WriteAllText(legacy, JsonSerializer.Serialize(new BackupConfigDTO
            { TargetDirectory = Path.Combine(temp.Paths.InstallDirectory, "Backups"), IsAutoBackupEnabled = false }));
        var service = new BackupService(new BackupRepository("Server=(localdb)\\none;Database=Nothing"), temp.Paths);
        var config = service.GetConfiguration();
        Assert.False(config.IsAutoBackupEnabled);
        Assert.Equal(Path.Combine(temp.Paths.InstallDirectory, "Backups"), config.LegacyTargetDirectory);
        Assert.Equal(temp.Paths.Backups, service.EffectiveTarget(config)); // install folder not allowed: default used
        Assert.True(File.Exists(legacy));

        Assert.Throws<InvalidOperationException>(() => service.SaveConfiguration(new BackupConfigDTO { TargetDirectory = Path.Combine(temp.Paths.InstallDirectory, "x") }));
        Assert.Throws<InvalidOperationException>(() => service.SaveConfiguration(new BackupConfigDTO { TargetDirectory = "Backups" }));
        Assert.Throws<InvalidOperationException>(() => service.SaveConfiguration(new BackupConfigDTO { TargetDirectory = @"\\server\share" }));
        Assert.Throws<InvalidOperationException>(() => service.SaveConfiguration(new BackupConfigDTO { AutoBackupTime = TimeSpan.FromHours(25) }));
        var good = Path.Combine(temp.Root, "elsewhere");
        service.SaveConfiguration(new BackupConfigDTO { TargetDirectory = good, AutoBackupTime = new TimeSpan(23, 0, 0) });
        Assert.Equal(good, service.EffectiveTarget(service.GetConfiguration()));
        Assert.True(File.Exists(Path.Combine(temp.Paths.Config, "backup_config.json")));
    }

    [Fact]
    [Trait("Fixes", "IMM-10")]
    public void Approved_DownloadOnlyExactBackupNamesInsideFolder_PRQ92()
    {
        using var temp = new TempRoot();
        var service = new BackupService(new BackupRepository("Server=(localdb)\\none;Database=Nothing"), temp.Paths);
        var good = "SwiftCheckoutPOS_20261104_230000_Daily.bak";
        File.WriteAllText(Path.Combine(temp.Paths.Backups, good), "x");
        File.WriteAllText(Path.Combine(temp.Paths.Backups, "SwiftCheckoutPOS_Backup_20250101_101010.bak"), "x");
        File.WriteAllText(Path.Combine(temp.Paths.Config, "secret.bak"), "x");
        Assert.NotNull(service.OpenBackupFile(good));
        Assert.NotNull(service.OpenBackupFile("SwiftCheckoutPOS_Backup_20250101_101010.bak"));
        foreach (var bad in new[] { "..\\Config\\secret.bak", "../Config/secret.bak", "*.bak", "SwiftCheckoutPOS_20261104_230000_Daily.json",
                     "C:\\Windows\\win.ini", "", null, "SwiftCheckoutPOS_20261104_230000_Daily.bak\\..\\..\\Config\\secret.bak" })
            Assert.Null(service.OpenBackupFile(bad));
    }
}

public sealed class ProductImageStoreTests
{
    private static byte[] Png(int width, int height)
    {
        var b = new byte[64];
        new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 13, (byte)'I', (byte)'H', (byte)'D', (byte)'R' }.CopyTo(b, 0);
        b[16] = (byte)(width >> 24); b[17] = (byte)(width >> 16); b[18] = (byte)(width >> 8); b[19] = (byte)width;
        b[20] = (byte)(height >> 24); b[21] = (byte)(height >> 16); b[22] = (byte)(height >> 8); b[23] = (byte)height;
        return b;
    }

    private static byte[] Jpeg(int width, int height) =>
        [0xFF, 0xD8, 0xFF, 0xE0, 0, 4, 0, 0, 0xFF, 0xC0, 0, 11, 8, (byte)(height >> 8), (byte)height, (byte)(width >> 8), (byte)width, 3, 0, 0, 0, 0];

    [Fact]
    [Trait("Fixes", "IMM-10")]
    public void Approved_ImagesValidatedNamedSafelyAndOldKeptOnFailure_PRQ72_74()
    {
        using var temp = new TempRoot();
        var legacy = Path.Combine(temp.Root, "wwwroot-images");
        Directory.CreateDirectory(legacy);
        var store = new ProductImageStore(temp.Paths.ProductImages, [legacy]);

        var first = store.Save(new MemoryStream(Png(800, 600)), "photo.png", "../../evil*");
        Assert.Matches(@"^p-evil-[a-f0-9]{12}\.png$", first);
        Assert.True(File.Exists(Path.Combine(temp.Paths.ProductImages, first)));
        Assert.Equal(("jpg", 640, 480), ProductImageStore.Inspect(Jpeg(640, 480)));

        Assert.Throws<InvalidOperationException>(() => store.Save(new MemoryStream(Png(2001, 10)), "big.png", "1"));
        Assert.Throws<InvalidOperationException>(() => store.Save(new MemoryStream(Png(10, 10)), "fake.jpg", "1"));   // content is PNG
        Assert.Throws<InvalidOperationException>(() => store.Save(new MemoryStream(Png(10, 10)), "x.gif", "1"));
        Assert.Throws<InvalidOperationException>(() => store.Save(new MemoryStream(new byte[3 * 1024 * 1024]), "x.png", "1"));
        Assert.Throws<InvalidOperationException>(() => store.Save(new MemoryStream("not an image"u8.ToArray()), "x.png", "1"));
        Assert.True(File.Exists(Path.Combine(temp.Paths.ProductImages, first)));               // failed uploads left the old one
        Assert.Single(Directory.GetFiles(temp.Paths.ProductImages));

        File.WriteAllText(Path.Combine(temp.Root, "data", "keep.txt"), "x");
        File.WriteAllText(Path.Combine(legacy, "12345.jpg"), "x");
        foreach (var bad in new[] { "../keep.txt", "..\\keep.txt", "*", "*.png", "default.png", "C:\\Windows\\win.ini" }) store.Remove(bad);
        Assert.True(File.Exists(Path.Combine(temp.Root, "data", "keep.txt")));
        Assert.True(File.Exists(Path.Combine(temp.Paths.ProductImages, first)));
        store.Remove("12345.jpg");                                                              // old barcode-named image
        Assert.False(File.Exists(Path.Combine(legacy, "12345.jpg")));
        store.Remove(first);
        Assert.Empty(Directory.GetFiles(temp.Paths.ProductImages));
    }
}

[Collection("SqlServer")]
public sealed class BackupDatabaseTests
{
    private readonly SqlServerFixture fixture;
    public BackupDatabaseTests(SqlServerFixture fixture) { this.fixture = fixture; fixture.Reset(); }

    [Fact]
    [Trait("Fixes", "BP-05")]
    public void Approved_VerifiedBackupUsbCopyDrillAndRestoreWithSafetyBackup_PRQ53_57_84()
    {
        using var temp = new TempRoot();
        File.WriteAllText(Path.Combine(temp.Paths.ProductImages, "p-a-000000000000.png"), "img");
        using (var c = fixture.Context()) FixtureData.Product(c, price: 10m);
        var now = new DateTime(2026, 11, 4, 23, 0, 0);
        using var context = fixture.Context();
        var service = new BackupService(new BackupRepository(context), temp.Paths, new AtomicUnit(context), () => now);

        var set = service.RunBackup("Manual", FixtureData.Actor);
        Assert.True(set.Verified);
        Assert.Equal(64, set.Sha256.Length);
        Assert.True(File.Exists(Path.Combine(temp.Paths.Backups, set.BackupFile)));
        Assert.NotNull(set.FilesArchive);                                    // PR-Q57 images/settings kept with the backup
        using (var zip = System.IO.Compression.ZipFile.OpenRead(Path.Combine(temp.Paths.Backups, set.FilesArchive!)))
            Assert.Contains(zip.Entries, e => e.FullName == "Media/products/p-a-000000000000.png");
        Assert.Single(service.GetHistory(), s => s.Verified);
        Assert.False(service.IsScheduledBackupDue());

        var usb = Path.Combine(temp.Root, "usb");
        Assert.Throws<InvalidOperationException>(() => service.CopyLatestToUsb(Path.Combine(temp.Paths.Root, "inside"), FixtureData.Actor));
        service.CopyLatestToUsb(usb, FixtureData.Actor);
        Assert.True(File.Exists(Path.Combine(usb, "SwiftCheckoutPOS-Backups", set.BackupFile)));
        Assert.Equal(set.SetName, service.GetStatus().LastUsbCopySet);

        using (var c = fixture.Context()) FixtureData.Product(c, price: 20m, barcode: "SECOND"); // made after the backup

        Assert.Contains("1 products", service.RestoreDrill(null, FixtureData.Actor));
        Assert.True(service.GetStatus().LastDrillPassed);
        using (var c = fixture.Context()) Assert.Equal(2, c.Products.Count());   // drill did not touch live data

        // In-place restore is exercised on a separate copy so the fixture's own session is not cut off.
        const string liveCopy = "SwiftCheckoutPOS_Test_RestoreLive";
        var repo = new BackupRepository(context);
        repo.DropDatabase(liveCopy);
        try
        {
            repo.RestoreAsCopy(Path.Combine(temp.Paths.Backups, set.BackupFile), liveCopy);
            var copyConnection = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(context.Database.GetDbConnection().ConnectionString) { InitialCatalog = liveCopy }.ConnectionString;
            using (var c = new Microsoft.Data.SqlClient.SqlConnection(copyConnection))
            {
                c.Open();
                using var cmd = new Microsoft.Data.SqlClient.SqlCommand("UPDATE dbo.Products SET ProductName = N'changed after backup';", c);
                cmd.ExecuteNonQuery();
            }
            var copyService = new BackupService(new BackupRepository(copyConnection), temp.Paths, null, () => now.AddMinutes(5));
            var message = copyService.RestoreLive(set.SetName, FixtureData.Actor);
            Assert.Contains("Safety backup", message);
            Assert.Equal(1, repo.CountRows(liveCopy).Products);
            using (var c = new Microsoft.Data.SqlClient.SqlConnection(copyConnection))
            {
                c.Open();
                using var cmd = new Microsoft.Data.SqlClient.SqlCommand("SELECT ProductName FROM dbo.Products;", c);
                Assert.Equal("BP10 synthetic item", cmd.ExecuteScalar());  // the change made after the backup is gone
            }
            Assert.Contains(copyService.GetHistory(), s => s.Kind == "PreRestore" && s.Verified && s.Database == liveCopy);

            File.AppendAllText(Path.Combine(temp.Paths.Backups, set.BackupFile), "tamper");
            Assert.Throws<InvalidOperationException>(() => copyService.RestoreLive(set.SetName, FixtureData.Actor));
        }
        finally
        {
            repo.DropDatabase(liveCopy);
        }
    }
}

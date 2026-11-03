using System;
using System.IO;

namespace SwiftCheckoutPOS.BLL.Configuration;

/// <summary>
/// BP-06/BP-12: every file the shop owns lives under one machine-local data root, outside the install folder,
/// so an upgrade or reinstall never touches it. Default: %ProgramData%\SwiftCheckoutPOS (config "Paths:DataRoot").
/// </summary>
public sealed class DataPaths
{
    public DataPaths(string? dataRoot = null, string? installDirectory = null)
    {
        Root = Path.GetFullPath(string.IsNullOrWhiteSpace(dataRoot)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "SwiftCheckoutPOS")
            : dataRoot);
        InstallDirectory = Path.GetFullPath(installDirectory ?? AppContext.BaseDirectory);
    }

    public string Root { get; }
    public string InstallDirectory { get; }
    public string Config => Path.Combine(Root, "Config");
    public string Backups => Path.Combine(Root, "Backups");
    public string Media => Path.Combine(Root, "Media");
    public string ProductImages => Path.Combine(Media, "products");
    public string Logs => Path.Combine(Root, "Logs");
    public string Keys => Path.Combine(Root, "Keys");
    public string VirtualReceipts => Path.Combine(Root, "VirtualReceipts");
    /// <summary>Machine-local settings file (connection string, printer, ports). Never committed.</summary>
    public string LocalSettingsFile => Path.Combine(Config, "appsettings.Local.json");

    public void EnsureCreated()
    {
        foreach (var dir in new[] { Root, Config, Backups, Media, ProductImages, Logs, Keys, VirtualReceipts })
            Directory.CreateDirectory(dir);
    }

    /// <summary>True when <paramref name="path"/> is <paramref name="folder"/> or inside it (no ../ escapes).</summary>
    public static bool IsInside(string path, string folder)
    {
        var p = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var f = Path.GetFullPath(folder).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return p.StartsWith(f, StringComparison.OrdinalIgnoreCase);
    }
}

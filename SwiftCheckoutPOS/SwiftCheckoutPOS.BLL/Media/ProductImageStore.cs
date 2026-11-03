using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace SwiftCheckoutPOS.BLL.Media;

/// <summary>
/// IMM-10 / PR-Q72–74: product images. JPG/PNG/WebP only (checked by file content, not just the name), at most
/// 2 MB and 2000 × 2000 pixels. Each upload gets a new generated name in the media folder; the barcode is never
/// used as a path or wildcard. The old image is removed only after the product is saved, so a failed upload or
/// failed save keeps the old picture.
/// </summary>
public sealed class ProductImageStore
{
    public const long MaxBytes = 2 * 1024 * 1024;
    public const int MaxPixels = 2000;
    private static readonly Regex StoredName = new(@"^p-[a-z0-9]{1,24}-[a-f0-9]{12}\.(jpg|png|webp)$", RegexOptions.CultureInvariant);
    private static readonly Regex LegacyName = new(@"^[A-Za-z0-9_\-]{1,64}\.(jpg|jpeg|png|webp)$", RegexOptions.CultureInvariant);

    private readonly string _folder;
    private readonly IReadOnlyList<string> _legacyFolders;

    public ProductImageStore(string folder, IEnumerable<string>? legacyFolders = null)
    {
        _folder = Path.GetFullPath(folder);
        _legacyFolders = (legacyFolders ?? Array.Empty<string>()).Select(Path.GetFullPath).ToList();
    }

    public string Folder => _folder;

    /// <summary>Validates and stores the upload. Returns the new stored file name. Throws InvalidOperationException with a plain message.</summary>
    public string Save(Stream content, string? originalFileName, string barcode)
    {
        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        int read;
        while ((read = content.Read(chunk, 0, chunk.Length)) > 0)
        {
            buffer.Write(chunk, 0, read);
            if (buffer.Length > MaxBytes) throw new InvalidOperationException("The image is larger than 2 MB.");
        }
        var bytes = buffer.ToArray();
        if (bytes.Length == 0) throw new InvalidOperationException("The image file is empty.");
        var claimed = Path.GetExtension(originalFileName ?? "").ToLowerInvariant();
        if (claimed is not (".jpg" or ".jpeg" or ".png" or ".webp")) throw new InvalidOperationException("Only JPG, PNG or WebP images are allowed.");
        var (format, width, height) = Inspect(bytes);
        if (format is null) throw new InvalidOperationException("The file is not a readable JPG, PNG or WebP image.");
        if ((claimed is ".jpg" or ".jpeg") != (format == "jpg") || (claimed == ".png") != (format == "png") || (claimed == ".webp") != (format == "webp"))
            throw new InvalidOperationException("The file content does not match its .jpg/.png/.webp name.");
        if (width <= 0 || height <= 0 || width > MaxPixels || height > MaxPixels)
            throw new InvalidOperationException($"The image must be at most {MaxPixels} × {MaxPixels} pixels (this one is {width} × {height}).");

        var token = new string((barcode ?? "").ToLowerInvariant().Where(c => c is >= 'a' and <= 'z' or >= '0' and <= '9').Take(24).ToArray());
        if (token.Length == 0) token = "item";
        var name = $"p-{token}-{Guid.NewGuid().ToString("N")[..12]}.{format}";
        Directory.CreateDirectory(_folder);
        var final = Path.Combine(_folder, name);
        var temp = final + ".upload";
        File.WriteAllBytes(temp, bytes);
        File.Move(temp, final);
        return name;
    }

    /// <summary>Removes a stored image by its exact name. Anything that is not a plain image file name in an image folder is ignored.</summary>
    public void Remove(string? storedName)
    {
        if (string.IsNullOrWhiteSpace(storedName) || storedName != Path.GetFileName(storedName)) return;
        if (storedName.Equals("default.png", StringComparison.OrdinalIgnoreCase)) return;
        var folders = StoredName.IsMatch(storedName) ? new[] { _folder } : LegacyName.IsMatch(storedName) ? _legacyFolders.Append(_folder).ToArray() : Array.Empty<string>();
        foreach (var folder in folders)
        {
            var full = Path.GetFullPath(Path.Combine(folder, storedName));
            if (!full.StartsWith(folder.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) continue;
            try { if (File.Exists(full)) File.Delete(full); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
    }

    /// <summary>Reads the image header: format and pixel size, without decoding the picture.</summary>
    public static (string? Format, int Width, int Height) Inspect(byte[] b)
    {
        if (b.Length > 24 && b[0] == 0x89 && b[1] == 0x50 && b[2] == 0x4E && b[3] == 0x47 && b[12] == 'I' && b[13] == 'H' && b[14] == 'D' && b[15] == 'R')
            return ("png", BigEndian(b, 16), BigEndian(b, 20));
        if (b.Length > 4 && b[0] == 0xFF && b[1] == 0xD8)
        {
            var i = 2;
            while (i + 9 < b.Length)
            {
                if (b[i] != 0xFF) { i++; continue; }
                var marker = b[i + 1];
                if (marker == 0xFF) { i++; continue; }
                if (marker is 0xD8 or 0x01 || marker is >= 0xD0 and <= 0xD7) { i += 2; continue; }
                var length = (b[i + 2] << 8) | b[i + 3];
                if (marker is >= 0xC0 and <= 0xCF && marker is not (0xC4 or 0xC8 or 0xCC))
                    return ("jpg", (b[i + 7] << 8) | b[i + 8], (b[i + 5] << 8) | b[i + 6]);
                if (length < 2) break;
                i += 2 + length;
            }
            return (null, 0, 0);
        }
        if (b.Length > 30 && b[0] == 'R' && b[1] == 'I' && b[2] == 'F' && b[3] == 'F' && b[8] == 'W' && b[9] == 'E' && b[10] == 'B' && b[11] == 'P')
        {
            var chunk = System.Text.Encoding.ASCII.GetString(b, 12, 4);
            switch (chunk)
            {
                case "VP8X": return ("webp", 1 + (b[24] | b[25] << 8 | b[26] << 16), 1 + (b[27] | b[28] << 8 | b[29] << 16));
                case "VP8L":
                    if (b[20] != 0x2F) return (null, 0, 0);
                    var bits = b[21] | b[22] << 8 | b[23] << 16 | b[24] << 24;
                    return ("webp", (bits & 0x3FFF) + 1, ((bits >> 14) & 0x3FFF) + 1);
                case "VP8 ":
                    if (b[23] != 0x9D || b[24] != 0x01 || b[25] != 0x2A) return (null, 0, 0);
                    return ("webp", (b[26] | b[27] << 8) & 0x3FFF, (b[28] | b[29] << 8) & 0x3FFF);
            }
        }
        return (null, 0, 0);
    }

    private static int BigEndian(byte[] b, int at) => (b[at] << 24) | (b[at + 1] << 16) | (b[at + 2] << 8) | b[at + 3];
}

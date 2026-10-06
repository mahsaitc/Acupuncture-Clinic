namespace Clinic.Web.Media;

/// <summary>
/// Stores public site media (hero video and poster) on disk and serves it under /media.
/// Medical files must never go here: everything in this folder is publicly readable.
/// </summary>
public class MediaStore(IConfiguration config, IWebHostEnvironment env)
{
    public const string RequestPath = "/media";

    public const long MaxVideoBytes = 150L * 1024 * 1024;
    public const long MaxImageBytes = 5L * 1024 * 1024;

    private static readonly Dictionary<string, string> VideoTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        [".mp4"] = "video/mp4",
        [".webm"] = "video/webm",
    };

    private static readonly Dictionary<string, string> ImageTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".png"] = "image/png",
        [".webp"] = "image/webp",
    };

    public string Root => Path.GetFullPath(config["Media:Root"] ?? Path.Combine(env.ContentRootPath, "media"));

    public Task<SaveResult> SaveVideoAsync(IFormFile file, CancellationToken ct = default) =>
        SaveAsync(file, "hero", VideoTypes, MaxVideoBytes, ct);

    public Task<SaveResult> SaveImageAsync(IFormFile file, CancellationToken ct = default) =>
        SaveAsync(file, "hero", ImageTypes, MaxImageBytes, ct);

    /// <summary>Deletes a file previously returned by this store. Paths outside the store are ignored.</summary>
    public void Delete(string? webPath)
    {
        if (string.IsNullOrEmpty(webPath) || !webPath.StartsWith(RequestPath + "/", StringComparison.Ordinal))
        {
            return;
        }
        var full = Path.GetFullPath(Path.Combine(Root, webPath[(RequestPath.Length + 1)..]));
        if (full.StartsWith(Root + Path.DirectorySeparatorChar, StringComparison.Ordinal) && File.Exists(full))
        {
            File.Delete(full);
        }
    }

    private async Task<SaveResult> SaveAsync(IFormFile file, string folder, Dictionary<string, string> allowed, long maxBytes, CancellationToken ct)
    {
        var extension = Path.GetExtension(file.FileName);
        if (!allowed.TryGetValue(extension, out var contentType) || !string.Equals(file.ContentType, contentType, StringComparison.OrdinalIgnoreCase))
        {
            return SaveResult.Fail(SaveError.WrongType);
        }
        if (file.Length == 0 || file.Length > maxBytes)
        {
            return SaveResult.Fail(SaveError.TooLarge);
        }

        await using (var head = file.OpenReadStream())
        {
            var buffer = new byte[12];
            var read = await head.ReadAtLeastAsync(buffer, buffer.Length, throwOnEndOfStream: false, ct);
            if (!LooksLike(contentType, buffer.AsSpan(0, read)))
            {
                return SaveResult.Fail(SaveError.WrongType);
            }
        }

        var directory = Path.Combine(Root, folder);
        Directory.CreateDirectory(directory);
        // Never trust the uploaded name: a random name blocks path tricks and overwrites.
        var name = $"{Guid.NewGuid():N}{extension.ToLowerInvariant()}";
        await using (var target = File.Create(Path.Combine(directory, name)))
        {
            await file.CopyToAsync(target, ct);
        }
        return SaveResult.Ok($"{RequestPath}/{folder}/{name}");
    }

    /// <summary>Checks the file signature so a renamed executable is not accepted as a video.</summary>
    internal static bool LooksLike(string contentType, ReadOnlySpan<byte> head) => contentType switch
    {
        "video/mp4" => head.Length >= 8 && head[4..8].SequenceEqual("ftyp"u8),
        "video/webm" => head.Length >= 4 && head[..4].SequenceEqual(new byte[] { 0x1A, 0x45, 0xDF, 0xA3 }),
        "image/jpeg" => head.Length >= 3 && head[0] == 0xFF && head[1] == 0xD8 && head[2] == 0xFF,
        "image/png" => head.Length >= 8 && head[..8].SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }),
        "image/webp" => head.Length >= 12 && head[..4].SequenceEqual("RIFF"u8) && head[8..12].SequenceEqual("WEBP"u8),
        _ => false,
    };

    public enum SaveError
    {
        None,
        WrongType,
        TooLarge,
    }

    public sealed record SaveResult(string? WebPath, SaveError Error)
    {
        public static SaveResult Ok(string path) => new(path, SaveError.None);

        public static SaveResult Fail(SaveError error) => new(null, error);
    }
}

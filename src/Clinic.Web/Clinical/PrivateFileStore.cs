namespace Clinic.Web.Clinical;

/// <summary>
/// Stores patients' medical files outside the web root. Nothing here is served directly:
/// every download goes through a page that checks access and writes an audit entry.
/// </summary>
public class PrivateFileStore(IConfiguration config, IWebHostEnvironment env)
{
    public const long MaxBytes = 200L * 1024;

    /// <summary>The value for an input's accept attribute.</summary>
    public const string Accept = ".jpg,.jpeg,.png,.webp,.pdf,.dcm";

    private static readonly Dictionary<string, string> Types = new(StringComparer.OrdinalIgnoreCase)
    {
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".png"] = "image/png",
        [".webp"] = "image/webp",
        [".pdf"] = "application/pdf",
        [".dcm"] = "application/dicom",
    };

    public string Root => Path.GetFullPath(config["PrivateFiles:Root"] ?? Path.Combine(env.ContentRootPath, "private-files"));

    public async Task<SaveResult> SaveAsync(IFormFile file, CancellationToken ct = default)
    {
        // Browsers send DICOM files with all sorts of content types, so the extension and the signature decide.
        if (!Types.TryGetValue(Path.GetExtension(file.FileName), out var contentType))
        {
            return SaveResult.Fail(SaveError.WrongType);
        }
        if (file.Length == 0 || file.Length > MaxBytes)
        {
            return SaveResult.Fail(SaveError.TooLarge);
        }

        await using (var head = file.OpenReadStream())
        {
            var buffer = new byte[132];
            var read = await head.ReadAtLeastAsync(buffer, buffer.Length, throwOnEndOfStream: false, ct);
            if (!LooksLike(contentType, buffer.AsSpan(0, read)))
            {
                return SaveResult.Fail(SaveError.WrongType);
            }
        }

        Directory.CreateDirectory(Root);
        var name = $"{Guid.NewGuid():N}{Path.GetExtension(file.FileName).ToLowerInvariant()}";
        await using (var target = File.Create(Path.Combine(Root, name)))
        {
            await file.CopyToAsync(target, ct);
        }
        return new SaveResult(name, contentType, file.Length, SaveError.None);
    }

    /// <summary>Opens a stored file, or returns null if the name is not one this store created.</summary>
    public Stream? Open(string storedName)
    {
        var path = PathOf(storedName);
        return path is not null && File.Exists(path) ? File.OpenRead(path) : null;
    }

    public void Delete(string storedName)
    {
        var path = PathOf(storedName);
        if (path is not null && File.Exists(path))
        {
            File.Delete(path);
        }
    }

    private string? PathOf(string storedName)
    {
        if (storedName.Length == 0 || storedName != Path.GetFileName(storedName))
        {
            return null;
        }
        var full = Path.GetFullPath(Path.Combine(Root, storedName));
        return full.StartsWith(Root + Path.DirectorySeparatorChar, StringComparison.Ordinal) ? full : null;
    }

    internal static bool LooksLike(string contentType, ReadOnlySpan<byte> head) => contentType switch
    {
        "application/pdf" => head.Length >= 5 && head[..5].SequenceEqual("%PDF-"u8),
        // DICOM Part 10: a 128-byte preamble, then "DICM".
        "application/dicom" => head.Length >= 132 && head[128..132].SequenceEqual("DICM"u8),
        _ => Media.MediaStore.LooksLike(contentType, head),
    };

    public enum SaveError
    {
        None,
        WrongType,
        TooLarge,
    }

    public sealed record SaveResult(string? StoredName, string? ContentType, long Size, SaveError Error)
    {
        public static SaveResult Fail(SaveError error) => new(null, null, 0, error);
    }
}

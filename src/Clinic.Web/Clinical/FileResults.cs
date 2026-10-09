using Clinic.Domain.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;

namespace Clinic.Web.Clinical;

public static class FileResults
{
    /// <summary>
    /// A medical file response that browsers and proxies do not cache, and that cannot be sniffed into
    /// something else. Only images and PDFs open in the browser; everything else downloads.
    /// </summary>
    public static FileStreamResult Private(HttpResponse response, Stream stream, MedicalFile file, bool download) =>
        Private(response, stream, file.ContentType, file.Title, download);

    public static FileStreamResult Private(HttpResponse response, Stream stream, string contentType, string title, bool download)
    {
        var isImage = contentType.StartsWith("image/", StringComparison.Ordinal);
        response.Headers.CacheControl = "no-store, private";
        response.Headers.XContentTypeOptions = "nosniff";
        if (isImage)
        {
            // Not for PDFs: a sandboxing policy stops the browser's own PDF viewer.
            response.Headers.ContentSecurityPolicy = "default-src 'none'; img-src 'self'; style-src 'unsafe-inline'; sandbox";
        }

        var inline = !download && (isImage || contentType == "application/pdf");
        var name = $"{Safe(title)}{Extension(contentType)}";
        var disposition = new ContentDispositionHeaderValue(inline ? "inline" : "attachment");
        disposition.SetHttpFileName(name);
        response.Headers.ContentDisposition = disposition.ToString();
        return new FileStreamResult(stream, contentType) { EnableRangeProcessing = true };
    }

    private static string Safe(string title)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var clean = new string(title.Select(c => invalid.Contains(c) || char.IsControl(c) ? '_' : c).ToArray()).Trim();
        return clean.Length == 0 ? "file" : clean.Length > 80 ? clean[..80] : clean;
    }

    private static string Extension(string contentType) => contentType switch
    {
        "image/jpeg" => ".jpg",
        "image/png" => ".png",
        "image/webp" => ".webp",
        "application/pdf" => ".pdf",
        "application/dicom" => ".dcm",
        _ => "",
    };
}

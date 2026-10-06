using Clinic.Web.Media;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;

namespace Clinic.Tests;

public sealed class MediaStoreTests : IDisposable
{
    private static readonly byte[] Mp4Header = [0, 0, 0, 0x18, (byte)'f', (byte)'t', (byte)'y', (byte)'p', (byte)'m', (byte)'p', (byte)'4', (byte)'2'];

    private readonly string _root = Path.Combine(Path.GetTempPath(), $"clinic-media-{Guid.NewGuid():N}");
    private readonly MediaStore _store;

    public MediaStoreTests()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Media:Root"] = _root }).Build();
        _store = new MediaStore(config, new FakeEnv());
    }

    [Fact]
    public async Task Saves_an_mp4_under_a_random_name()
    {
        var result = await _store.SaveVideoAsync(File("../../evil name.mp4", "video/mp4", Mp4Header));

        Assert.Equal(MediaStore.SaveError.None, result.Error);
        Assert.Matches("^/media/hero/[0-9a-f]{32}\\.mp4$", result.WebPath!);
        Assert.True(System.IO.File.Exists(Path.Combine(_root, "hero", Path.GetFileName(result.WebPath!))));
    }

    [Fact]
    public async Task Rejects_a_file_whose_content_is_not_a_video()
    {
        var result = await _store.SaveVideoAsync(File("clip.mp4", "video/mp4", "MZ this is an exe"u8.ToArray()));

        Assert.Equal(MediaStore.SaveError.WrongType, result.Error);
    }

    [Fact]
    public async Task Rejects_other_extensions()
    {
        var result = await _store.SaveVideoAsync(File("clip.mov", "video/quicktime", Mp4Header));

        Assert.Equal(MediaStore.SaveError.WrongType, result.Error);
    }

    [Fact]
    public async Task Delete_removes_stored_files_but_ignores_paths_outside_the_store()
    {
        var saved = await _store.SaveVideoAsync(File("clip.mp4", "video/mp4", Mp4Header));
        var outside = Path.Combine(Path.GetTempPath(), $"keep-{Guid.NewGuid():N}.txt");
        await System.IO.File.WriteAllTextAsync(outside, "keep");

        _store.Delete("/media/../../" + Path.GetFileName(outside));
        _store.Delete(saved.WebPath);

        Assert.True(System.IO.File.Exists(outside));
        Assert.False(System.IO.File.Exists(Path.Combine(_root, "hero", Path.GetFileName(saved.WebPath!))));
        System.IO.File.Delete(outside);
    }

    private static FormFile File(string name, string contentType, byte[] content) =>
        new(new MemoryStream(content), 0, content.Length, "video", name)
        {
            Headers = new HeaderDictionary(),
            ContentType = contentType,
        };

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private sealed class FakeEnv : IWebHostEnvironment
    {
        public string WebRootPath { get; set; } = "";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string ApplicationName { get; set; } = "Clinic.Web";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        public string ContentRootPath { get; set; } = Path.GetTempPath();
        public string EnvironmentName { get; set; } = "Test";
    }
}

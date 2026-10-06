using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Clinic.Tests;

public partial class LocalizationTests
{
    /// <summary>Every English UI key used in the web project must have a Persian translation.</summary>
    [Fact]
    public void Every_ui_string_has_a_persian_translation()
    {
        var web = Path.Combine(RepoRoot(), "src", "Clinic.Web");
        var translated = XDocument.Load(Path.Combine(web, "Resources", "SharedResource.fa.resx"))
            .Descendants("data")
            .Select(d => (string)d.Attribute("name")!)
            .ToHashSet();

        var used = Directory.EnumerateFiles(web, "*.*", SearchOption.AllDirectories)
            .Where(f => f.EndsWith(".cs") || f.EndsWith(".cshtml"))
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                        && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
            .SelectMany(f => KeyPattern().Matches(File.ReadAllText(f)).Select(m => m.Groups[1].Value + m.Groups[2].Value))
            .ToHashSet();

        // Values rendered through L[value.ToString()].
        used.UnionWith(Enum.GetNames<Domain.Entities.AppointmentStatus>());
        used.UnionWith(Enum.GetNames<DayOfWeek>());
        used.UnionWith(Domain.Roles.All);

        Assert.Empty(used.Except(translated).Order());
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !dir.EnumerateFiles("*.slnx").Any())
        {
            dir = dir.Parent;
        }
        return dir?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }

    [GeneratedRegex("""\b[Ll]\["((?:[^"\\]|\\.)*)"|(?:Name|ErrorMessage) = "((?:[^"\\]|\\.)*)"(?=[,)])""")]
    private static partial Regex KeyPattern();
}

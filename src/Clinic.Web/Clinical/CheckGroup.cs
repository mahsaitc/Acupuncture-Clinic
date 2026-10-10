namespace Clinic.Web.Clinical;

public sealed record CheckOption(string Value, string Label, bool Checked);

/// <summary>
/// A row of tick boxes (several answers) or radio buttons (one answer) for the _CheckGroup partial.
/// Labels are resource keys: the enum names, or the yes/no keys below.
/// </summary>
public sealed record CheckGroup(string Name, IReadOnlyList<CheckOption> Options, bool Single)
{
    public static CheckGroup Flags<T>(string name, T value) where T : struct, Enum => new(name,
        Enum.GetValues<T>().Where(v => Convert.ToInt64(v) != 0).Select(v => new CheckOption(v.ToString(), v.ToString(), value.HasFlag(v))).ToList(),
        Single: false);

    public static CheckGroup One<T>(string name, T? value) where T : struct, Enum => new(name,
        Enum.GetValues<T>().Select(v => new CheckOption(v.ToString(), v.ToString(), value.HasValue && value.Value.Equals(v))).ToList(),
        Single: true);

    public static CheckGroup HasAllergy(string name, bool? value) => new(name,
        [new("false", "No allergy", value == false), new("true", "Has an allergy", value == true)],
        Single: true);

    /// <summary>Combines the ticked values of a flags enum.</summary>
    public static T Combine<T>(IEnumerable<T>? values) where T : struct, Enum =>
        (T)Enum.ToObject(typeof(T), (values ?? []).Aggregate(0L, (all, v) => all | Convert.ToInt64(v)));

    /// <summary>The ticked values of a flags enum, for filling the form.</summary>
    public static List<T> Split<T>(T value) where T : struct, Enum =>
        Enum.GetValues<T>().Where(v => Convert.ToInt64(v) != 0 && value.HasFlag(v)).ToList();
}

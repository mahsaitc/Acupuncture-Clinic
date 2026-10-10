namespace Clinic.Web.Clinical;

/// <summary>English names (the resource keys) of the chart pages and views.</summary>
public static class ChartText
{
    public static string Page(string page) => page switch
    {
        "body" => "Whole body",
        "head" => "Head and face",
        "arm" => "Arm and hand",
        "leg" => "Leg and foot",
        "ear" => "Ear",
        _ => page,
    };

    public static string View(string view) => view switch
    {
        "front" => "Front",
        "back" => "Back",
        "side" => "Side",
        "head-front" => "Face",
        "head-side" => "Head, side",
        "arm-inner" => "Inner arm (palm side)",
        "arm-outer" => "Outer arm (back of the hand)",
        "leg-inner" => "Inner leg",
        "leg-outer" => "Outer leg",
        "ear" => "Ear",
        _ => view,
    };
}

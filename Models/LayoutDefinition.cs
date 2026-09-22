namespace DesktopSplit.Models;

public sealed class LayoutDefinition
{
    public string Name { get; set; } = "自定义布局";
    public List<ZoneDefinition> Zones { get; set; } = [];

    public LayoutDefinition Clone() => new()
    {
        Name = Name,
        Zones = (Zones ?? []).Select(zone => zone.Clone()).ToList()
    };

    public static LayoutDefinition MainWide(double leftRatio = 0.8) => new()
    {
        Name = "4/5 + 1/5",
        Zones =
        [
            new ZoneDefinition { Name = "主区域", Left = 0, Top = 0, Width = leftRatio, Height = 1 },
            new ZoneDefinition { Name = "侧边区域", Left = leftRatio, Top = 0, Width = 1 - leftRatio, Height = 1 }
        ]
    };

    public static LayoutDefinition EqualSplit() => new()
    {
        Name = "左右均分",
        Zones =
        [
            new ZoneDefinition { Name = "左侧", Left = 0, Top = 0, Width = 0.5, Height = 1 },
            new ZoneDefinition { Name = "右侧", Left = 0.5, Top = 0, Width = 0.5, Height = 1 }
        ]
    };

    public static LayoutDefinition ThreeColumns() => new()
    {
        Name = "三列布局",
        Zones =
        [
            new ZoneDefinition { Name = "左列", Left = 0, Top = 0, Width = 1d / 3, Height = 1 },
            new ZoneDefinition { Name = "中列", Left = 1d / 3, Top = 0, Width = 1d / 3, Height = 1 },
            new ZoneDefinition { Name = "右列", Left = 2d / 3, Top = 0, Width = 1d / 3, Height = 1 }
        ]
    };

    public static LayoutDefinition TwoZone(double leftRatio, string name = "自定义两区")
    {
        leftRatio = Math.Clamp(leftRatio, 0.1, 0.9);
        return new LayoutDefinition
        {
            Name = name,
            Zones =
            [
                new ZoneDefinition { Name = "左侧", Left = 0, Top = 0, Width = leftRatio, Height = 1 },
                new ZoneDefinition { Name = "右侧", Left = leftRatio, Top = 0, Width = 1 - leftRatio, Height = 1 }
            ]
        };
    }

    public static IReadOnlyList<LayoutDefinition> BuiltIns =>
    [
        EqualSplit(),
        MainWide(),
        ThreeColumns()
    ];
}

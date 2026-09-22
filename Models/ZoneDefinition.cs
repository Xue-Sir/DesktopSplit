namespace DesktopSplit.Models;

public sealed class ZoneDefinition
{
    public string Name { get; set; } = "区域";

    // Bounds are normalized to the monitor work area: 0..1.
    public double Left { get; set; }
    public double Top { get; set; }
    public double Width { get; set; }
    public double Height { get; set; } = 1;

    public ZoneDefinition Clone() => new()
    {
        Name = Name,
        Left = Left,
        Top = Top,
        Width = Width,
        Height = Height
    };
}

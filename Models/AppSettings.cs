namespace DesktopSplit.Models;

public sealed class AppSettings
{
    public bool Autostart { get; set; } = true;
    public LayoutDefinition ActiveLayout { get; set; } = LayoutDefinition.MainWide();
    public List<LayoutDefinition> CustomLayouts { get; set; } = [];
}

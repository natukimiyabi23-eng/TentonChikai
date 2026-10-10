using Dalamud.Configuration;

namespace YMoveStandalone;

public sealed class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 1;
    public bool Enabled { get; set; }
    public float VerticalOffset { get; set; }
    public bool AutoDeepDungeonSafety { get; set; } = true;
}

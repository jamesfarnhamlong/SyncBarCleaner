using Dalamud.Configuration;
using System;

namespace SyncBarCleaner;

[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 2;

    public bool AutoEnableOnLoad { get; set; } = true;

    public bool EnableMainCrossHotbar { get; set; } = true;

    public bool EnableExpandedCrossHotbars { get; set; } = true;

    public bool EnableTestMode { get; set; } = false;

    public int TestEffectiveLevel { get; set; } = 50;

    public bool IsConfigWindowMovable { get; set; } = true;

    public void Save()
    {
        Plugin.PluginInterface.SavePluginConfig(this);
    }
}

using Dalamud.Configuration;
using System;

namespace SyncBarCleaner;

[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 1;

    public bool AutoEnableOnLoad { get; set; } = true;

    public bool EnableExpandedCrossHotbars { get; set; } = true;

    public bool IsConfigWindowMovable { get; set; } = true;

    public void Save()
    {
        Plugin.PluginInterface.SavePluginConfig(this);
    }
}

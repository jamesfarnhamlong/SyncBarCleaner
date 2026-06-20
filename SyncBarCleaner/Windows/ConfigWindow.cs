using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace SyncBarCleaner.Windows;

public class ConfigWindow : Window, IDisposable
{
    private readonly Plugin plugin;
    private readonly Configuration configuration;

    public ConfigWindow(Plugin plugin) : base("SyncBarCleaner Settings###SyncBarCleanerConfigV2")
    {
        this.plugin = plugin;
        configuration = plugin.Configuration;

        // Keep the window simple: collapsible disabled, but resizing and scrolling allowed.
        Flags = ImGuiWindowFlags.NoCollapse;

        // New window ID above resets the saved tiny size from the old config window.
        Size = new Vector2(520, 420);
        SizeCondition = ImGuiCond.FirstUseEver;

        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(420, 260),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };
    }

    public void Dispose()
    {
    }

    public override void PreDraw()
    {
        if (configuration.IsConfigWindowMovable)
        {
            Flags &= ~ImGuiWindowFlags.NoMove;
        }
        else
        {
            Flags |= ImGuiWindowFlags.NoMove;
        }
    }

    public override void Draw()
    {
        ImGui.TextWrapped("SyncBarCleaner visually hides level-synced actions that are not currently available.");
        ImGui.Spacing();

        var autoEnable = configuration.AutoEnableOnLoad;
        if (ImGui.Checkbox("Enable automatically on plugin load", ref autoEnable))
        {
            configuration.AutoEnableOnLoad = autoEnable;
            configuration.Save();
        }

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.TextWrapped("Apply to:");

        var main = configuration.EnableMainCrossHotbar;
        if (ImGui.Checkbox("Main cross hotbar", ref main))
        {
            configuration.EnableMainCrossHotbar = main;
            configuration.Save();
        }

        var expanded = configuration.EnableExpandedCrossHotbars;
        if (ImGui.Checkbox("Expanded/WXHB cross hotbars", ref expanded))
        {
            configuration.EnableExpandedCrossHotbars = expanded;
            configuration.Save();
        }

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.TextWrapped("Testing:");

        var testMode = configuration.EnableTestMode;
        if (ImGui.Checkbox("Enable fake level test", ref testMode))
        {
            configuration.EnableTestMode = testMode;
            configuration.Save();
        }

        var testLevel = configuration.TestEffectiveLevel;
        ImGui.SetNextItemWidth(120);
        if (ImGui.InputInt("Test effective level", ref testLevel))
        {
            configuration.TestEffectiveLevel = Math.Clamp(testLevel, 1, 100);
            configuration.Save();
        }

        ImGui.TextWrapped("When fake level test is enabled, the plugin behaves as if your effective level is the value above.");

        ImGui.Spacing();
        ImGui.Separator();

        if (ImGui.Button("Restore hidden icons now"))
        {
            plugin.RestoreFromConfigWindow();
        }

        ImGui.SameLine();

        var movable = configuration.IsConfigWindowMovable;
        if (ImGui.Checkbox("Movable window", ref movable))
        {
            configuration.IsConfigWindowMovable = movable;
            configuration.Save();
        }

        ImGui.Spacing();
        ImGui.TextWrapped("Commands: /syncbar status, /syncbar auto, /syncbar auto <level>, /syncbar auto off, /syncbar restore");
    }
}

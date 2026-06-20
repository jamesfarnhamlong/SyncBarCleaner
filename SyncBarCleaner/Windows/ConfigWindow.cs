using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace SyncBarCleaner.Windows;

public class ConfigWindow : Window, IDisposable
{
    private readonly Configuration configuration;

    public ConfigWindow(Plugin plugin) : base("SyncBarCleaner Settings###SyncBarCleanerConfig")
    {
        Flags = ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoScrollbar |
                ImGuiWindowFlags.NoScrollWithMouse;

        Size = new Vector2(420, 220);
        SizeCondition = ImGuiCond.FirstUseEver;

        configuration = plugin.Configuration;
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

        ImGui.TextWrapped("When enabled, SyncBarCleaner starts in real level-sync mode automatically.");

        ImGui.Spacing();

        var expanded = configuration.EnableExpandedCrossHotbars;
        if (ImGui.Checkbox("Enable expanded cross hotbar support", ref expanded))
        {
            configuration.EnableExpandedCrossHotbars = expanded;
            configuration.Save();
        }

        ImGui.TextWrapped("Controls hiding on expanded/WXHB cross hotbars. Turn this off if an unusual controller setup behaves incorrectly.");

        ImGui.Spacing();

        var movable = configuration.IsConfigWindowMovable;
        if (ImGui.Checkbox("Movable settings window", ref movable))
        {
            configuration.IsConfigWindowMovable = movable;
            configuration.Save();
        }

        ImGui.Separator();

        ImGui.TextWrapped("Commands:");
        ImGui.BulletText("/syncbar status");
        ImGui.BulletText("/syncbar auto");
        ImGui.BulletText("/syncbar auto <level>");
        ImGui.BulletText("/syncbar auto off");
        ImGui.BulletText("/syncbar restore");
    }
}

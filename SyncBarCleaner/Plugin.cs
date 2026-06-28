using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using FFXIVClientStructs.FFXIV.Component.GUI;
using SyncBarCleaner.Windows;
using System;
using static FFXIVClientStructs.FFXIV.Client.UI.Misc.RaptureHotbarModule;

namespace SyncBarCleaner;

/// <summary>
/// SyncBarCleaner hides actions that are above the player's current synced level
/// on controller cross hotbars.
///
/// Important safety rule:
/// This plugin does NOT edit hotbar data, clear slots, move actions, or save layouts.
/// It only changes the visibility/alpha of specific UI leaf nodes that belong to
/// already-rendered hotbar buttons.
///
/// Stable visual targets discovered during testing:
///   inner node 2, deep node 20 = action icon image
///   inner node 2, deep node 8  = small MP/value text
///
/// Supported visual addons:
///   _ActionCross        = current visible cross hotbar set, detected from the SET label
///   _ActionDoubleCrossL = expanded/WXHB left side, XHB2 slots 0-7
///   _ActionDoubleCrossR = expanded/WXHB right side, XHB2 slots 8-15
/// </summary>
public sealed class Plugin : IDalamudPlugin
{
    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] internal static IPlayerState PlayerState { get; private set; } = null!;
    [PluginService] internal static IDataManager DataManager { get; private set; } = null!;
    [PluginService] internal static IChatGui ChatGui { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;
    [PluginService] internal static IAddonLifecycle AddonLifecycle { get; private set; } = null!;

    private const string CommandName = "/syncbar";

    // RaptureHotbarModule cross hotbar IDs:
    //   SET 1 = 10, SET 2 = 11, ... SET 8 = 17.
    private const uint FirstCrossHotbarId = 10;
    private const uint ExpandedCrossHotbarId = 11; // XHB2.
    private const uint FallbackCrossHotbarId = FirstCrossHotbarId;

    private const int MinCrossHotbarSet = 1;
    private const int MaxCrossHotbarSet = 8;

    // The SET number in _ActionCross uses the FFXIV icon font.
    // Observed sequence: U+E090 = SET 1, U+E091 = SET 2, ..., U+E097 = SET 8.
    private const int CrossHotbarSetGlyphStart = 0xE090;

    // Stable action-button leaf nodes.
    // Do not hide the whole button/component: that caused black-box/cache artefacts.
    private const uint ButtonInnerNodeId = 2;
    private const uint ActionIconDeepNodeId = 20;
    private const uint ValueTextDeepNodeId = 8;

    // Main mode state.
    // autoHideTestLevel == 0 means "real level sync only".
    // autoHideTestLevel > 0 means fake-test as that effective level.
    private bool autoHideEnabled = false;
    private int autoHideTestLevel = 0;

    // Restores are queued because the native hotbar addons may not exist/update
    // at the exact moment the command is typed. We restore when each addon next
    // reaches PostUpdate/PreDraw.
    private bool restoreMainCrossOnce = false;
    private bool restoreDoubleCrossLOnce = false;
    private bool restoreDoubleCrossROnce = false;

    public Configuration Configuration { get; init; }

    private readonly WindowSystem WindowSystem = new("SyncBarCleaner");
    private ConfigWindow ConfigWindow { get; init; }

    public Plugin()
    {
        Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();

        ConfigWindow = new ConfigWindow(this);
        WindowSystem.AddWindow(ConfigWindow);

        CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "SyncBarCleaner: /syncbar auto, /syncbar auto <level>, /syncbar auto off, /syncbar restore, /syncbar status"
        });

        // We hook both events so our visual changes survive normal hotbar redraws.
        AddonLifecycle.RegisterListener(AddonEvent.PostUpdate, OnAnyAddonPostUpdate);
        AddonLifecycle.RegisterListener(AddonEvent.PreDraw, OnAnyAddonPostUpdate);

        // Auto-enable real level-sync mode when the plugin loads.
        // This behaves like typing /syncbar auto, not /syncbar auto <level>.
        autoHideEnabled = Configuration.AutoEnableOnLoad;
        autoHideTestLevel = 0;

        PluginInterface.UiBuilder.Draw += WindowSystem.Draw;
        PluginInterface.UiBuilder.OpenConfigUi += ToggleConfigUi;
        PluginInterface.UiBuilder.OpenMainUi += ToggleConfigUi;

        Log.Information($"=== {PluginInterface.Manifest.Name} loaded ===");
    }

    public void Dispose()
    {
        PluginInterface.UiBuilder.Draw -= WindowSystem.Draw;
        PluginInterface.UiBuilder.OpenConfigUi -= ToggleConfigUi;
        PluginInterface.UiBuilder.OpenMainUi -= ToggleConfigUi;

        AddonLifecycle.UnregisterListener(OnAnyAddonPostUpdate);

        // Stop applying hide logic. Queueing restore here is mostly defensive;
        // after unregistering the lifecycle listener it may not run immediately,
        // but it keeps state safe if disposal order changes later.
        autoHideEnabled = false;
        autoHideTestLevel = 0;
        QueueRestore();

        WindowSystem.RemoveAllWindows();
        ConfigWindow.Dispose();

        CommandManager.RemoveHandler(CommandName);
    }

    private unsafe void OnAnyAddonPostUpdate(AddonEvent type, AddonArgs args)
    {
        // First process one-shot restores, if requested.
        if (restoreMainCrossOnce && args.AddonName == "_ActionCross")
        {
            restoreMainCrossOnce = false;
            RestoreMainCross(args);
        }

        if (restoreDoubleCrossLOnce && args.AddonName == "_ActionDoubleCrossL")
        {
            restoreDoubleCrossLOnce = false;
            RestoreDoubleCross(args);
        }

        if (restoreDoubleCrossROnce && args.AddonName == "_ActionDoubleCrossR")
        {
            restoreDoubleCrossROnce = false;
            RestoreDoubleCross(args);
        }

        if (!autoHideEnabled)
            return;

        if (Configuration.EnableMainCrossHotbar && args.AddonName == "_ActionCross")
        {
            ApplyAutoHideActionCross(args);
            return;
        }

        // Expanded/WXHB display of XHB2:
        //   _ActionDoubleCrossL = XHB2 slots 0-7
        //   _ActionDoubleCrossR = XHB2 slots 8-15
        if (Configuration.EnableExpandedCrossHotbars && args.AddonName == "_ActionDoubleCrossL")
        {
            ApplyAutoHideDoubleCross(args, 0);
            return;
        }

        if (Configuration.EnableExpandedCrossHotbars && args.AddonName == "_ActionDoubleCrossR")
        {
            ApplyAutoHideDoubleCross(args, 8);
            return;
        }
    }

    private void OnCommand(string command, string args)
    {
        args = args.Trim();

        if (args.Length == 0 || args.Equals("status", StringComparison.OrdinalIgnoreCase))
        {
            PrintStatus();
            return;
        }

        if (args.Equals("restore", StringComparison.OrdinalIgnoreCase))
        {
            autoHideEnabled = false;
            autoHideTestLevel = 0;
            QueueRestore();

            ChatGui.Print("[SyncBarCleaner] Restore queued. Hold/use your main and expanded cross hotbars to refresh them.");
            return;
        }

        if (args.StartsWith("auto", StringComparison.OrdinalIgnoreCase))
        {
            HandleAutoCommand(args);
            return;
        }

        ChatGui.Print("[SyncBarCleaner] Commands: /syncbar auto, /syncbar auto <level>, /syncbar auto off, /syncbar restore, /syncbar status");
    }

    private void HandleAutoCommand(string args)
    {
        var parts = args.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length >= 2 && parts[1].Equals("off", StringComparison.OrdinalIgnoreCase))
        {
            autoHideEnabled = false;
            autoHideTestLevel = 0;
            QueueRestore();

            ChatGui.Print("[SyncBarCleaner] Auto-hide OFF. Hold/use your main and expanded cross hotbars to restore hidden buttons.");
            return;
        }

        autoHideEnabled = true;
        autoHideTestLevel = 0;

        if (parts.Length >= 2 && int.TryParse(parts[1], out var fakeLevel))
            autoHideTestLevel = Math.Clamp(fakeLevel, 1, 100);

        ChatGui.Print(
            autoHideTestLevel > 0
                ? $"[SyncBarCleaner] Auto-hide ON. TEST_EFFECTIVE={autoHideTestLevel}"
                : "[SyncBarCleaner] Auto-hide ON. Using real level sync only."
        );
    }

    private void PrintStatus()
    {
        var mode = !autoHideEnabled
            ? "OFF"
            : autoHideTestLevel > 0
                ? $"ON, command test effective level {autoHideTestLevel}"
                : Configuration.EnableTestMode
                    ? $"ON, settings test effective level {Math.Clamp(Configuration.TestEffectiveLevel, 1, 100)}"
                    : "ON, real level sync";

        ChatGui.Print(
            $"[SyncBarCleaner] Auto-hide={mode}. " +
            $"MainXHB={Configuration.EnableMainCrossHotbar}, " +
            $"ExpandedXHB={Configuration.EnableExpandedCrossHotbars}, " +
            $"Synced={PlayerState.IsLevelSynced}, " +
            $"Level={PlayerState.Level}, " +
            $"Effective={PlayerState.EffectiveLevel}"
        );
    }

    private bool TryGetEffectiveLevel(out int effectiveLevel)
    {
        if (autoHideTestLevel > 0)
        {
            effectiveLevel = Math.Clamp(autoHideTestLevel, 1, 100);
            return true;
        }

        if (Configuration.EnableTestMode)
        {
            effectiveLevel = Math.Clamp(Configuration.TestEffectiveLevel, 1, 100);
            return true;
        }

        effectiveLevel = PlayerState.EffectiveLevel;
        return PlayerState.IsLevelSynced;
    }

    private void QueueRestore()
    {
        restoreMainCrossOnce = true;
        restoreDoubleCrossLOnce = true;
        restoreDoubleCrossROnce = true;
    }

    public void RestoreFromConfigWindow()
    {
        autoHideEnabled = false;
        autoHideTestLevel = 0;
        QueueRestore();

        ChatGui.Print("[SyncBarCleaner] Auto-hide OFF. Restore queued from settings. Hold/use your main and expanded cross hotbars to refresh them.");
    }

    private unsafe void RestoreMainCross(AddonArgs args)
    {
        for (uint slotId = 0; slotId < 16; slotId++)
        {
            GetMainCrossVisualSlot(slotId, out var parentNodeId, out var childNodeId);
            RestoreButtonIconAndText(args, parentNodeId, childNodeId);
        }
    }

    private unsafe void RestoreDoubleCross(AddonArgs args)
    {
        for (uint visibleSlotId = 0; visibleSlotId < 8; visibleSlotId++)
        {
            GetDoubleCrossVisualSlot(visibleSlotId, out var parentNodeId, out var childNodeId);
            RestoreButtonIconAndText(args, parentNodeId, childNodeId);
        }
    }

    private unsafe void ApplyAutoHideActionCross(AddonArgs args)
    {
        var hotbarModule = RaptureHotbarModule.Instance();

        if (hotbarModule == null || !hotbarModule->ModuleReady)
            return;

        var actionSheet = DataManager.GetExcelSheet<Lumina.Excel.Sheets.Action>();

        if (actionSheet == null)
            return;

        var shouldApply = TryGetEffectiveLevel(out var effectiveLevel);

        // _ActionCross can display different sets depending on controller input mode.
        // Read the visible SET label so we compare each visual slot with the correct
        // underlying cross hotbar data.
        var hotbarId = GetVisibleActionCrossHotbarId(args);

        for (uint slotId = 0; slotId < 16; slotId++)
        {
            var shouldHide = shouldApply && ShouldHideHotbarSlot(
                hotbarModule,
                actionSheet,
                hotbarId,
                slotId,
                effectiveLevel
            );

            GetMainCrossVisualSlot(slotId, out var parentNodeId, out var childNodeId);
            SetButtonHidden(args, parentNodeId, childNodeId, shouldHide);
        }
    }

    private unsafe void ApplyAutoHideDoubleCross(AddonArgs args, uint slotOffset)
    {
        var hotbarModule = RaptureHotbarModule.Instance();

        if (hotbarModule == null || !hotbarModule->ModuleReady)
            return;

        var actionSheet = DataManager.GetExcelSheet<Lumina.Excel.Sheets.Action>();

        if (actionSheet == null)
            return;

        var shouldApply = TryGetEffectiveLevel(out var effectiveLevel);

        for (uint visibleSlotId = 0; visibleSlotId < 8; visibleSlotId++)
        {
            var slotId = slotOffset + visibleSlotId;

            var shouldHide = shouldApply && ShouldHideHotbarSlot(
                hotbarModule,
                actionSheet,
                ExpandedCrossHotbarId,
                slotId,
                effectiveLevel
            );

            GetDoubleCrossVisualSlot(visibleSlotId, out var parentNodeId, out var childNodeId);
            SetButtonHidden(args, parentNodeId, childNodeId, shouldHide);
        }
    }

    private unsafe uint GetVisibleActionCrossHotbarId(AddonArgs args)
    {
        if (args.Addon.IsNull)
            return FallbackCrossHotbarId;

        var addon = (AtkUnitBase*)args.Addon.Address;

        if (addon == null)
            return FallbackCrossHotbarId;

        var nodeList = addon->UldManager.NodeList;
        var count = addon->UldManager.NodeListCount;

        if (nodeList == null || count == 0)
            return FallbackCrossHotbarId;

        for (var i = 0; i < count; i++)
        {
            var node = nodeList[i];

            if (node == null)
                continue;

            // From the earlier text dump:
            //   root[025] id=19 text='Set'
            //   root[024] id=20 text='<FFXIV set-number glyph>'
            if (node->NodeId != 20)
                continue;

            if (!node->Type.ToString().Equals("Text", StringComparison.OrdinalIgnoreCase))
                continue;

            var textNode = (AtkTextNode*)node;
            var setNumber = ParseCrossHotbarSetNumber(textNode->NodeText.ToString());

            if (setNumber is >= MinCrossHotbarSet and <= MaxCrossHotbarSet)
                return FirstCrossHotbarId + (uint)(setNumber - 1);
        }

        return FallbackCrossHotbarId;
    }

    private static int ParseCrossHotbarSetNumber(string text)
    {
        if (string.IsNullOrEmpty(text))
            return 0;

        foreach (var ch in text)
        {
            // Normal digits, just in case some UI/font state exposes them directly.
            if (ch is >= '1' and <= '8')
                return ch - '0';

            var code = (int)ch;

            if (code is >= CrossHotbarSetGlyphStart and < CrossHotbarSetGlyphStart + MaxCrossHotbarSet)
                return code - CrossHotbarSetGlyphStart + 1;
        }

        return 0;
    }

    private unsafe bool ShouldHideHotbarSlot(
        RaptureHotbarModule* hotbarModule,
        Lumina.Excel.ExcelSheet<Lumina.Excel.Sheets.Action> actionSheet,
        uint hotbarId,
        uint slotId,
        int effectiveLevel
    )
    {
        var slot = hotbarModule->GetSlotById(hotbarId, slotId);

        if (slot == null)
            return false;

        // Only hide normal PvE actions.
        // Items, macros, mounts, gearsets, duty actions, etc. are deliberately ignored.
        if (slot->CommandType != HotbarSlotType.Action)
            return false;

        var displayType = slot->CommandType;
        var displayActionId = slot->CommandId;
        ushort unk = 0;

        // Resolve the displayed action. This matters for actions that upgrade,
        // transform, or share a hotbar slot appearance.
        RaptureHotbarModule.GetSlotAppearance(
            &displayType,
            &displayActionId,
            &unk,
            hotbarModule,
            slot
        );

        if (displayType != HotbarSlotType.Action)
            return false;

        var action = actionSheet.GetRow(displayActionId);

        if (action.RowId == 0)
            return false;

        int requiredLevel = action.ClassJobLevel;

        return requiredLevel > effectiveLevel;
    }

    private unsafe void SetButtonHidden(AddonArgs args, uint parentNodeId, uint childNodeId, bool hidden)
    {
        var alpha = hidden ? (byte)0 : (byte)255;

        ApplyAlphaToDeepLeafNode(args, parentNodeId, childNodeId, ButtonInnerNodeId, ActionIconDeepNodeId, alpha);
        ApplyAlphaToDeepLeafNode(args, parentNodeId, childNodeId, ButtonInnerNodeId, ValueTextDeepNodeId, alpha);
    }

    private unsafe void RestoreButtonIconAndText(AddonArgs args, uint parentNodeId, uint childNodeId)
    {
        ApplyAlphaToDeepLeafNode(args, parentNodeId, childNodeId, ButtonInnerNodeId, ActionIconDeepNodeId, 255);
        ApplyAlphaToDeepLeafNode(args, parentNodeId, childNodeId, ButtonInnerNodeId, ValueTextDeepNodeId, 255);
    }

    private void GetMainCrossVisualSlot(uint slotId, out uint parentNodeId, out uint childNodeId)
    {
        // _ActionCross parent block mapping:
        //   33 = S00-S03
        //   34 = S04-S07
        //   35 = S08-S11
        //   36 = S12-S15
        parentNodeId = 33 + (slotId / 4);

        childNodeId = GetCrossButtonChildNodeId(slotId);
    }

    private void GetDoubleCrossVisualSlot(uint visibleSlotId, out uint parentNodeId, out uint childNodeId)
    {
        // _ActionDoubleCrossL/R parent mapping:
        //   5 = visible slots 0-3
        //   6 = visible slots 4-7
        parentNodeId = 5 + (visibleSlotId / 4);

        childNodeId = GetCrossButtonChildNodeId(visibleSlotId);
    }

    private static uint GetCrossButtonChildNodeId(uint slotId)
    {
        // Button mapping inside each parent block:
        //   +0 = left   = child 2
        //   +1 = top    = child 3
        //   +2 = right  = child 4
        //   +3 = bottom = child 5
        return (slotId % 4) switch
        {
            0 => 2,
            1 => 3,
            2 => 4,
            3 => 5,
            _ => 2,
        };
    }

    private unsafe void ApplyAlphaToDeepLeafNode(
        AddonArgs args,
        uint parentNodeId,
        uint childNodeId,
        uint innerNodeId,
        uint deepNodeId,
        byte alpha
    )
    {
        if (args.Addon.IsNull)
            return;

        var addon = (AtkUnitBase*)args.Addon.Address;
        if (addon == null)
            return;

        var addonNodeList = addon->UldManager.NodeList;
        var addonCount = addon->UldManager.NodeListCount;

        if (addonNodeList == null || addonCount == 0)
            return;

        // Find the top-level parent block in the addon.
        AtkResNode* parentNode = null;

        for (var i = 0; i < addonCount; i++)
        {
            var node = addonNodeList[i];
            if (node == null)
                continue;

            if (node->NodeId == parentNodeId)
            {
                parentNode = node;
                break;
            }
        }

        if (parentNode == null || (int)parentNode->Type < 1000)
            return;

        var parentComponent = ((AtkComponentNode*)parentNode)->Component;
        if (parentComponent == null)
            return;

        var childNodeList = parentComponent->UldManager.NodeList;
        var childCount = parentComponent->UldManager.NodeListCount;

        if (childNodeList == null || childCount == 0)
            return;

        // Find the specific button node inside that parent block.
        AtkResNode* buttonNode = null;

        for (var i = 0; i < childCount; i++)
        {
            var child = childNodeList[i];
            if (child == null)
                continue;

            if (child->NodeId == childNodeId)
            {
                buttonNode = child;
                break;
            }
        }

        if (buttonNode == null || (int)buttonNode->Type < 1000)
            return;

        var buttonComponent = ((AtkComponentNode*)buttonNode)->Component;
        if (buttonComponent == null)
            return;

        var innerNodeList = buttonComponent->UldManager.NodeList;
        var innerCount = buttonComponent->UldManager.NodeListCount;

        if (innerNodeList == null || innerCount == 0)
            return;

        // Find the inner button component that contains the icon/text leaves.
        AtkResNode* innerNode = null;

        for (var i = 0; i < innerCount; i++)
        {
            var inner = innerNodeList[i];
            if (inner == null)
                continue;

            if (inner->NodeId == innerNodeId)
            {
                innerNode = inner;
                break;
            }
        }

        if (innerNode == null || (int)innerNode->Type < 1000)
            return;

        var innerComponent = ((AtkComponentNode*)innerNode)->Component;
        if (innerComponent == null)
            return;

        var deepNodeList = innerComponent->UldManager.NodeList;
        var deepCount = innerComponent->UldManager.NodeListCount;

        if (deepNodeList == null || deepCount == 0)
            return;

        // Finally, touch only the exact leaf node requested.
        // No recursion, no broad component visibility changes.
        for (var i = 0; i < deepCount; i++)
        {
            var deep = deepNodeList[i];
            if (deep == null)
                continue;

            if (deep->NodeId != deepNodeId)
                continue;

            deep->Alpha_2 = alpha;
            deep->ToggleVisibility(alpha > 0);
            return;
        }
    }

    public void ToggleConfigUi() => ConfigWindow.Toggle();
}

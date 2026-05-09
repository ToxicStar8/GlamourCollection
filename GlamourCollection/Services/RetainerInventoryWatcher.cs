using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Client.Game;
using System;

namespace Main.Services;

public unsafe sealed class RetainerInventoryWatcher : IDisposable
{
    private static readonly string[] RetainerInventoryAddons =
    [
        "InventoryRetainer",
        "InventoryRetainerLarge",
    ];

    private static readonly string[] RetainerMenuAddons =
    [
        "SelectString",
    ];

    private const int ScanDelayFrames = 8;
    private const int SpeculativeScanDelayFrames = 24;

    private int framesUntilScan;
    private bool pendingScanIsSpeculative;
    private string pendingScanReason = string.Empty;

    public string LastScanReason { get; private set; } = string.Empty;
    public bool LastScanWasSpeculative { get; private set; }

    public RetainerInventoryWatcher()
    {
        Svc.AddonLifecycle.RegisterListener(AddonEvent.PostOpen, RetainerInventoryAddons, OnRetainerInventoryAddon);
        Svc.AddonLifecycle.RegisterListener(AddonEvent.PostRefresh, RetainerInventoryAddons, OnRetainerInventoryAddon);
        Svc.AddonLifecycle.RegisterListener(AddonEvent.PostOpen, RetainerMenuAddons, OnRetainerMenuAddon);
        Svc.AddonLifecycle.RegisterListener(AddonEvent.PostRefresh, RetainerMenuAddons, OnRetainerMenuAddon);
    }

    public bool ConsumeScanRequest()
    {
        if (this.framesUntilScan <= 0)
            return false;

        this.framesUntilScan--;
        if (this.framesUntilScan > 0)
            return false;

        this.LastScanReason = this.pendingScanReason;
        this.LastScanWasSpeculative = this.pendingScanIsSpeculative;
        this.pendingScanReason = string.Empty;
        this.pendingScanIsSpeculative = false;
        return true;
    }

    public void ClearPending()
    {
        this.framesUntilScan = 0;
        this.LastScanReason = string.Empty;
        this.LastScanWasSpeculative = false;
        this.pendingScanReason = string.Empty;
        this.pendingScanIsSpeculative = false;
    }

    public void Dispose()
    {
        Svc.AddonLifecycle.UnregisterListener(AddonEvent.PostOpen, RetainerInventoryAddons, OnRetainerInventoryAddon);
        Svc.AddonLifecycle.UnregisterListener(AddonEvent.PostRefresh, RetainerInventoryAddons, OnRetainerInventoryAddon);
        Svc.AddonLifecycle.UnregisterListener(AddonEvent.PostOpen, RetainerMenuAddons, OnRetainerMenuAddon);
        Svc.AddonLifecycle.UnregisterListener(AddonEvent.PostRefresh, RetainerMenuAddons, OnRetainerMenuAddon);
    }

    private void OnRetainerInventoryAddon(AddonEvent type, AddonArgs args)
    {
        if (!Svc.ClientState.IsLoggedIn)
            return;

        this.ScheduleScan(ScanDelayFrames, "Retainer inventory scan.", false);
    }

    private void OnRetainerMenuAddon(AddonEvent type, AddonArgs args)
    {
        if (!Svc.ClientState.IsLoggedIn || !HasActiveRetainer())
            return;

        this.ScheduleScan(SpeculativeScanDelayFrames, "Retainer selected scan.", true);
    }

    private void ScheduleScan(int delayFrames, string reason, bool isSpeculative)
    {
        this.framesUntilScan = delayFrames;
        this.pendingScanReason = reason;
        this.pendingScanIsSpeculative = isSpeculative;
    }

    private static bool HasActiveRetainer()
    {
        var manager = RetainerManager.Instance();
        return manager != null && manager->GetActiveRetainer() != null;
    }
}

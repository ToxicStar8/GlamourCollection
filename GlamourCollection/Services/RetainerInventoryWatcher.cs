using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using ECommons.DalamudServices;
using System;

namespace Main.Services;

public sealed class RetainerInventoryWatcher : IDisposable
{
    private static readonly string[] RetainerInventoryAddons =
    [
        "InventoryRetainer",
        "InventoryRetainerLarge",
    ];

    private const int ScanDelayFrames = 8;

    private int framesUntilScan;

    public string LastScanReason { get; private set; } = string.Empty;

    public RetainerInventoryWatcher()
    {
        Svc.AddonLifecycle.RegisterListener(AddonEvent.PostOpen, RetainerInventoryAddons, OnRetainerInventoryAddon);
        Svc.AddonLifecycle.RegisterListener(AddonEvent.PostRefresh, RetainerInventoryAddons, OnRetainerInventoryAddon);
    }

    public bool ConsumeScanRequest()
    {
        if (this.framesUntilScan <= 0)
            return false;

        this.framesUntilScan--;
        if (this.framesUntilScan > 0)
            return false;

        this.LastScanReason = "Retainer inventory scan.";
        return true;
    }

    public void ClearPending()
    {
        this.framesUntilScan = 0;
        this.LastScanReason = string.Empty;
    }

    public void Dispose()
    {
        Svc.AddonLifecycle.UnregisterListener(AddonEvent.PostOpen, RetainerInventoryAddons, OnRetainerInventoryAddon);
        Svc.AddonLifecycle.UnregisterListener(AddonEvent.PostRefresh, RetainerInventoryAddons, OnRetainerInventoryAddon);
    }

    private void OnRetainerInventoryAddon(AddonEvent type, AddonArgs args)
    {
        if (!Svc.ClientState.IsLoggedIn)
            return;

        this.framesUntilScan = ScanDelayFrames;
    }
}

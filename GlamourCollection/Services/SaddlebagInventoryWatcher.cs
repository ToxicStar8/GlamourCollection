using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using ECommons.DalamudServices;
using System;

namespace Main.Services;

public sealed class SaddlebagInventoryWatcher : IDisposable
{
    private static readonly string[] SaddlebagAddons =
    [
        "InventoryBuddy",
    ];

    private const int ScanDelayFrames = 8;

    private int framesUntilScan;
    private string pendingScanReason = string.Empty;

    public string LastScanReason { get; private set; } = string.Empty;

    public SaddlebagInventoryWatcher()
    {
        Svc.AddonLifecycle.RegisterListener(AddonEvent.PostOpen, SaddlebagAddons, OnSaddlebagAddon);
        Svc.AddonLifecycle.RegisterListener(AddonEvent.PostRefresh, SaddlebagAddons, OnSaddlebagAddon);
    }

    public bool ConsumeScanRequest()
    {
        if (this.framesUntilScan <= 0)
            return false;

        this.framesUntilScan--;
        if (this.framesUntilScan > 0)
            return false;

        this.LastScanReason = this.pendingScanReason;
        this.pendingScanReason = string.Empty;
        return true;
    }

    public void ClearPending()
    {
        this.framesUntilScan = 0;
        this.LastScanReason = string.Empty;
        this.pendingScanReason = string.Empty;
    }

    public void Dispose()
    {
        Svc.AddonLifecycle.UnregisterListener(AddonEvent.PostOpen, SaddlebagAddons, OnSaddlebagAddon);
        Svc.AddonLifecycle.UnregisterListener(AddonEvent.PostRefresh, SaddlebagAddons, OnSaddlebagAddon);
    }

    private void OnSaddlebagAddon(AddonEvent type, AddonArgs args)
    {
        if (!Svc.ClientState.IsLoggedIn)
            return;

        this.ScheduleScan(ScanDelayFrames, "Saddlebag inventory scan.");
    }

    private void ScheduleScan(int delayFrames, string reason)
    {
        this.framesUntilScan = delayFrames;
        this.pendingScanReason = reason;
    }
}

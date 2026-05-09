using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using ECommons.DalamudServices;
using System;

namespace Main.Services;

public sealed class GlamourDresserInventoryWatcher : IDisposable
{
    private static readonly string[] GlamourDresserAddons =
    [
        "MiragePrismPrismBox",
    ];

    private const int ScanDelayFrames = 16;

    private int framesUntilScan;
    private string pendingScanReason = string.Empty;

    public string LastScanReason { get; private set; } = string.Empty;

    public GlamourDresserInventoryWatcher()
    {
        Svc.AddonLifecycle.RegisterListener(AddonEvent.PostOpen, GlamourDresserAddons, OnGlamourDresserAddon);
        Svc.AddonLifecycle.RegisterListener(AddonEvent.PostRefresh, GlamourDresserAddons, OnGlamourDresserAddon);
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
        Svc.AddonLifecycle.UnregisterListener(AddonEvent.PostOpen, GlamourDresserAddons, OnGlamourDresserAddon);
        Svc.AddonLifecycle.UnregisterListener(AddonEvent.PostRefresh, GlamourDresserAddons, OnGlamourDresserAddon);
    }

    private void OnGlamourDresserAddon(AddonEvent type, AddonArgs args)
    {
        if (!Svc.ClientState.IsLoggedIn)
            return;

        this.ScheduleScan(ScanDelayFrames, "幻化柜扫描。");
    }

    private void ScheduleScan(int delayFrames, string reason)
    {
        this.framesUntilScan = delayFrames;
        this.pendingScanReason = reason;
    }
}

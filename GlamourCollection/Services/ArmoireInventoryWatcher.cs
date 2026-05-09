using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using ECommons.DalamudServices;
using System;

namespace Main.Services;

public sealed class ArmoireInventoryWatcher : IDisposable
{
    private static readonly string[] ArmoireAddons =
    [
        "Cabinet",
        "CabinetWithdraw",
    ];

    private const int ScanDelayFrames = 60;
    private const int RetryDelayFrames = 30;
    private const int MaxScanAttempts = 20;

    private int framesUntilScan;
    private int remainingRetryAttempts;
    private string pendingScanReason = string.Empty;

    public string LastScanReason { get; private set; } = string.Empty;

    public ArmoireInventoryWatcher()
    {
        Svc.AddonLifecycle.RegisterListener(AddonEvent.PostOpen, ArmoireAddons, OnArmoireAddon);
        Svc.AddonLifecycle.RegisterListener(AddonEvent.PostRefresh, ArmoireAddons, OnArmoireAddon);
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

    public bool ScheduleRetry(string reason)
    {
        if (this.remainingRetryAttempts <= 0)
            return false;

        this.remainingRetryAttempts--;
        this.ScheduleScan(RetryDelayFrames, reason);
        return true;
    }

    public void ClearPending()
    {
        this.framesUntilScan = 0;
        this.remainingRetryAttempts = 0;
        this.LastScanReason = string.Empty;
        this.pendingScanReason = string.Empty;
    }

    public void Dispose()
    {
        Svc.AddonLifecycle.UnregisterListener(AddonEvent.PostOpen, ArmoireAddons, OnArmoireAddon);
        Svc.AddonLifecycle.UnregisterListener(AddonEvent.PostRefresh, ArmoireAddons, OnArmoireAddon);
    }

    private void OnArmoireAddon(AddonEvent type, AddonArgs args)
    {
        if (!Svc.ClientState.IsLoggedIn)
            return;

        this.remainingRetryAttempts = MaxScanAttempts - 1;
        this.ScheduleScan(ScanDelayFrames, "收藏柜扫描。");
    }

    private void ScheduleScan(int delayFrames, string reason)
    {
        this.framesUntilScan = delayFrames;
        this.pendingScanReason = reason;
    }
}

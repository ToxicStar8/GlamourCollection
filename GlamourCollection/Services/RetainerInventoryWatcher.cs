using Dalamud.Game.Inventory;
using Dalamud.Game.Inventory.InventoryEventArgTypes;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Client.Game;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Main.Services;

public unsafe sealed class RetainerInventoryWatcher : IDisposable
{
    private static readonly string[] RetainerInventoryAddons =
    [
        "InventoryRetainer",
        "InventoryRetainerLarge",
    ];

    private const int ScanDelayFrames = 2;
    private const int InventoryChangeScanDelayFrames = 15;
    private const int RetryDelayFrames = 8;
    private const int MaxScanAttempts = 20;

    private readonly Dictionary<ulong, PendingRetainerScan> pendingScans = [];
    private readonly Dictionary<ulong, int> remainingRetryAttempts = [];
    private ulong observedRetainerId;

    public string LastScanReason { get; private set; } = string.Empty;
    public bool LastScanWasSpeculative { get; private set; }
    public ulong LastScanRetainerId { get; private set; }

    public RetainerInventoryWatcher()
    {
        Svc.AddonLifecycle.RegisterListener(AddonEvent.PostOpen, RetainerInventoryAddons, OnRetainerInventoryAddon);
        Svc.GameInventory.InventoryChangedRaw += OnInventoryChangedRaw;
    }

    public void UpdateActiveRetainer()
    {
        var activeRetainerId = Svc.ClientState.IsLoggedIn ? GetActiveRetainerId() : 0;
        if (activeRetainerId == this.observedRetainerId)
            return;

        this.observedRetainerId = activeRetainerId;
        if (activeRetainerId == 0)
            return;

        this.remainingRetryAttempts[activeRetainerId] = MaxScanAttempts - 1;
        this.ScheduleScan(ScanDelayFrames, "雇员切换后扫描。", true, activeRetainerId);
    }

    public bool ConsumeScanRequest()
    {
        if (this.observedRetainerId == 0
            || !this.pendingScans.TryGetValue(this.observedRetainerId, out var pendingScan))
            return false;

        pendingScan.FramesUntilScan--;
        if (pendingScan.FramesUntilScan > 0)
            return false;

        this.pendingScans.Remove(this.observedRetainerId);
        this.LastScanReason = pendingScan.Reason;
        this.LastScanWasSpeculative = pendingScan.IsSpeculative;
        this.LastScanRetainerId = this.observedRetainerId;
        return true;
    }

    public bool ScheduleRetry(string reason, bool isSpeculative)
    {
        var retainerId = this.LastScanRetainerId;
        if (retainerId == 0
            || !this.remainingRetryAttempts.TryGetValue(retainerId, out var remainingAttempts)
            || remainingAttempts <= 0)
            return false;

        this.remainingRetryAttempts[retainerId] = remainingAttempts - 1;
        this.ScheduleScan(RetryDelayFrames, reason, isSpeculative, retainerId);
        return true;
    }

    public void MarkScanCompleted()
    {
        if (this.LastScanRetainerId != 0)
            this.remainingRetryAttempts.Remove(this.LastScanRetainerId);
    }

    public void ClearPending()
    {
        this.pendingScans.Clear();
        this.remainingRetryAttempts.Clear();
        this.LastScanReason = string.Empty;
        this.LastScanWasSpeculative = false;
        this.LastScanRetainerId = 0;
        this.observedRetainerId = 0;
    }

    public void Dispose()
    {
        Svc.AddonLifecycle.UnregisterListener(AddonEvent.PostOpen, RetainerInventoryAddons, OnRetainerInventoryAddon);
        Svc.GameInventory.InventoryChangedRaw -= OnInventoryChangedRaw;
    }

    private void OnRetainerInventoryAddon(AddonEvent type, AddonArgs args)
    {
        if (!Svc.ClientState.IsLoggedIn)
            return;

        var activeRetainerId = GetActiveRetainerId();
        if (activeRetainerId == 0)
            return;

        this.remainingRetryAttempts[activeRetainerId] = MaxScanAttempts - 1;
        this.ScheduleScan(ScanDelayFrames, "雇员库存扫描。", false, activeRetainerId);
    }

    private void OnInventoryChangedRaw(IReadOnlyCollection<InventoryEventArgs> events)
    {
        var activeRetainerId = GetActiveRetainerId();
        if (!Svc.ClientState.IsLoggedIn
            || activeRetainerId == 0
            || events.Count == 0
            || !events.Any(IsRetainerEvent))
            return;

        this.remainingRetryAttempts[activeRetainerId] = MaxScanAttempts - 1;
        this.ScheduleScan(InventoryChangeScanDelayFrames, "雇员库存变化扫描。", false, activeRetainerId);
    }

    private void ScheduleScan(int delayFrames, string reason, bool isSpeculative, ulong retainerId)
    {
        if (retainerId == 0)
            return;

        if (this.pendingScans.TryGetValue(retainerId, out var pendingScan))
        {
            pendingScan.FramesUntilScan = delayFrames;
            pendingScan.Reason = reason;
            pendingScan.IsSpeculative = isSpeculative;
            return;
        }

        this.pendingScans[retainerId] = new PendingRetainerScan(delayFrames, reason, isSpeculative);
    }

    private static ulong GetActiveRetainerId()
    {
        var manager = RetainerManager.Instance();
        var activeRetainer = manager == null ? null : manager->GetActiveRetainer();
        return activeRetainer == null ? 0 : activeRetainer->RetainerId;
    }

    private static bool IsRetainerEvent(InventoryEventArgs args)
    {
        if (InventoryScanner.IsRetainerContainer(args.Item.ContainerType))
            return true;

        if (args is InventoryItemChangedArgs changed
            && InventoryScanner.IsRetainerContainer(changed.OldItemState.ContainerType))
            return true;

        if (args is InventoryComplexEventArgs complex)
        {
            return InventoryScanner.IsRetainerContainer(complex.SourceInventory)
                   || InventoryScanner.IsRetainerContainer(complex.TargetInventory);
        }

        return false;
    }

    private sealed class PendingRetainerScan(int framesUntilScan, string reason, bool isSpeculative)
    {
        public int FramesUntilScan { get; set; } = framesUntilScan;
        public string Reason { get; set; } = reason;
        public bool IsSpeculative { get; set; } = isSpeculative;
    }
}

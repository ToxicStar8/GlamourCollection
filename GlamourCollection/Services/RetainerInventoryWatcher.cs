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

    private static readonly string[] RetainerMenuAddons =
    [
        "SelectString",
    ];

    private const int ScanDelayFrames = 2;
    private const int SpeculativeScanDelayFrames = 8;
    private const int InventoryChangeScanDelayFrames = 1;
    private const int RetryDelayFrames = 8;
    private const int MaxScanAttempts = 20;

    private int framesUntilScan;
    private int remainingRetryAttempts;
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
        Svc.GameInventory.InventoryChangedRaw += OnInventoryChangedRaw;
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

    public bool ScheduleRetry(string reason, bool isSpeculative)
    {
        if (this.remainingRetryAttempts <= 0)
            return false;

        this.remainingRetryAttempts--;
        this.ScheduleScan(RetryDelayFrames, reason, isSpeculative);
        return true;
    }

    public void MarkScanCompleted()
    {
        this.remainingRetryAttempts = 0;
    }

    public void ClearPending()
    {
        this.framesUntilScan = 0;
        this.remainingRetryAttempts = 0;
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
        Svc.GameInventory.InventoryChangedRaw -= OnInventoryChangedRaw;
    }

    private void OnRetainerInventoryAddon(AddonEvent type, AddonArgs args)
    {
        if (!Svc.ClientState.IsLoggedIn)
            return;

        this.remainingRetryAttempts = MaxScanAttempts - 1;
        this.ScheduleScan(ScanDelayFrames, "雇员库存扫描。", false);
    }

    private void OnRetainerMenuAddon(AddonEvent type, AddonArgs args)
    {
        if (!Svc.ClientState.IsLoggedIn || !HasActiveRetainer())
            return;

        this.remainingRetryAttempts = MaxScanAttempts - 1;
        this.ScheduleScan(SpeculativeScanDelayFrames, "雇员选择后扫描。", true);
    }

    private void OnInventoryChangedRaw(IReadOnlyCollection<InventoryEventArgs> events)
    {
        if (!Svc.ClientState.IsLoggedIn || events.Count == 0 || !events.Any(IsRetainerEvent))
            return;

        this.remainingRetryAttempts = MaxScanAttempts - 1;
        this.ScheduleScan(InventoryChangeScanDelayFrames, "雇员库存变化扫描。", false);
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
}

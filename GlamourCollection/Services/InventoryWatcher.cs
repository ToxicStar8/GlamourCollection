using Dalamud.Game.Inventory;
using Dalamud.Game.Inventory.InventoryEventArgTypes;
using ECommons.DalamudServices;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Main.Services;

public sealed class InventoryWatcher : IDisposable
{
    private const int RescanDelayFrames = 3;

    private int framesUntilRescan;
    private int pendingEventCount;

    public string LastChangeReason { get; private set; } = string.Empty;

    public InventoryWatcher()
    {
        Svc.GameInventory.InventoryChangedRaw += OnInventoryChangedRaw;
    }

    public bool ConsumeRescanRequest()
    {
        if (this.framesUntilRescan <= 0)
            return false;

        this.framesUntilRescan--;
        if (this.framesUntilRescan > 0)
            return false;

        this.LastChangeReason = $"库存变化（{this.pendingEventCount} 个事件）。";
        this.pendingEventCount = 0;
        return true;
    }

    public void ClearPending()
    {
        this.framesUntilRescan = 0;
        this.pendingEventCount = 0;
        this.LastChangeReason = string.Empty;
    }

    public void Dispose()
    {
        Svc.GameInventory.InventoryChangedRaw -= OnInventoryChangedRaw;
    }

    private void OnInventoryChangedRaw(IReadOnlyCollection<InventoryEventArgs> events)
    {
        if (!Svc.ClientState.IsLoggedIn || events.Count == 0)
            return;

        if (!events.Any(IsWatchedEvent))
            return;

        this.pendingEventCount += events.Count;
        this.framesUntilRescan = RescanDelayFrames;
    }

    private static bool IsWatchedEvent(InventoryEventArgs args)
    {
        if (InventoryScanner.IsPhaseOneContainer(args.Item.ContainerType))
            return true;

        if (args is InventoryItemChangedArgs changed
            && InventoryScanner.IsPhaseOneContainer(changed.OldItemState.ContainerType))
            return true;

        if (args is InventoryComplexEventArgs complex)
        {
            return InventoryScanner.IsPhaseOneContainer(complex.SourceInventory)
                   || InventoryScanner.IsPhaseOneContainer(complex.TargetInventory);
        }

        return false;
    }
}

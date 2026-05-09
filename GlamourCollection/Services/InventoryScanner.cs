using Dalamud.Game.Inventory;
using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Client.Game;
using Main.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Main.Services;

public unsafe sealed class InventoryScanner(ItemDatabaseService itemDatabase)
{
    private static readonly GameInventoryType[] PhaseOneContainers =
    [
        GameInventoryType.Inventory1,
        GameInventoryType.Inventory2,
        GameInventoryType.Inventory3,
        GameInventoryType.Inventory4,
        GameInventoryType.EquippedItems,
        GameInventoryType.ArmoryMainHand,
        GameInventoryType.ArmoryOffHand,
        GameInventoryType.ArmoryHead,
        GameInventoryType.ArmoryBody,
        GameInventoryType.ArmoryHands,
        GameInventoryType.ArmoryLegs,
        GameInventoryType.ArmoryFeets,
        GameInventoryType.ArmoryEar,
        GameInventoryType.ArmoryNeck,
        GameInventoryType.ArmoryWrist,
        GameInventoryType.ArmoryRings,
    ];

    private static readonly HashSet<GameInventoryType> PhaseOneContainerSet = PhaseOneContainers.ToHashSet();

    private static readonly GameInventoryType[] RetainerContainers =
    [
        GameInventoryType.RetainerPage1,
        GameInventoryType.RetainerPage2,
        GameInventoryType.RetainerPage3,
        GameInventoryType.RetainerPage4,
        GameInventoryType.RetainerPage5,
        GameInventoryType.RetainerPage6,
        GameInventoryType.RetainerPage7,
    ];

    private static readonly HashSet<GameInventoryType> RetainerContainerSet = RetainerContainers.ToHashSet();

    public static bool IsPhaseOneContainer(GameInventoryType type)
        => PhaseOneContainerSet.Contains(type);

    public static bool IsRetainerContainer(GameInventoryType type)
        => RetainerContainerSet.Contains(type);

    public IReadOnlyList<OwnedItemRecord> ScanPhaseOne(ulong characterId, uint worldId)
    {
        var now = DateTimeOffset.UtcNow;
        var records = new List<OwnedItemRecord>();

        itemDatabase.Load();

        foreach (var container in PhaseOneContainers)
        {
            foreach (var inventoryItem in Svc.GameInventory.GetInventoryItems(container))
            {
                if (inventoryItem.IsEmpty)
                    continue;

                var rawItemId = inventoryItem.ItemId;
                var baseItemId = inventoryItem.BaseItemId;
                if (!itemDatabase.TryGetEquipment(baseItemId, out var equipment))
                    continue;

                records.Add(new OwnedItemRecord
                {
                    RawItemId = rawItemId,
                    BaseItemId = baseItemId,
                    ItemId = baseItemId,
                    ItemName = equipment.Name,
                    Quantity = inventoryItem.Quantity,
                    IsHq = inventoryItem.IsHq,
                    SourceContainer = GetContainerLabel(inventoryItem.ContainerType),
                    ContainerType = inventoryItem.ContainerType.ToString(),
                    ContainerId = (ushort)inventoryItem.ContainerType,
                    Slot = inventoryItem.InventorySlot,
                    CharacterId = characterId,
                    WorldId = worldId,
                    UpdatedAt = now,
                });
            }
        }

        return records;
    }

    public RetainerInventorySnapshot ScanCurrentRetainer(ulong characterId, uint worldId)
    {
        var retainer = GetCurrentRetainer();
        var retainerName = NormalizeRetainerName(retainer.Name);
        var records = new List<OwnedItemRecord>();
        var now = DateTimeOffset.UtcNow;
        var hasReadableContainers = false;
        var nonEmptyItemCount = 0;

        itemDatabase.Load();

        foreach (var container in RetainerContainers)
        {
            var items = Svc.GameInventory.GetInventoryItems(container);
            if (!items.IsEmpty)
                hasReadableContainers = true;

            foreach (var inventoryItem in items)
            {
                if (inventoryItem.IsEmpty)
                    continue;

                nonEmptyItemCount++;
                var rawItemId = inventoryItem.ItemId;
                var baseItemId = inventoryItem.BaseItemId;
                if (!itemDatabase.TryGetEquipment(baseItemId, out var equipment))
                    continue;

                records.Add(new OwnedItemRecord
                {
                    RawItemId = rawItemId,
                    BaseItemId = baseItemId,
                    ItemId = baseItemId,
                    ItemName = equipment.Name,
                    Quantity = inventoryItem.Quantity,
                    IsHq = inventoryItem.IsHq,
                    SourceContainer = $"Retainer: {retainerName}",
                    ContainerType = inventoryItem.ContainerType.ToString(),
                    ContainerId = (ushort)inventoryItem.ContainerType,
                    Slot = inventoryItem.InventorySlot,
                    RetainerId = retainer.Id,
                    RetainerName = retainerName,
                    CharacterId = characterId,
                    WorldId = worldId,
                    UpdatedAt = now,
                });
            }
        }

        var itemCountMatches = retainer.ItemCount < 0 || nonEmptyItemCount == retainer.ItemCount;
        var isReadable = hasReadableContainers && itemCountMatches;
        return new RetainerInventorySnapshot(retainer.Id, retainerName, isReadable, records);
    }

    private static string GetContainerLabel(GameInventoryType type)
        => type switch
        {
            GameInventoryType.Inventory1 => "Inventory 1",
            GameInventoryType.Inventory2 => "Inventory 2",
            GameInventoryType.Inventory3 => "Inventory 3",
            GameInventoryType.Inventory4 => "Inventory 4",
            GameInventoryType.EquippedItems => "Equipped",
            GameInventoryType.ArmoryMainHand => "Armoury Main Hand",
            GameInventoryType.ArmoryOffHand => "Armoury Off Hand",
            GameInventoryType.ArmoryHead => "Armoury Head",
            GameInventoryType.ArmoryBody => "Armoury Body",
            GameInventoryType.ArmoryHands => "Armoury Hands",
            GameInventoryType.ArmoryLegs => "Armoury Legs",
            GameInventoryType.ArmoryFeets => "Armoury Feet",
            GameInventoryType.ArmoryEar => "Armoury Earrings",
            GameInventoryType.ArmoryNeck => "Armoury Necklace",
            GameInventoryType.ArmoryWrist => "Armoury Bracelet",
            GameInventoryType.ArmoryRings => "Armoury Rings",
            _ => type.ToString(),
        };

    private static RetainerIdentity GetCurrentRetainer()
    {
        var manager = RetainerManager.Instance();
        if (manager == null)
            return new RetainerIdentity(0, string.Empty, -1);

        var activeRetainer = manager->GetActiveRetainer();
        if (activeRetainer == null)
            return new RetainerIdentity(manager->LastSelectedRetainerId, string.Empty, -1);

        return new RetainerIdentity(activeRetainer->RetainerId, activeRetainer->NameString, activeRetainer->ItemCount);
    }

    private static string NormalizeRetainerName(string retainerName)
        => string.IsNullOrWhiteSpace(retainerName) ? "Unknown Retainer" : retainerName.Trim();

    private sealed record RetainerIdentity(ulong Id, string Name, int ItemCount);
}

public sealed record RetainerInventorySnapshot(
    ulong RetainerId,
    string RetainerName,
    bool IsReadable,
    IReadOnlyList<OwnedItemRecord> Records);

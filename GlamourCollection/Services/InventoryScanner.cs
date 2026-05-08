using Dalamud.Game.Inventory;
using ECommons.DalamudServices;
using Main.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Main.Services;

public sealed class InventoryScanner(ItemDatabaseService itemDatabase, Configuration configuration)
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

    public static bool IsPhaseOneContainer(GameInventoryType type)
        => PhaseOneContainerSet.Contains(type);

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
                    ItemId = GetConfiguredItemId(rawItemId, baseItemId),
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

    private uint GetConfiguredItemId(uint rawItemId, uint baseItemId)
        => (OwnershipMatchMode)configuration.OwnershipMatchMode switch
        {
            OwnershipMatchMode.RawItemId => rawItemId,
            _ => baseItemId,
        };

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
}

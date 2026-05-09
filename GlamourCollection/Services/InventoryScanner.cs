using Dalamud.Game.Inventory;
using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using Lumina.Excel.Sheets;
using Main.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using LuminaCabinet = Lumina.Excel.Sheets.Cabinet;

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

    private static readonly GameInventoryType[] SaddlebagContainers =
    [
        GameInventoryType.SaddleBag1,
        GameInventoryType.SaddleBag2,
    ];

    private static readonly GameInventoryType[] PremiumSaddlebagContainers =
    [
        GameInventoryType.PremiumSaddleBag1,
        GameInventoryType.PremiumSaddleBag2,
    ];

    private static readonly HashSet<GameInventoryType> SaddlebagContainerSet =
        SaddlebagContainers.Concat(PremiumSaddlebagContainers).ToHashSet();

    public static bool IsPhaseOneContainer(GameInventoryType type)
        => PhaseOneContainerSet.Contains(type);

    public static bool IsRetainerContainer(GameInventoryType type)
        => RetainerContainerSet.Contains(type);

    public static bool IsSaddlebagContainer(GameInventoryType type)
        => SaddlebagContainerSet.Contains(type);

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
                    SourceContainer = $"雇员: {retainerName}",
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

    public SaddlebagInventorySnapshot ScanSaddlebag(ulong characterId, uint worldId)
    {
        itemDatabase.Load();

        var now = DateTimeOffset.UtcNow;
        var saddlebag = this.ScanSaddlebagGroup(characterId, worldId, now, SaddlebagContainers, "陆行鸟背包");
        var premiumSaddlebag = this.ScanSaddlebagGroup(
            characterId,
            worldId,
            now,
            PremiumSaddlebagContainers,
            "高级陆行鸟背包");

        return new SaddlebagInventorySnapshot(
            saddlebag.IsReadable,
            premiumSaddlebag.IsReadable,
            saddlebag.Records.Concat(premiumSaddlebag.Records).ToList(),
            saddlebag.Records.Count,
            premiumSaddlebag.Records.Count);
    }

    private SaddlebagGroupSnapshot ScanSaddlebagGroup(
        ulong characterId,
        uint worldId,
        DateTimeOffset updatedAt,
        IReadOnlyList<GameInventoryType> containers,
        string sourceContainer)
    {
        var records = new List<OwnedItemRecord>();
        var hasReadableContainers = false;

        foreach (var container in containers)
        {
            var items = Svc.GameInventory.GetInventoryItems(container);
            if (!items.IsEmpty)
                hasReadableContainers = true;

            foreach (var inventoryItem in items)
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
                    SourceContainer = sourceContainer,
                    ContainerType = inventoryItem.ContainerType.ToString(),
                    ContainerId = (ushort)inventoryItem.ContainerType,
                    Slot = inventoryItem.InventorySlot,
                    CharacterId = characterId,
                    WorldId = worldId,
                    UpdatedAt = updatedAt,
                });
            }
        }

        return new SaddlebagGroupSnapshot(hasReadableContainers, records);
    }

    public GlamourDresserInventorySnapshot ScanGlamourDresser(ulong characterId, uint worldId)
    {
        var manager = MirageManager.Instance();
        if (manager == null || !manager->PrismBoxLoaded)
            return new GlamourDresserInventorySnapshot(false, []);

        itemDatabase.Load();

        var now = DateTimeOffset.UtcNow;
        var records = new List<OwnedItemRecord>();
        var setItems = Svc.Data.GetExcelSheet<MirageStoreSetItem>()
            .ToDictionary(item => item.RowId);
        var itemIds = manager->PrismBoxItemIds;

        for (var index = 0; index < itemIds.Length; index++)
        {
            var rawItemId = itemIds[index];
            if (rawItemId == 0)
                continue;

            if (this.TryAddExternalEquipmentRecord(
                    records,
                    rawItemId,
                    "幻化柜",
                    "GlamourDresser",
                    (uint)index,
                    characterId,
                    worldId,
                    now))
                continue;

            if (setItems.TryGetValue(rawItemId, out var setItem))
                this.AddGlamourDresserSetRecords(manager, (uint)index, setItem, records, characterId, worldId, now);
        }

        return new GlamourDresserInventorySnapshot(true, records);
    }

    public ArmoireInventorySnapshot ScanArmoire(ulong characterId, uint worldId)
    {
        var uiState = UIState.Instance();
        if (uiState == null)
            return new ArmoireInventorySnapshot(false, []);

        var cabinet = &uiState->Cabinet;
        if (!cabinet->IsCabinetLoaded())
            return new ArmoireInventorySnapshot(false, []);

        itemDatabase.Load();

        var now = DateTimeOffset.UtcNow;
        var records = new List<OwnedItemRecord>();

        foreach (var cabinetItem in Svc.Data.GetExcelSheet<LuminaCabinet>())
        {
            var rawItemId = cabinetItem.Item.RowId;
            var cabinetRowId = cabinetItem.RowId;
            if (rawItemId == 0 || (!cabinet->IsItemInCabinet(rawItemId) && !cabinet->IsItemInCabinet(cabinetRowId)))
                continue;

            this.TryAddExternalEquipmentRecord(
                records,
                rawItemId,
                "收藏柜",
                "Armoire",
                cabinetRowId,
                characterId,
                worldId,
                now);
        }

        return new ArmoireInventorySnapshot(true, records);
    }

    private void AddGlamourDresserSetRecords(
        MirageManager* manager,
        uint prismBoxIndex,
        MirageStoreSetItem setItem,
        List<OwnedItemRecord> records,
        ulong characterId,
        uint worldId,
        DateTimeOffset updatedAt)
    {
        foreach (var slot in GetMirageStoreSetSlots(setItem))
        {
            if (slot.ItemId == 0 || !manager->IsSetSlotUnlocked(prismBoxIndex, slot.SlotIndex))
                continue;

            this.TryAddExternalEquipmentRecord(
                records,
                slot.ItemId,
                "幻化柜",
                "GlamourDresser",
                (prismBoxIndex * 100) + (uint)slot.SlotIndex,
                characterId,
                worldId,
                updatedAt);
        }
    }

    private bool TryAddExternalEquipmentRecord(
        List<OwnedItemRecord> records,
        uint rawItemId,
        string sourceContainer,
        string containerType,
        uint slot,
        ulong characterId,
        uint worldId,
        DateTimeOffset updatedAt)
    {
        var baseItemId = NormalizeBaseItemId(rawItemId);
        if (!itemDatabase.TryGetEquipment(baseItemId, out var equipment))
            return false;

        records.Add(new OwnedItemRecord
        {
            RawItemId = rawItemId,
            BaseItemId = baseItemId,
            ItemId = baseItemId,
            ItemName = equipment.Name,
            Quantity = 1,
            IsHq = rawItemId != baseItemId,
            SourceContainer = sourceContainer,
            ContainerType = containerType,
            Slot = slot,
            CharacterId = characterId,
            WorldId = worldId,
            UpdatedAt = updatedAt,
        });

        return true;
    }

    private static IReadOnlyList<MirageStoreSetSlot> GetMirageStoreSetSlots(MirageStoreSetItem setItem)
        =>
        [
            new MirageStoreSetSlot(0, setItem.MainHand.RowId),
            new MirageStoreSetSlot(1, setItem.OffHand.RowId),
            new MirageStoreSetSlot(2, setItem.Head.RowId),
            new MirageStoreSetSlot(3, setItem.Body.RowId),
            new MirageStoreSetSlot(4, setItem.Hands.RowId),
            new MirageStoreSetSlot(5, setItem.Legs.RowId),
            new MirageStoreSetSlot(6, setItem.Feet.RowId),
            new MirageStoreSetSlot(7, setItem.Earrings.RowId),
            new MirageStoreSetSlot(8, setItem.Necklace.RowId),
            new MirageStoreSetSlot(9, setItem.Bracelets.RowId),
            new MirageStoreSetSlot(10, setItem.Ring.RowId),
        ];

    private static uint NormalizeBaseItemId(uint itemId)
    {
        const uint hqItemIdOffset = 1_000_000;
        return itemId > hqItemIdOffset ? itemId - hqItemIdOffset : itemId;
    }

    private static string GetContainerLabel(GameInventoryType type)
        => type switch
        {
            GameInventoryType.Inventory1 => "背包 1",
            GameInventoryType.Inventory2 => "背包 2",
            GameInventoryType.Inventory3 => "背包 3",
            GameInventoryType.Inventory4 => "背包 4",
            GameInventoryType.EquippedItems => "已装备",
            GameInventoryType.ArmoryMainHand => "军械库 主手",
            GameInventoryType.ArmoryOffHand => "军械库 副手",
            GameInventoryType.ArmoryHead => "军械库 头部",
            GameInventoryType.ArmoryBody => "军械库 身体",
            GameInventoryType.ArmoryHands => "军械库 手部",
            GameInventoryType.ArmoryLegs => "军械库 腿部",
            GameInventoryType.ArmoryFeets => "军械库 脚部",
            GameInventoryType.ArmoryEar => "军械库 耳饰",
            GameInventoryType.ArmoryNeck => "军械库 项链",
            GameInventoryType.ArmoryWrist => "军械库 手镯",
            GameInventoryType.ArmoryRings => "军械库 戒指",
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
        => string.IsNullOrWhiteSpace(retainerName) ? "未知雇员" : retainerName.Trim();

    private sealed record RetainerIdentity(ulong Id, string Name, int ItemCount);

    private readonly record struct MirageStoreSetSlot(int SlotIndex, uint ItemId);
}

public sealed record RetainerInventorySnapshot(
    ulong RetainerId,
    string RetainerName,
    bool IsReadable,
    IReadOnlyList<OwnedItemRecord> Records);

public sealed record SaddlebagInventorySnapshot(
    bool IsSaddlebagReadable,
    bool IsPremiumSaddlebagReadable,
    IReadOnlyList<OwnedItemRecord> Records,
    int SaddlebagRecordCount,
    int PremiumSaddlebagRecordCount)
{
    public bool IsReadable => this.IsSaddlebagReadable || this.IsPremiumSaddlebagReadable;
}

internal sealed record SaddlebagGroupSnapshot(
    bool IsReadable,
    IReadOnlyList<OwnedItemRecord> Records);

public sealed record GlamourDresserInventorySnapshot(
    bool IsReadable,
    IReadOnlyList<OwnedItemRecord> Records);

public sealed record ArmoireInventorySnapshot(
    bool IsReadable,
    IReadOnlyList<OwnedItemRecord> Records);

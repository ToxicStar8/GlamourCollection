using Main.Models;
using System.Collections.Generic;
using System.Linq;

namespace Main.Services;

public sealed class OwnershipService(ItemDatabaseService itemDatabase, OwnedItemRepository repository, Configuration configuration)
{
    private readonly List<EquipmentViewModel> viewModels = [];

    public IReadOnlyList<EquipmentViewModel> ViewModels => this.viewModels;

    public int OwnedItemCount => this.viewModels.Count(item => item.IsOwned);

    public int Version { get; private set; }

    public void Refresh()
    {
        itemDatabase.Load();

        var displayMode = (EquipmentDisplayMode)configuration.EquipmentDisplayMode;
        var appearanceMatchMode = (EquipmentAppearanceMatchMode)configuration.EquipmentAppearanceMatchMode;
        const OwnedLocationMode locationMode = OwnedLocationMode.AllLocations;
        var ownedByItemId = repository.Records
            .GroupBy(GetBaseItemId)
            .ToDictionary(group => group.Key, group => (IReadOnlyList<OwnedItemRecord>)group.ToList());

        this.viewModels.Clear();

        if (displayMode == EquipmentDisplayMode.ByAppearanceModel)
        {
            var appearanceItemsByKey = itemDatabase.Equipment
                .GroupBy(item => item.GetAppearanceKey(appearanceMatchMode))
                .ToDictionary(group => group.Key, group => (IReadOnlyList<EquipmentRecord>)group.ToList());

            this.viewModels.AddRange(appearanceItemsByKey.Values
                .Select(group => CreateAppearanceViewModel(group, ownedByItemId, locationMode))
                .OrderBy(item => item.Item.Name));
        }
        else
        {
            this.viewModels.AddRange(itemDatabase.Equipment
                .Select(item => CreateItemViewModel(item, ownedByItemId, locationMode)));
        }

        this.Version++;
    }

    public bool RefreshEquipmentData(IEnumerable<uint> itemIds)
    {
        var itemIdSet = itemIds.Where(itemId => itemId != 0).ToHashSet();
        if (itemIdSet.Count == 0)
            return false;

        var changed = false;
        for (var index = 0; index < this.viewModels.Count; index++)
        {
            var viewModel = this.viewModels[index];
            if (!viewModel.AppearanceItems.Any(item => itemIdSet.Contains(item.ItemId)))
                continue;

            var appearanceChanged = false;
            var updatedAppearanceItems = viewModel.AppearanceItems
                .Select(item =>
                {
                    if (!itemIdSet.Contains(item.ItemId) || !itemDatabase.TryGetEquipment(item.ItemId, out var updated))
                        return item;

                    appearanceChanged = true;
                    return updated;
                })
                .ToList();

            var updatedRepresentative = viewModel.Item;
            if (itemIdSet.Contains(viewModel.Item.ItemId)
                && itemDatabase.TryGetEquipment(viewModel.Item.ItemId, out var representative))
            {
                updatedRepresentative = representative;
                appearanceChanged = true;
            }

            if (!appearanceChanged)
                continue;

            this.viewModels[index] = viewModel with
            {
                Item = updatedRepresentative,
                AppearanceItems = updatedAppearanceItems,
            };
            changed = true;
        }

        if (changed)
            this.Version++;

        return changed;
    }

    public void RefreshOwnedLocations()
    {
        var ownedByItemId = repository.Records
            .GroupBy(GetBaseItemId)
            .ToDictionary(group => group.Key, group => (IReadOnlyList<OwnedItemRecord>)group.ToList());
        const OwnedLocationMode locationMode = OwnedLocationMode.AllLocations;

        for (var index = 0; index < this.viewModels.Count; index++)
        {
            var viewModel = this.viewModels[index];
            if (!viewModel.IsOwned)
                continue;

            this.viewModels[index] = viewModel.IsAppearanceGroup
                ? CreateAppearanceViewModel(viewModel.AppearanceItems, ownedByItemId, locationMode)
                : CreateItemViewModel(viewModel.Item, ownedByItemId, locationMode);
        }

        this.Version++;
    }

    private static EquipmentViewModel CreateItemViewModel(
        EquipmentRecord item,
        IReadOnlyDictionary<uint, IReadOnlyList<OwnedItemRecord>> ownedByItemId,
        OwnedLocationMode locationMode)
    {
        ownedByItemId.TryGetValue(item.ItemId, out var locations);
        return CreateViewModel(item, locations ?? [], [item], locationMode, isAppearanceGroup: false);
    }

    private static EquipmentViewModel CreateAppearanceViewModel(
        IReadOnlyList<EquipmentRecord> appearanceItems,
        IReadOnlyDictionary<uint, IReadOnlyList<OwnedItemRecord>> ownedByItemId,
        OwnedLocationMode locationMode)
    {
        var sortedItems = appearanceItems.OrderBy(item => item.Name).ToList();
        var ownedItems = new List<EquipmentRecord>();
        var ownedLocations = new List<OwnedItemRecord>();
        var hasNormalQuality = false;
        var hasHighQuality = false;

        foreach (var item in sortedItems)
        {
            if (!ownedByItemId.TryGetValue(item.ItemId, out var locations) || locations.Count == 0)
                continue;

            ownedItems.Add(item);
            ownedLocations.AddRange(locations);
            hasNormalQuality |= locations.Any(location => !location.IsHq);
            hasHighQuality |= locations.Any(location => location.IsHq);
        }

        if (locationMode == OwnedLocationMode.FirstLocationOnly && ownedLocations.Count > 1)
            ownedLocations = ownedLocations.Take(1).ToList();

        var representative = ownedItems.Count > 0 ? ownedItems[0] : sortedItems[0];
        return new EquipmentViewModel(
            representative,
            ownedLocations.Count > 0,
            ownedLocations,
            sortedItems,
            true,
            hasNormalQuality,
            hasHighQuality);
    }

    private static uint GetBaseItemId(OwnedItemRecord item)
    {
        if (item.BaseItemId != 0)
            return item.BaseItemId;

        if (item.ItemId != 0)
            return NormalizeBaseItemId(item.ItemId);

        return NormalizeBaseItemId(item.RawItemId);
    }

    private static EquipmentViewModel CreateViewModel(
        EquipmentRecord item,
        IReadOnlyList<OwnedItemRecord> allLocations,
        IReadOnlyList<EquipmentRecord> appearanceItems,
        OwnedLocationMode locationMode,
        bool isAppearanceGroup)
    {
        var locations = locationMode == OwnedLocationMode.FirstLocationOnly
            ? allLocations.Take(1).ToList()
            : allLocations;

        return new EquipmentViewModel(
            item,
            allLocations.Count > 0,
            locations,
            appearanceItems,
            isAppearanceGroup,
            allLocations.Any(location => !location.IsHq),
            allLocations.Any(location => location.IsHq));
    }

    private static uint NormalizeBaseItemId(uint itemId)
    {
        const uint hqItemIdOffset = 1_000_000;
        return itemId > hqItemIdOffset ? itemId - hqItemIdOffset : itemId;
    }
}

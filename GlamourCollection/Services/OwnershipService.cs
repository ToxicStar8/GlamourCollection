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
        const OwnedLocationMode locationMode = OwnedLocationMode.AllLocations;
        var ownedByItemId = repository.Records
            .GroupBy(GetBaseItemId)
            .ToDictionary(group => group.Key, group => (IReadOnlyList<OwnedItemRecord>)group.ToList());

        this.viewModels.Clear();

        if (displayMode == EquipmentDisplayMode.ByAppearanceModel)
        {
            this.viewModels.AddRange(itemDatabase.Equipment
                .GroupBy(item => item.AppearanceKey)
                .Select(group => CreateAppearanceViewModel(group.ToList(), ownedByItemId, locationMode))
                .OrderBy(item => item.Item.Name));
        }
        else
        {
            this.viewModels.AddRange(itemDatabase.Equipment
                .Select(item => CreateItemViewModel(item, ownedByItemId, locationMode)));
        }

        this.Version++;
    }

    private static EquipmentViewModel CreateItemViewModel(
        EquipmentRecord item,
        IReadOnlyDictionary<uint, IReadOnlyList<OwnedItemRecord>> ownedByItemId,
        OwnedLocationMode locationMode)
    {
        ownedByItemId.TryGetValue(item.ItemId, out var locations);
        return CreateViewModel(item, locations ?? [], [item], locationMode);
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
        OwnedLocationMode locationMode)
    {
        var locations = locationMode == OwnedLocationMode.FirstLocationOnly
            ? allLocations.Take(1).ToList()
            : allLocations;

        return new EquipmentViewModel(
            item,
            allLocations.Count > 0,
            locations,
            appearanceItems,
            allLocations.Any(location => !location.IsHq),
            allLocations.Any(location => location.IsHq));
    }

    private static uint NormalizeBaseItemId(uint itemId)
    {
        const uint hqItemIdOffset = 1_000_000;
        return itemId > hqItemIdOffset ? itemId - hqItemIdOffset : itemId;
    }
}

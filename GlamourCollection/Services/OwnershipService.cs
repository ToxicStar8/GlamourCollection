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

        var locationMode = (OwnedLocationMode)configuration.OwnedLocationMode;
        var ownedByItemId = repository.Records
            .GroupBy(GetMatchItemId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<OwnedItemRecord>)(locationMode == OwnedLocationMode.FirstLocationOnly
                    ? group.Take(1).ToList()
                    : group.ToList()));

        this.viewModels.Clear();
        this.viewModels.AddRange(itemDatabase.Equipment.Select(item =>
        {
            ownedByItemId.TryGetValue(item.ItemId, out var locations);
            return new EquipmentViewModel(item, locations is { Count: > 0 }, locations ?? []);
        }));
        this.Version++;
    }

    private uint GetMatchItemId(OwnedItemRecord item)
    {
        var matchMode = (OwnershipMatchMode)configuration.OwnershipMatchMode;
        return matchMode switch
        {
            OwnershipMatchMode.RawItemId => item.RawItemId != 0 ? item.RawItemId : item.ItemId,
            _ => GetBaseItemId(item),
        };
    }

    private static uint GetBaseItemId(OwnedItemRecord item)
    {
        if (item.BaseItemId != 0)
            return item.BaseItemId;

        if (item.ItemId != 0)
            return NormalizeBaseItemId(item.ItemId);

        return NormalizeBaseItemId(item.RawItemId);
    }

    private static uint NormalizeBaseItemId(uint itemId)
    {
        const uint hqItemIdOffset = 1_000_000;
        return itemId > hqItemIdOffset ? itemId - hqItemIdOffset : itemId;
    }
}

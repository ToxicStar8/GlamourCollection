using Main.Models;
using System.Collections.Generic;
using System.Linq;

namespace Main.Services;

public sealed class OwnershipService(ItemDatabaseService itemDatabase, OwnedItemRepository repository)
{
    private readonly List<EquipmentViewModel> viewModels = [];

    public IReadOnlyList<EquipmentViewModel> ViewModels => this.viewModels;

    public int OwnedItemCount => this.viewModels.Count(item => item.IsOwned);

    public void Refresh()
    {
        itemDatabase.Load();

        var ownedByItemId = repository.Records
            .GroupBy(item => item.ItemId)
            .ToDictionary(group => group.Key, group => (IReadOnlyList<OwnedItemRecord>)group.ToList());

        this.viewModels.Clear();
        this.viewModels.AddRange(itemDatabase.Equipment.Select(item =>
        {
            ownedByItemId.TryGetValue(item.ItemId, out var locations);
            return new EquipmentViewModel(item, locations is { Count: > 0 }, locations ?? []);
        }));
    }
}

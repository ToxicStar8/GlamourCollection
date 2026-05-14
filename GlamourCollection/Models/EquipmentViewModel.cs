using System.Collections.Generic;

namespace Main.Models;

public sealed record EquipmentViewModel(
    EquipmentRecord Item,
    bool IsOwned,
    IReadOnlyList<OwnedItemRecord> OwnedLocations,
    IReadOnlyList<EquipmentRecord> AppearanceItems,
    bool IsAppearanceGroup,
    bool HasNormalQuality,
    bool HasHighQuality)
{
    public int AppearanceItemCount => this.AppearanceItems.Count;

    public IReadOnlyList<EquipmentRecord> FilterItems => this.IsAppearanceGroup ? this.AppearanceItems : [this.Item];
}

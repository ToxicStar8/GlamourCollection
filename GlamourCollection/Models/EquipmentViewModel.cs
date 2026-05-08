using System.Collections.Generic;

namespace Main.Models;

public sealed record EquipmentViewModel(
    EquipmentRecord Item,
    bool IsOwned,
    IReadOnlyList<OwnedItemRecord> OwnedLocations);

using System.Collections.Generic;

namespace Main.Models;

public sealed record EquipmentSourceInfo(
    string Text,
    IReadOnlyList<SourceCategory> Categories,
    ExpansionCategory Expansion,
    string ExpansionText,
    bool IsExpansionEstimated,
    bool HasDetailedData);

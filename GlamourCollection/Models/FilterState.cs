using System;
using System.Collections.Generic;

namespace Main.Models;

[Serializable]
public sealed class FilterState
{
    public int Version { get; set; }

    public int OwnershipFilter { get; set; } = (int)EquipmentOwnershipFilter.All;

    public int QualityFilter { get; set; } = (int)EquipmentQualityFilter.All;

    public int SameModelFilter { get; set; } = (int)EquipmentSameModelFilter.All;

    public int DyeFilter { get; set; } = (int)EquipmentDyeFilter.All;

    public List<int> SelectedJobs { get; set; } = [];

    public List<int> SelectedSlots { get; set; } = [];

    public List<int> SelectedExpansions { get; set; } = [];

    public List<int> SelectedSourceCategories { get; set; } = [];

    public int EquipLevelMin { get; set; }

    public int EquipLevelMax { get; set; }

    public int ItemLevelMin { get; set; }

    public int ItemLevelMax { get; set; }

    public int SortMode { get; set; } = (int)EquipmentSortMode.Name;

    public bool SortDescending { get; set; }

    public bool IsFilterPanelOpen { get; set; } = true;

    public bool IsAdvancedFilterOpen { get; set; }

    public void EnsureLists()
    {
        this.SelectedJobs ??= [];
        this.SelectedSlots ??= [];
        this.SelectedExpansions ??= [];
        this.SelectedSourceCategories ??= [];
    }

    public void ClearFilters()
    {
        this.OwnershipFilter = (int)EquipmentOwnershipFilter.All;
        this.QualityFilter = (int)EquipmentQualityFilter.All;
        this.SameModelFilter = (int)EquipmentSameModelFilter.All;
        this.DyeFilter = (int)EquipmentDyeFilter.All;
        this.SelectedJobs.Clear();
        this.SelectedSlots.Clear();
        this.SelectedExpansions.Clear();
        this.SelectedSourceCategories.Clear();
        this.EquipLevelMin = 0;
        this.EquipLevelMax = 0;
        this.ItemLevelMin = 0;
        this.ItemLevelMax = 0;
    }

    public void ResetView()
    {
        this.ClearFilters();
        this.SortMode = (int)EquipmentSortMode.Name;
        this.SortDescending = false;
        this.IsFilterPanelOpen = true;
        this.IsAdvancedFilterOpen = false;
    }
}

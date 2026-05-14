using Dalamud.Configuration;
using Dalamud.Plugin;
using ECommons.DalamudServices;
using Main.Models;
using System;
using System.Collections.Generic;

namespace Main
{
    [Serializable]
    public class Configuration : IPluginConfiguration
    {
        public int Version { get; set; } = 0;

        //是否登录就显示窗口
        public bool IsLoginedOpenWindow { get; set; } = false;
        //是否使用Esc可以关闭窗口
        public bool IsEscCloseWindow { get; set; } = true;
        //语言类型 zh=0 todo:待实现
        public int LangIndex { get; set; } = 0;
        public int EquipmentDisplayMode { get; set; } = (int)Main.Models.EquipmentDisplayMode.ByItem;
        public int EquipmentAppearanceMatchMode { get; set; } = (int)Main.Models.EquipmentAppearanceMatchMode.Strict;
        public int OwnedLocationMode { get; set; } = (int)Main.Models.OwnedLocationMode.AllLocations;
        public int EquipmentOwnershipFilter { get; set; } = (int)Main.Models.EquipmentOwnershipFilter.All;
        public int EquipmentQualityFilter { get; set; } = (int)Main.Models.EquipmentQualityFilter.All;
        public int EquipmentSameModelFilter { get; set; } = (int)Main.Models.EquipmentSameModelFilter.All;
        public int EquipmentDyeFilter { get; set; } = (int)Main.Models.EquipmentDyeFilter.All;
        public int EquipmentSortMode { get; set; } = (int)Main.Models.EquipmentSortMode.Name;
        public bool EquipmentSortDescending { get; set; } = false;
        public bool ShowHoveredItemOwnershipOverlay { get; set; } = true;
        public bool HoveredItemOwnershipUseSameModel { get; set; } = false;
        public FilterState Filters { get; set; } = new();

        public void Init()
        {
            this.Filters ??= new FilterState();
            this.Filters.EnsureLists();

            if (this.Filters.Version != 0)
                return;

            this.Filters.OwnershipFilter = this.EquipmentOwnershipFilter;
            this.Filters.QualityFilter = this.EquipmentQualityFilter;
            this.Filters.SameModelFilter = this.EquipmentSameModelFilter;
            this.Filters.DyeFilter = this.EquipmentDyeFilter;
            this.Filters.SortMode = this.EquipmentSortMode;
            this.Filters.SortDescending = this.EquipmentSortDescending;
            this.Filters.Version = 1;
        }

        public void Save()
        {
            Plugin.Instance.PluginInterface!.SavePluginConfig(this);
        }
    }
}

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
        public int OwnedLocationMode { get; set; } = (int)Main.Models.OwnedLocationMode.AllLocations;
        public int EquipmentOwnershipFilter { get; set; } = (int)Main.Models.EquipmentOwnershipFilter.All;
        public int EquipmentQualityFilter { get; set; } = (int)Main.Models.EquipmentQualityFilter.All;
        public int EquipmentSameModelFilter { get; set; } = (int)Main.Models.EquipmentSameModelFilter.All;
        public int EquipmentDyeFilter { get; set; } = (int)Main.Models.EquipmentDyeFilter.All;
        public int EquipmentSortMode { get; set; } = (int)Main.Models.EquipmentSortMode.Name;
        public bool EquipmentSortDescending { get; set; } = false;

        public void Init()
        {

        }

        public void Save()
        {
            Plugin.Instance.PluginInterface!.SavePluginConfig(this);
        }
    }
}

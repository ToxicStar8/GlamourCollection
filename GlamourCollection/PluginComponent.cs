using Dalamud.Game.Gui.Dtr;
using Dalamud.Hooking;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Utility;
using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Client.UI.Info;
using Lumina.Excel.Sheets;
using Main.Services;
using System;
using System.Collections.Generic;
using World = Lumina.Excel.Sheets.World;

namespace Main
{
    public unsafe partial class Plugin
    {
        [PluginService] public IDalamudPluginInterface PluginInterface { get; private set; } = null!;
        //基础
        public string Name => "GlamourCollection";
        //指令名
        private const string _commonName = "/dgc";
        //窗口事件
        private WindowSystem _windowSystem = new("GlamourCollection");
        //主UI
        private MainWindow _mainWindow { get; init; }
        //配置
        public Configuration Configuration { get; init; }
        public GarlandSourceCacheService GarlandSources { get; private set; } = null!;
        public SourceInfoService SourceInfo { get; private set; } = null!;
        public ItemDatabaseService ItemDatabase { get; private set; } = null!;
        public OwnedItemRepository OwnedItems { get; private set; } = null!;
        public InventoryScanner InventoryScanner { get; private set; } = null!;
        public InventoryWatcher InventoryWatcher { get; private set; } = null!;
        public RetainerInventoryWatcher RetainerInventoryWatcher { get; private set; } = null!;
        public SaddlebagInventoryWatcher SaddlebagInventoryWatcher { get; private set; } = null!;
        public GlamourDresserInventoryWatcher GlamourDresserInventoryWatcher { get; private set; } = null!;
        public ArmoireInventoryWatcher ArmoireInventoryWatcher { get; private set; } = null!;
        public OwnershipService Ownership { get; private set; } = null!;
        public ITryOnService TryOn { get; private set; } = null!;
        public HoveredItemOwnershipOverlay HoveredItemOwnershipOverlay { get; private set; } = null!;
        public DateTimeOffset? LastInventoryScanAt { get; private set; }
        public string LastInventoryScanStatus { get; private set; } = "等待角色登录。";
        //自己
        public static Plugin Instance = null!;

        private bool scanWhenCharacterReady;
        private ulong loadedCharacterId;
    }
}

using Dalamud.Game.Gui.Dtr;
using Dalamud.Hooking;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Utility;
using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Client.UI.Info;
using Lumina.Excel.Sheets;
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
        private const string _commonName = "/glamour";
        //窗口事件
        private WindowSystem _windowSystem = new("GlamourCollection");
        //主UI
        private MainWindow _mainWindow { get; init; }
        //配置
        public Configuration Configuration { get; init; }
        //自己
        public static Plugin Instance;
    }
}

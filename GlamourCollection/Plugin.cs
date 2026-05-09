using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.Command;
using Dalamud.Game.Gui.ContextMenu;
using Dalamud.Game.Gui.Dtr;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using ECommons;
using ECommons.Automation;
using ECommons.DalamudServices;
using ECommons.DalamudServices.Legacy;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.FFXIV.Client.Graphics.Scene;
using FFXIVClientStructs.FFXIV.Client.UI.Info;
using Lumina.Excel.Sheets;
using Main.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;

namespace Main
{
    /// <summary>
    /// 插件入口
    /// </summary>
    public unsafe partial class Plugin : IDalamudPlugin
    {
        //构造函数
        public Plugin(IDalamudPluginInterface pluginInterface)
        {
            PluginInterface = pluginInterface;
            //初始化
            Instance = this;
            ECommonsMain.Init(PluginInterface, this);

            //new配置出来
            Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
            Configuration.Init();
            SourceInfo = new SourceInfoService();
            ItemDatabase = new ItemDatabaseService(SourceInfo);
            OwnedItems = new OwnedItemRepository();
            InventoryScanner = new InventoryScanner(ItemDatabase);
            InventoryWatcher = new InventoryWatcher();
            RetainerInventoryWatcher = new RetainerInventoryWatcher();
            Ownership = new OwnershipService(ItemDatabase, OwnedItems, Configuration);
            TryOn = new TryOnService();
            Ownership.Refresh();
            //窗口类
            _mainWindow = new MainWindow();
            _windowSystem.AddWindow(_mainWindow);

            //绑定指令监听
            Svc.Commands.AddHandler(_commonName, new CommandInfo(OpenMainUI)
            {
                HelpMessage = Lang.OpenSetting
            });

            //绘制UI
            Svc.PluginInterface.UiBuilder.Draw += DrawUI;
            Svc.PluginInterface.UiBuilder.OpenMainUi += ToggleMainUI;
            Svc.ClientState.Login += OnLogin;
            Svc.ClientState.Logout += OnLogout;
            Svc.Framework.Update += OnFrameworkUpdate;

            _mainWindow.RespectCloseHotkey = Configuration.IsEscCloseWindow;

            //启动时根据情况选择是否开启，方便测试
            _mainWindow.IsOpen = Configuration.IsLoginedOpenWindow;
            scanWhenCharacterReady = true;
        }

        #region Common
        //绘制UI方法
        private void DrawUI()
        {
            _windowSystem.Draw();
        }

        /// <summary>
        /// 打开主UI
        /// </summary>
        private void OpenMainUI(string command, string args)
        {
            _mainWindow.Toggle();
        }

        private void ToggleMainUI()
        {
            _mainWindow.Toggle();
        }

        private void OnLogin()
        {
            scanWhenCharacterReady = true;
        }

        private void OnLogout(int type, int code)
        {
            scanWhenCharacterReady = false;
            loadedCharacterId = 0;
            InventoryWatcher.ClearPending();
            RetainerInventoryWatcher.ClearPending();
            OwnedItems.Clear();
            Ownership.Refresh();
            LastInventoryScanStatus = "等待角色登录。";
            LastInventoryScanAt = null;
        }

        private void OnFrameworkUpdate(IFramework framework)
        {
            if (!Svc.ClientState.IsLoggedIn)
                return;

            var characterId = Svc.ClientState.LocalContentId;
            if (characterId == 0)
                return;

            if (loadedCharacterId != characterId)
            {
                loadedCharacterId = characterId;
                OwnedItems.Load(characterId);
                Ownership.Refresh();
                scanWhenCharacterReady = true;
            }

            if (scanWhenCharacterReady)
            {
                RescanOwnedItems("登录扫描。");
                scanWhenCharacterReady = false;
                InventoryWatcher.ClearPending();
                return;
            }

            if (InventoryWatcher.ConsumeRescanRequest())
                RescanOwnedItems(InventoryWatcher.LastChangeReason);

            if (RetainerInventoryWatcher.ConsumeScanRequest())
                ScanCurrentRetainerInventory(RetainerInventoryWatcher.LastScanReason);
        }

        public void RescanOwnedItems(string reason = "手动扫描。")
        {
            if (!Svc.ClientState.IsLoggedIn || Svc.ClientState.LocalContentId == 0)
            {
                LastInventoryScanStatus = "请先登录角色再扫描库存。";
                LastInventoryScanAt = null;
                return;
            }

            var characterId = Svc.ClientState.LocalContentId;
            if (loadedCharacterId != characterId)
            {
                loadedCharacterId = characterId;
                OwnedItems.Load(characterId);
            }

            var worldId = Svc.ClientState.LocalPlayer?.HomeWorld.RowId ?? 0;
            var snapshot = InventoryScanner.ScanPhaseOne(characterId, worldId);

            OwnedItems.ReplacePhaseOneSnapshot(characterId, snapshot);
            Ownership.Refresh();

            LastInventoryScanAt = DateTimeOffset.Now;
            LastInventoryScanStatus = $"{reason} 已扫描 {snapshot.Count} 个装备位置。";
        }

        private void ScanCurrentRetainerInventory(string reason = "Retainer inventory scan.")
        {
            if (!Svc.ClientState.IsLoggedIn || Svc.ClientState.LocalContentId == 0)
            {
                LastInventoryScanStatus = "Please log in before scanning retainer inventory.";
                LastInventoryScanAt = null;
                return;
            }

            var characterId = Svc.ClientState.LocalContentId;
            if (loadedCharacterId != characterId)
            {
                loadedCharacterId = characterId;
                OwnedItems.Load(characterId);
            }

            var worldId = Svc.ClientState.LocalPlayer?.HomeWorld.RowId ?? 0;
            var snapshot = InventoryScanner.ScanCurrentRetainer(characterId, worldId);
            if (!snapshot.IsReadable)
            {
                LastInventoryScanStatus = "Retainer inventory is not readable yet.";
                return;
            }

            OwnedItems.ReplaceRetainerSnapshot(characterId, snapshot.RetainerId, snapshot.RetainerName, snapshot.Records);
            Ownership.Refresh();

            LastInventoryScanAt = DateTimeOffset.Now;
            LastInventoryScanStatus = $"{reason} {snapshot.RetainerName}: {snapshot.Records.Count} equipment locations.";
        }

        #endregion

        //退出方法
        public void Dispose()
        {
            //保存配置
            Configuration.Save();
            //移除窗口监听
            _windowSystem.RemoveAllWindows();
            //关闭窗口
            _mainWindow.Dispose();
            //移除指令监听
            Svc.Commands.RemoveHandler(_commonName);
            //移除绘制监听
            Svc.PluginInterface.UiBuilder.Draw -= DrawUI;
            Svc.PluginInterface.UiBuilder.OpenMainUi -= ToggleMainUI;
            Svc.ClientState.Login -= OnLogin;
            Svc.ClientState.Logout -= OnLogout;
            Svc.Framework.Update -= OnFrameworkUpdate;
            InventoryWatcher.Dispose();
            RetainerInventoryWatcher.Dispose();
            //
            ECommonsMain.Dispose();
        }
    }
}

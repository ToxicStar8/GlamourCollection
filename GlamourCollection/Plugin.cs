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
            GarlandSources = new GarlandSourceCacheService(PluginInterface.ConfigDirectory.FullName);
            SourceInfo = new SourceInfoService(GarlandSources);
            ItemDatabase = new ItemDatabaseService(SourceInfo);
            OwnedItems = new OwnedItemRepository();
            InventoryScanner = new InventoryScanner(ItemDatabase);
            InventoryWatcher = new InventoryWatcher();
            RetainerInventoryWatcher = new RetainerInventoryWatcher();
            SaddlebagInventoryWatcher = new SaddlebagInventoryWatcher();
            GlamourDresserInventoryWatcher = new GlamourDresserInventoryWatcher();
            ArmoireInventoryWatcher = new ArmoireInventoryWatcher();
            Ownership = new OwnershipService(ItemDatabase, OwnedItems, Configuration);
            TryOn = new TryOnService();
            HoveredItemOwnershipOverlay = new HoveredItemOwnershipOverlay(Configuration, ItemDatabase, OwnedItems);
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
            HoveredItemOwnershipOverlay.Draw();
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
            FlushRetainerSnapshotQueue();
            scanWhenCharacterReady = false;
            loadedCharacterId = 0;
            InventoryWatcher.ClearPending();
            RetainerInventoryWatcher.ClearPending();
            SaddlebagInventoryWatcher.ClearPending();
            GlamourDresserInventoryWatcher.ClearPending();
            ArmoireInventoryWatcher.ClearPending();
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
                FlushRetainerSnapshotQueue();
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
                RescanOwnedItems(InventoryWatcher.LastChangeReason, refreshWhenLocationsOnly: false);

            ProcessRetainerSnapshotQueue();
            RetainerInventoryWatcher.UpdateActiveRetainer();
            if (RetainerInventoryWatcher.ConsumeScanRequest())
                ScanCurrentRetainerInventory(
                    RetainerInventoryWatcher.LastScanReason,
                    RetainerInventoryWatcher.LastScanWasSpeculative,
                    RetainerInventoryWatcher.LastScanRetainerId);

            if (SaddlebagInventoryWatcher.ConsumeScanRequest())
                ScanSaddlebagInventory(SaddlebagInventoryWatcher.LastScanReason);

            if (GlamourDresserInventoryWatcher.ConsumeScanRequest())
                ScanGlamourDresserInventory(GlamourDresserInventoryWatcher.LastScanReason);

            if (ArmoireInventoryWatcher.ConsumeScanRequest())
                ScanArmoireInventory(ArmoireInventoryWatcher.LastScanReason);
        }

        public void RescanOwnedItems(string reason = "手动扫描。", bool refreshWhenLocationsOnly = true)
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

            var changeKind = OwnedItems.ReplacePhaseOneSnapshot(characterId, snapshot);
            if (refreshWhenLocationsOnly || changeKind == OwnedItemSnapshotChangeKind.OwnershipChanged)
                Ownership.Refresh();

            LastInventoryScanAt = DateTimeOffset.Now;
            LastInventoryScanStatus = changeKind == OwnedItemSnapshotChangeKind.LocationsOnly && !refreshWhenLocationsOnly
                ? $"{reason} 已更新装备位置，拥有列表未重建。"
                : $"{reason} 已扫描 {snapshot.Count} 个装备位置。";
        }

        private void ScanCurrentRetainerInventory(
            string reason = "雇员库存扫描。",
            bool silentIfUnreadable = false,
            ulong expectedRetainerId = 0)
        {
            if (!Svc.ClientState.IsLoggedIn || Svc.ClientState.LocalContentId == 0)
            {
                LastInventoryScanStatus = "请先登录角色再扫描雇员库存。";
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
            if (expectedRetainerId != 0 && snapshot.RetainerId != expectedRetainerId)
            {
                if (RetainerInventoryWatcher.ScheduleRetry(reason, silentIfUnreadable))
                    return;

                if (!silentIfUnreadable)
                    LastInventoryScanStatus = "雇员切换过快，本次扫描已丢弃。";
                return;
            }

            if (!snapshot.IsReadable)
            {
                if (RetainerInventoryWatcher.ScheduleRetry(reason, silentIfUnreadable))
                {
                    if (!silentIfUnreadable)
                        LastInventoryScanStatus = "雇员库存仍在加载，正在自动重试扫描。";
                    return;
                }

                if (!silentIfUnreadable)
                    LastInventoryScanStatus = "雇员库存暂时不可读取，请打开雇员道具管理后稍等。";
                return;
            }

            RetainerInventoryWatcher.MarkScanCompleted();
            LastInventoryScanAt = DateTimeOffset.Now;
            this.queuedRetainerSnapshots.Enqueue(new QueuedRetainerSnapshot(characterId, reason, snapshot));
            this.framesUntilRetainerSnapshotFlush = RetainerSnapshotFlushDelayFrames;
            LastInventoryScanStatus = $"{reason} {snapshot.RetainerName}: 已读取 {snapshot.Records.Count} 个装备位置，等待队列写入。";
        }

        private void ProcessRetainerSnapshotQueue()
        {
            if (this.queuedRetainerSnapshots.Count > 0)
            {
                var queued = this.queuedRetainerSnapshots.Dequeue();
                if (queued.CharacterId == loadedCharacterId)
                {
                    var snapshot = queued.Snapshot;
                    var changeKind = OwnedItems.ReplaceRetainerSnapshot(
                        queued.CharacterId,
                        snapshot.RetainerId,
                        snapshot.RetainerName,
                        snapshot.Records,
                        save: false);

                    this.queuedRetainerOwnershipChanged |= changeKind == OwnedItemSnapshotChangeKind.OwnershipChanged;
                    this.queuedRetainerLocationsChanged |= changeKind == OwnedItemSnapshotChangeKind.LocationsOnly;
                    this.queuedRetainerStatusText = changeKind switch
                    {
                        OwnedItemSnapshotChangeKind.None => $"{queued.Reason} {snapshot.RetainerName}: 缓存无变化。",
                        OwnedItemSnapshotChangeKind.LocationsOnly => $"{queued.Reason} {snapshot.RetainerName}: 已更新装备位置。",
                        _ => $"{queued.Reason} {snapshot.RetainerName}: 已扫描 {snapshot.Records.Count} 个装备位置。",
                    };
                }

                if (this.queuedRetainerSnapshots.Count == 0
                    && !this.queuedRetainerOwnershipChanged
                    && !this.queuedRetainerLocationsChanged
                    && !string.IsNullOrWhiteSpace(this.queuedRetainerStatusText))
                {
                    LastInventoryScanStatus = this.queuedRetainerStatusText;
                    this.queuedRetainerStatusText = string.Empty;
                    this.framesUntilRetainerSnapshotFlush = 0;
                }

                return;
            }

            if (!this.queuedRetainerOwnershipChanged && !this.queuedRetainerLocationsChanged)
                return;

            if (this.framesUntilRetainerSnapshotFlush > 0)
            {
                this.framesUntilRetainerSnapshotFlush--;
                return;
            }

            CommitRetainerSnapshotChanges();
        }

        private void FlushRetainerSnapshotQueue()
        {
            while (this.queuedRetainerSnapshots.Count > 0)
            {
                var queued = this.queuedRetainerSnapshots.Dequeue();
                if (queued.CharacterId != loadedCharacterId)
                    continue;

                var snapshot = queued.Snapshot;
                var changeKind = OwnedItems.ReplaceRetainerSnapshot(
                    queued.CharacterId,
                    snapshot.RetainerId,
                    snapshot.RetainerName,
                    snapshot.Records,
                    save: false);
                this.queuedRetainerOwnershipChanged |= changeKind == OwnedItemSnapshotChangeKind.OwnershipChanged;
                this.queuedRetainerLocationsChanged |= changeKind == OwnedItemSnapshotChangeKind.LocationsOnly;
            }

            CommitRetainerSnapshotChanges();
        }

        private void CommitRetainerSnapshotChanges()
        {
            if (!this.queuedRetainerOwnershipChanged && !this.queuedRetainerLocationsChanged)
                return;

            OwnedItems.Save();
            if (this.queuedRetainerOwnershipChanged)
                Ownership.Refresh();
            else
                Ownership.RefreshOwnedLocations();

            this.queuedRetainerOwnershipChanged = false;
            this.queuedRetainerLocationsChanged = false;
            this.framesUntilRetainerSnapshotFlush = 0;
            if (!string.IsNullOrWhiteSpace(this.queuedRetainerStatusText))
                LastInventoryScanStatus = this.queuedRetainerStatusText;
            this.queuedRetainerStatusText = string.Empty;
        }

        private void ScanSaddlebagInventory(string reason = "陆行鸟背包扫描。")
        {
            if (!Svc.ClientState.IsLoggedIn || Svc.ClientState.LocalContentId == 0)
            {
                LastInventoryScanStatus = "请先登录角色再扫描陆行鸟背包。";
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
            var snapshot = InventoryScanner.ScanSaddlebag(characterId, worldId);
            if (!snapshot.IsReadable)
            {
                LastInventoryScanStatus = "陆行鸟背包暂时不可读取，请打开陆行鸟背包后稍等。";
                return;
            }

            OwnedItems.ReplaceSaddlebagSnapshot(
                characterId,
                snapshot.IsSaddlebagReadable,
                snapshot.IsPremiumSaddlebagReadable,
                snapshot.Records);
            Ownership.Refresh();

            var saddlebagText = snapshot.IsSaddlebagReadable
                ? $"陆行鸟背包: {snapshot.SaddlebagRecordCount} 个装备位置"
                : "陆行鸟背包: 不可读取";
            var premiumText = snapshot.IsPremiumSaddlebagReadable
                ? $"高级陆行鸟背包: {snapshot.PremiumSaddlebagRecordCount} 个装备位置"
                : "高级陆行鸟背包: 不可读取";

            LastInventoryScanAt = DateTimeOffset.Now;
            LastInventoryScanStatus = $"{reason} {saddlebagText}; {premiumText}.";
        }

        private void ScanGlamourDresserInventory(string reason = "幻化柜扫描。")
        {
            if (!Svc.ClientState.IsLoggedIn || Svc.ClientState.LocalContentId == 0)
            {
                LastInventoryScanStatus = "请先登录角色再扫描幻化柜。";
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
            var snapshot = InventoryScanner.ScanGlamourDresser(characterId, worldId);
            if (!snapshot.IsReadable)
            {
                LastInventoryScanStatus = "幻化柜暂时不可读取，请打开幻化柜后稍等。";
                return;
            }

            OwnedItems.ReplaceGlamourDresserSnapshot(characterId, snapshot.Records);
            Ownership.Refresh();

            LastInventoryScanAt = DateTimeOffset.Now;
            LastInventoryScanStatus = $"{reason} 幻化柜: 已扫描 {snapshot.Records.Count} 个装备位置。";
        }

        private void ScanArmoireInventory(string reason = "收藏柜扫描。")
        {
            if (!Svc.ClientState.IsLoggedIn || Svc.ClientState.LocalContentId == 0)
            {
                LastInventoryScanStatus = "请先登录角色再扫描收藏柜。";
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
            var snapshot = InventoryScanner.ScanArmoire(characterId, worldId);
            if (!snapshot.IsReadable)
            {
                if (ArmoireInventoryWatcher.ScheduleRetry(reason))
                {
                    LastInventoryScanStatus = "收藏柜数据仍在加载，正在自动重试扫描。";
                    return;
                }

                LastInventoryScanStatus = "收藏柜暂时不可读取，请打开收藏柜的存入/取出界面并稍等。";
                return;
            }

            OwnedItems.ReplaceArmoireSnapshot(characterId, snapshot.Records);
            Ownership.Refresh();

            LastInventoryScanAt = DateTimeOffset.Now;
            LastInventoryScanStatus = $"{reason} 收藏柜: 已扫描 {snapshot.Records.Count} 个装备位置。";
        }

        public void ClearRetainerInventoryCache()
        {
            if (!Svc.ClientState.IsLoggedIn || Svc.ClientState.LocalContentId == 0)
            {
                LastInventoryScanStatus = "请先登录角色再清除雇员缓存。";
                LastInventoryScanAt = null;
                return;
            }

            var characterId = Svc.ClientState.LocalContentId;
            if (loadedCharacterId != characterId)
            {
                loadedCharacterId = characterId;
                OwnedItems.Load(characterId);
            }

            var removed = OwnedItems.ClearRetainerSnapshots(characterId);
            Ownership.Refresh();

            LastInventoryScanAt = DateTimeOffset.Now;
            LastInventoryScanStatus = $"已清除 {removed} 条雇员装备缓存。";
        }

        public void ClearSaddlebagInventoryCache()
        {
            if (!Svc.ClientState.IsLoggedIn || Svc.ClientState.LocalContentId == 0)
            {
                LastInventoryScanStatus = "请先登录角色再清除陆行鸟背包缓存。";
                LastInventoryScanAt = null;
                return;
            }

            var characterId = Svc.ClientState.LocalContentId;
            if (loadedCharacterId != characterId)
            {
                loadedCharacterId = characterId;
                OwnedItems.Load(characterId);
            }

            var removed = OwnedItems.ClearSaddlebagSnapshots(characterId);
            Ownership.Refresh();

            LastInventoryScanAt = DateTimeOffset.Now;
            LastInventoryScanStatus = $"已清除 {removed} 条陆行鸟背包装备缓存。";
        }

        public void ClearGlamourDresserInventoryCache()
        {
            if (!Svc.ClientState.IsLoggedIn || Svc.ClientState.LocalContentId == 0)
            {
                LastInventoryScanStatus = "请先登录角色再清除幻化柜缓存。";
                LastInventoryScanAt = null;
                return;
            }

            var characterId = Svc.ClientState.LocalContentId;
            if (loadedCharacterId != characterId)
            {
                loadedCharacterId = characterId;
                OwnedItems.Load(characterId);
            }

            var removed = OwnedItems.ClearGlamourDresserSnapshots(characterId);
            Ownership.Refresh();

            LastInventoryScanAt = DateTimeOffset.Now;
            LastInventoryScanStatus = $"已清除 {removed} 条幻化柜装备缓存。";
        }

        public void ClearArmoireInventoryCache()
        {
            if (!Svc.ClientState.IsLoggedIn || Svc.ClientState.LocalContentId == 0)
            {
                LastInventoryScanStatus = "请先登录角色再清除收藏柜缓存。";
                LastInventoryScanAt = null;
                return;
            }

            var characterId = Svc.ClientState.LocalContentId;
            if (loadedCharacterId != characterId)
            {
                loadedCharacterId = characterId;
                OwnedItems.Load(characterId);
            }

            var removed = OwnedItems.ClearArmoireSnapshots(characterId);
            Ownership.Refresh();

            LastInventoryScanAt = DateTimeOffset.Now;
            LastInventoryScanStatus = $"已清除 {removed} 条收藏柜装备缓存。";
        }

        #endregion

        //退出方法
        public void Dispose()
        {
            FlushRetainerSnapshotQueue();
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
            SaddlebagInventoryWatcher.Dispose();
            GlamourDresserInventoryWatcher.Dispose();
            ArmoireInventoryWatcher.Dispose();
            //
            ECommonsMain.Dispose();
        }
    }
}

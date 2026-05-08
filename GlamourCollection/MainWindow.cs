using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Windowing;
using ECommons.DalamudServices;
using Main.Models;
using Main.Services;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;

namespace Main
{
    public unsafe class MainWindow : Window, IDisposable
    {
        private static readonly string[] OwnershipMatchModeLabels =
        [
            "基础道具ID（HQ/NQ有一个即可）",
            "原始道具ID（严格区分）",
        ];

        private static readonly string[] OwnedLocationModeLabels =
        [
            "只显示一个位置",
            "显示全部位置",
        ];

        private readonly List<EquipmentViewModel> filteredItems = [];
        private string searchText = string.Empty;
        private string cachedSearchText = string.Empty;
        private int cachedOwnershipVersion = -1;

        public MainWindow() : base(Plugin.Instance.Name)
        {
            SizeConstraints = new WindowSizeConstraints
            {
                MinimumSize = new Vector2(520, 320),
                MaximumSize = new Vector2(1000, 900),
            };
        }

        public override void Draw()
        {
            var config = Plugin.Instance.Configuration;

            if (!ImGui.BeginTabBar(Plugin.Instance.Name))
                return;

            if (ImGui.BeginTabItem("收藏"))
            {
                DrawCollection();
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem(Lang.Setting))
            {
                var isLoginedOpenWindow = config.IsLoginedOpenWindow;
                ImGui.Checkbox(Lang.LoginShow, ref isLoginedOpenWindow);
                if (config.IsLoginedOpenWindow != isLoginedOpenWindow)
                {
                    config.IsLoginedOpenWindow = isLoginedOpenWindow;
                    config.Save();
                }

                var isEscCloseWindow = config.IsEscCloseWindow;
                ImGui.Checkbox(Lang.EscClose, ref isEscCloseWindow);
                if (config.IsEscCloseWindow != isEscCloseWindow)
                {
                    config.IsEscCloseWindow = isEscCloseWindow;
                    RespectCloseHotkey = isEscCloseWindow;
                    config.Save();
                }

                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem(Lang.About))
            {
                if (ImGui.Button(Lang.SendIssue))
                {
                    var url = "https://discord.gg/GWMEY9P9BX";
                    Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
                }

                ImGui.EndTabItem();
            }

            ImGui.EndTabBar();
        }

        private void DrawCollection()
        {
            var plugin = Plugin.Instance;

            if (ImGui.Button("重新扫描"))
                plugin.RescanOwnedItems();

            ImGui.SameLine();
            ImGui.TextUnformatted(plugin.LastInventoryScanStatus);

            if (plugin.LastInventoryScanAt is { } lastScan)
                ImGui.TextDisabled(lastScan.ToString("yyyy-MM-dd HH:mm:ss"));

            DrawOwnershipOptions(plugin);

            ImGui.InputText("搜索", ref searchText, 128);

            var filtered = GetFilteredItems(plugin.Ownership);
            ImGui.TextUnformatted($"已拥有 {plugin.Ownership.OwnedItemCount} / {plugin.Ownership.ViewModels.Count} | 当前显示 {filtered.Count}");

            DrawEquipmentTable(filtered);
        }

        private void DrawOwnershipOptions(Plugin plugin)
        {
            var config = plugin.Configuration;
            var matchMode = Math.Clamp(config.OwnershipMatchMode, 0, OwnershipMatchModeLabels.Length - 1);
            var locationMode = Math.Clamp(config.OwnedLocationMode, 0, OwnedLocationModeLabels.Length - 1);

            ImGui.SetNextItemWidth(220f);
            ImGui.SetNextItemWidth(260f);
            if (ImGui.Combo("拥有判定", ref matchMode, OwnershipMatchModeLabels, OwnershipMatchModeLabels.Length))
            {
                config.OwnershipMatchMode = matchMode;
                config.Save();
                plugin.RescanOwnedItems("拥有判定已切换。");
                InvalidateFilterCache();
            }

            ImGui.SameLine();
            ImGui.SetNextItemWidth(180f);
            if (ImGui.Combo("位置显示", ref locationMode, OwnedLocationModeLabels, OwnedLocationModeLabels.Length))
            {
                config.OwnedLocationMode = locationMode;
                config.Save();
                plugin.Ownership.Refresh();
                InvalidateFilterCache();
            }
        }

        private IReadOnlyList<EquipmentViewModel> GetFilteredItems(OwnershipService ownership)
        {
            if (this.cachedOwnershipVersion == ownership.Version
                && string.Equals(this.cachedSearchText, this.searchText, StringComparison.Ordinal))
                return this.filteredItems;

            this.filteredItems.Clear();
            this.cachedSearchText = this.searchText;
            this.cachedOwnershipVersion = ownership.Version;

            if (string.IsNullOrWhiteSpace(this.searchText))
            {
                this.filteredItems.AddRange(ownership.ViewModels);
            }
            else
            {
                this.filteredItems.AddRange(ownership.ViewModels.Where(
                    item => item.Item.Name.Contains(this.searchText, StringComparison.CurrentCultureIgnoreCase)));
            }

            return this.filteredItems;
        }

        private static void DrawEquipmentTable(IReadOnlyList<EquipmentViewModel> items)
        {
            const float rowHeight = 36f;
            var tableFlags = ImGuiTableFlags.Borders
                             | ImGuiTableFlags.RowBg
                             | ImGuiTableFlags.ScrollY
                             | ImGuiTableFlags.SizingStretchProp;

            if (!ImGui.BeginTable("##equipmentList", 3, tableFlags, new Vector2(-1, -1)))
                return;

            var iconSize = GetIconSize(rowHeight);
            ImGui.TableSetupColumn("图标", ImGuiTableColumnFlags.WidthFixed, iconSize + 10f);
            ImGui.TableSetupColumn("名称", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableSetupColumn("拥有状态", ImGuiTableColumnFlags.WidthFixed, 92f);
            ImGui.TableHeadersRow();

            var clipper = ImGui.ImGuiListClipper();
            clipper.Begin(items.Count, rowHeight);
            while (clipper.Step())
            {
                for (var index = clipper.DisplayStart; index < clipper.DisplayEnd; index++)
                    DrawEquipmentRow(items[index], rowHeight, iconSize);
            }

            ImGui.EndTable();
        }

        private static void DrawEquipmentRow(EquipmentViewModel item, float rowHeight, float iconSize)
        {
            ImGui.TableNextRow(rowHeight);

            ImGui.TableSetColumnIndex(0);
            DrawItemIcon(item.Item.IconId, iconSize);

            ImGui.TableSetColumnIndex(1);
            ImGui.AlignTextToFramePadding();
            ImGui.TextUnformatted(item.Item.Name);

            ImGui.TableSetColumnIndex(2);
            ImGui.AlignTextToFramePadding();
            if (item.IsOwned)
                ImGui.TextColored(new Vector4(0.25f, 0.9f, 0.4f, 1f), "[x]");
            else
                ImGui.TextDisabled("-");
        }

        private static float GetIconSize(float rowHeight)
            => Math.Clamp(rowHeight - 4f, 18f, 48f);

        private static void DrawItemIcon(uint iconId, float iconSize)
        {
            var size = new Vector2(iconSize, iconSize);
            if (iconId != 0
                && Svc.Texture.TryGetFromGameIcon(new GameIconLookup(iconId), out var icon)
                && icon.TryGetWrap(out var texture, out _))
            {
                ImGui.Image(texture.Handle, size);
                return;
            }

            ImGui.Dummy(size);
        }

        private void InvalidateFilterCache()
        {
            this.cachedOwnershipVersion = -1;
        }

        public void Dispose()
        {
        }
    }
}

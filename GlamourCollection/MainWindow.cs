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
        private static readonly string[] EquipmentDisplayModeLabels =
        [
            "逐件显示",
            "同模型合并",
        ];

        private static readonly string[] OwnershipFilterLabels =
        [
            "全部",
            "已拥有",
            "未拥有",
        ];

        private static readonly string[] QualityFilterLabels =
        [
            "全部品质",
            "包含 NQ",
            "包含 HQ",
            "同时有 NQ+HQ",
        ];

        private static readonly string[] SameModelFilterLabels =
        [
            "全部模型",
            "仅同模装备",
            "仅单件模型",
        ];

        private static readonly string[] DyeFilterLabels =
        [
            "全部染色",
            "仅可染色",
            "仅不可染色",
        ];

        private static readonly string[] SortModeLabels =
        [
            "名称",
            "拥有状态",
            "同模数",
            "装备等级",
            "物品等级",
            "来源",
        ];

        private readonly List<EquipmentViewModel> filteredItems = [];
        private string searchText = string.Empty;
        private string cachedSearchText = string.Empty;
        private string cachedFilterKey = string.Empty;
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

            ImGui.Separator();
            ImGui.Spacing();
            DrawCollectionControls(plugin);

            var filtered = GetFilteredItems(plugin.Ownership, plugin.Configuration);
            ImGui.TextUnformatted($"已拥有 {plugin.Ownership.OwnedItemCount} / {plugin.Ownership.ViewModels.Count} | 当前显示 {filtered.Count}");

            DrawEquipmentTable(filtered);
        }

        private void DrawCollectionControls(Plugin plugin)
        {
            var config = plugin.Configuration;
            var displayMode = Math.Clamp(config.EquipmentDisplayMode, 0, EquipmentDisplayModeLabels.Length - 1);
            var ownershipFilter = Math.Clamp(config.EquipmentOwnershipFilter, 0, OwnershipFilterLabels.Length - 1);
            var qualityFilter = Math.Clamp(config.EquipmentQualityFilter, 0, QualityFilterLabels.Length - 1);
            var sameModelFilter = Math.Clamp(config.EquipmentSameModelFilter, 0, SameModelFilterLabels.Length - 1);
            var dyeFilter = Math.Clamp(config.EquipmentDyeFilter, 0, DyeFilterLabels.Length - 1);
            var sortMode = Math.Clamp(config.EquipmentSortMode, 0, SortModeLabels.Length - 1);
            var sortDescending = config.EquipmentSortDescending;

            if (ImGui.BeginTable("##collectionOptions", 4, ImGuiTableFlags.SizingStretchProp))
            {
                ImGui.TableSetupColumn("##leftLabel", ImGuiTableColumnFlags.WidthFixed, 72f);
                ImGui.TableSetupColumn("##leftValue", ImGuiTableColumnFlags.WidthFixed, 240f);
                ImGui.TableSetupColumn("##rightLabel", ImGuiTableColumnFlags.WidthFixed, 72f);
                ImGui.TableSetupColumn("##rightValue", ImGuiTableColumnFlags.WidthStretch);

                ImGui.TableNextRow();
                DrawOptionLabel(0, "显示模式");
                ImGui.TableSetColumnIndex(1);
                ImGui.SetNextItemWidth(240f);
                if (ImGui.Combo("##displayMode", ref displayMode, EquipmentDisplayModeLabels, EquipmentDisplayModeLabels.Length))
                {
                    config.EquipmentDisplayMode = displayMode;
                    config.Save();
                    plugin.Ownership.Refresh();
                    InvalidateFilterCache();
                }

                ImGui.TableNextRow();
                DrawOptionLabel(0, "搜索");
                ImGui.TableSetColumnIndex(1);
                ImGui.SetNextItemWidth(240f);
                ImGui.InputText("##search", ref searchText, 128);

                DrawOptionLabel(2, "拥有");
                ImGui.TableSetColumnIndex(3);
                ImGui.SetNextItemWidth(180f);
                if (ImGui.Combo("##ownershipFilter", ref ownershipFilter, OwnershipFilterLabels, OwnershipFilterLabels.Length))
                {
                    config.EquipmentOwnershipFilter = ownershipFilter;
                    config.Save();
                    InvalidateFilterCache();
                }

                ImGui.TableNextRow();
                DrawOptionLabel(0, "品质");
                ImGui.TableSetColumnIndex(1);
                ImGui.SetNextItemWidth(240f);
                if (ImGui.Combo("##qualityFilter", ref qualityFilter, QualityFilterLabels, QualityFilterLabels.Length))
                {
                    config.EquipmentQualityFilter = qualityFilter;
                    config.Save();
                    InvalidateFilterCache();
                }

                DrawOptionLabel(2, "同模");
                ImGui.TableSetColumnIndex(3);
                ImGui.SetNextItemWidth(180f);
                if (ImGui.Combo("##sameModelFilter", ref sameModelFilter, SameModelFilterLabels, SameModelFilterLabels.Length))
                {
                    config.EquipmentSameModelFilter = sameModelFilter;
                    config.Save();
                    InvalidateFilterCache();
                }

                ImGui.TableNextRow();
                DrawOptionLabel(0, "染色");
                ImGui.TableSetColumnIndex(1);
                ImGui.SetNextItemWidth(240f);
                if (ImGui.Combo("##dyeFilter", ref dyeFilter, DyeFilterLabels, DyeFilterLabels.Length))
                {
                    config.EquipmentDyeFilter = dyeFilter;
                    config.Save();
                    InvalidateFilterCache();
                }

                DrawOptionLabel(2, "排序");
                ImGui.TableSetColumnIndex(3);
                ImGui.SetNextItemWidth(180f);
                if (ImGui.Combo("##sortMode", ref sortMode, SortModeLabels, SortModeLabels.Length))
                {
                    config.EquipmentSortMode = sortMode;
                    config.Save();
                    InvalidateFilterCache();
                }

                ImGui.TableNextRow();
                ImGui.TableSetColumnIndex(1);
                if (ImGui.Button("清空筛选"))
                    ResetFilters(config);

                ImGui.TableSetColumnIndex(3);
                if (ImGui.Checkbox("降序", ref sortDescending))
                {
                    config.EquipmentSortDescending = sortDescending;
                    config.Save();
                    InvalidateFilterCache();
                }

                ImGui.EndTable();
            }

            ImGui.Spacing();
        }

        private static void DrawOptionLabel(int column, string label)
        {
            ImGui.TableSetColumnIndex(column);
            ImGui.AlignTextToFramePadding();
            ImGui.TextUnformatted(label);
        }

        private void ResetFilters(Configuration config)
        {
            searchText = string.Empty;
            config.EquipmentOwnershipFilter = (int)EquipmentOwnershipFilter.All;
            config.EquipmentQualityFilter = (int)EquipmentQualityFilter.All;
            config.EquipmentSameModelFilter = (int)EquipmentSameModelFilter.All;
            config.EquipmentDyeFilter = (int)EquipmentDyeFilter.All;
            config.EquipmentSortMode = (int)EquipmentSortMode.Name;
            config.EquipmentSortDescending = false;
            config.Save();
            InvalidateFilterCache();
        }

        private IReadOnlyList<EquipmentViewModel> GetFilteredItems(OwnershipService ownership, Configuration config)
        {
            var filterKey = BuildFilterKey(config);
            if (this.cachedOwnershipVersion == ownership.Version
                && string.Equals(this.cachedSearchText, this.searchText, StringComparison.Ordinal)
                && string.Equals(this.cachedFilterKey, filterKey, StringComparison.Ordinal))
                return this.filteredItems;

            this.filteredItems.Clear();
            this.cachedSearchText = this.searchText;
            this.cachedFilterKey = filterKey;
            this.cachedOwnershipVersion = ownership.Version;

            var query = ownership.ViewModels.AsEnumerable();
            query = ApplySearch(query);
            query = ApplyFilters(query, config);
            query = ApplySort(query, config);

            this.filteredItems.AddRange(query);
            return this.filteredItems;
        }

        private string BuildFilterKey(Configuration config)
            => string.Join(
                '|',
                config.EquipmentOwnershipFilter,
                config.EquipmentQualityFilter,
                config.EquipmentSameModelFilter,
                config.EquipmentDyeFilter,
                config.EquipmentSortMode,
                config.EquipmentSortDescending);

        private IEnumerable<EquipmentViewModel> ApplySearch(IEnumerable<EquipmentViewModel> query)
        {
            if (string.IsNullOrWhiteSpace(this.searchText))
                return query;

            return query.Where(item => item.AppearanceItems.Any(
                appearanceItem => appearanceItem.Name.Contains(this.searchText, StringComparison.CurrentCultureIgnoreCase)));
        }

        private static IEnumerable<EquipmentViewModel> ApplyFilters(IEnumerable<EquipmentViewModel> query, Configuration config)
        {
            query = (EquipmentOwnershipFilter)config.EquipmentOwnershipFilter switch
            {
                EquipmentOwnershipFilter.Owned => query.Where(item => item.IsOwned),
                EquipmentOwnershipFilter.Missing => query.Where(item => !item.IsOwned),
                _ => query,
            };

            query = (EquipmentQualityFilter)config.EquipmentQualityFilter switch
            {
                EquipmentQualityFilter.HasNormalQuality => query.Where(item => item.HasNormalQuality),
                EquipmentQualityFilter.HasHighQuality => query.Where(item => item.HasHighQuality),
                EquipmentQualityFilter.HasBoth => query.Where(item => item.HasNormalQuality && item.HasHighQuality),
                _ => query,
            };

            query = (EquipmentSameModelFilter)config.EquipmentSameModelFilter switch
            {
                EquipmentSameModelFilter.SameModelOnly => query.Where(item => item.AppearanceItemCount > 1),
                EquipmentSameModelFilter.SingleItemOnly => query.Where(item => item.AppearanceItemCount == 1),
                _ => query,
            };

            query = (EquipmentDyeFilter)config.EquipmentDyeFilter switch
            {
                EquipmentDyeFilter.DyeableOnly => query.Where(item => item.AppearanceItems.Any(appearanceItem => appearanceItem.CanBeDyed)),
                EquipmentDyeFilter.NotDyeableOnly => query.Where(item => item.AppearanceItems.All(appearanceItem => !appearanceItem.CanBeDyed)),
                _ => query,
            };

            return query;
        }

        private static IEnumerable<EquipmentViewModel> ApplySort(IEnumerable<EquipmentViewModel> query, Configuration config)
            => ((EquipmentSortMode)config.EquipmentSortMode, config.EquipmentSortDescending) switch
            {
                (EquipmentSortMode.Owned, false) => query.OrderBy(item => item.IsOwned).ThenBy(item => item.Item.Name),
                (EquipmentSortMode.Owned, true) => query.OrderByDescending(item => item.IsOwned).ThenBy(item => item.Item.Name),
                (EquipmentSortMode.SameModelCount, false) => query.OrderBy(item => item.AppearanceItemCount).ThenBy(item => item.Item.Name),
                (EquipmentSortMode.SameModelCount, true) => query.OrderByDescending(item => item.AppearanceItemCount).ThenBy(item => item.Item.Name),
                (EquipmentSortMode.EquipLevel, false) => query.OrderBy(item => item.Item.EquipLevel).ThenBy(item => item.Item.Name),
                (EquipmentSortMode.EquipLevel, true) => query.OrderByDescending(item => item.Item.EquipLevel).ThenBy(item => item.Item.Name),
                (EquipmentSortMode.ItemLevel, false) => query.OrderBy(item => item.Item.ItemLevel).ThenBy(item => item.Item.Name),
                (EquipmentSortMode.ItemLevel, true) => query.OrderByDescending(item => item.Item.ItemLevel).ThenBy(item => item.Item.Name),
                (EquipmentSortMode.Source, false) => query.OrderBy(item => item.Item.SourceInfo).ThenBy(item => item.Item.Name),
                (EquipmentSortMode.Source, true) => query.OrderByDescending(item => item.Item.SourceInfo).ThenBy(item => item.Item.Name),
                (EquipmentSortMode.Name, true) => query.OrderByDescending(item => item.Item.Name),
                _ => query.OrderBy(item => item.Item.Name),
            };

        private static void DrawEquipmentTable(IReadOnlyList<EquipmentViewModel> items)
        {
            const float rowHeight = 36f;
            var tableFlags = ImGuiTableFlags.Borders
                             | ImGuiTableFlags.RowBg
                             | ImGuiTableFlags.ScrollY
                             | ImGuiTableFlags.ScrollX
                             | ImGuiTableFlags.SizingStretchProp;

            var tableHeight = Math.Max(160f, ImGui.GetContentRegionAvail().Y);
            if (!ImGui.BeginTable("##equipmentList", 9, tableFlags, new Vector2(-1, tableHeight)))
                return;

            var iconSize = GetIconSize(rowHeight);
            ImGui.TableSetupColumn("图标", ImGuiTableColumnFlags.WidthFixed, iconSize + 10f);
            ImGui.TableSetupColumn("名称", ImGuiTableColumnFlags.WidthFixed, 280f);
            ImGui.TableSetupColumn("分类", ImGuiTableColumnFlags.WidthFixed, 110f);
            ImGui.TableSetupColumn("装等", ImGuiTableColumnFlags.WidthFixed, 56f);
            ImGui.TableSetupColumn("品级", ImGuiTableColumnFlags.WidthFixed, 56f);
            ImGui.TableSetupColumn("可染", ImGuiTableColumnFlags.WidthFixed, 56f);
            ImGui.TableSetupColumn("来源", ImGuiTableColumnFlags.WidthFixed, 120f);
            ImGui.TableSetupColumn("同模", ImGuiTableColumnFlags.WidthFixed, 64f);
            ImGui.TableSetupColumn("拥有", ImGuiTableColumnFlags.WidthFixed, 124f);
            ImGui.TableHeadersRow();

            var clipper = ImGui.ImGuiListClipper();
            clipper.Begin(items.Count);
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
            var isHovered = false;

            ImGui.TableSetColumnIndex(0);
            DrawItemIcon(item.Item.IconId, iconSize);
            isHovered |= ImGui.IsItemHovered();

            ImGui.TableSetColumnIndex(1);
            ImGui.AlignTextToFramePadding();
            ImGui.TextUnformatted(item.Item.Name);
            isHovered |= ImGui.IsItemHovered();

            ImGui.TableSetColumnIndex(2);
            ImGui.AlignTextToFramePadding();
            ImGui.TextUnformatted(item.Item.CategoryName);
            isHovered |= ImGui.IsItemHovered();

            ImGui.TableSetColumnIndex(3);
            ImGui.AlignTextToFramePadding();
            ImGui.TextUnformatted(item.Item.EquipLevel.ToString());
            isHovered |= ImGui.IsItemHovered();

            ImGui.TableSetColumnIndex(4);
            ImGui.AlignTextToFramePadding();
            ImGui.TextUnformatted(item.Item.ItemLevel.ToString());
            isHovered |= ImGui.IsItemHovered();

            ImGui.TableSetColumnIndex(5);
            ImGui.AlignTextToFramePadding();
            if (item.Item.CanBeDyed)
                ImGui.TextUnformatted("是");
            else
                ImGui.TextDisabled("-");
            isHovered |= ImGui.IsItemHovered();

            ImGui.TableSetColumnIndex(6);
            ImGui.AlignTextToFramePadding();
            ImGui.TextUnformatted(item.Item.SourceInfo);
            isHovered |= ImGui.IsItemHovered();

            ImGui.TableSetColumnIndex(7);
            ImGui.AlignTextToFramePadding();
            if (item.AppearanceItemCount > 1)
                ImGui.TextUnformatted(item.AppearanceItemCount.ToString());
            else
                ImGui.TextDisabled("-");
            isHovered |= ImGui.IsItemHovered();

            ImGui.TableSetColumnIndex(8);
            ImGui.AlignTextToFramePadding();
            if (item.IsOwned)
                ImGui.TextColored(new Vector4(0.25f, 0.9f, 0.4f, 1f), GetOwnedDisplayText(item));
            else
                ImGui.TextDisabled("-");
            isHovered |= ImGui.IsItemHovered();

            if (isHovered)
                DrawEquipmentTooltip(item);
        }

        private static string GetOwnedDisplayText(EquipmentViewModel item)
        {
            if (item.HasNormalQuality && item.HasHighQuality)
                return "[x] NQ+HQ";

            if (item.HasHighQuality)
                return "[x] HQ";

            return "[x] NQ";
        }

        private static void DrawEquipmentTooltip(EquipmentViewModel item)
        {
            ImGui.BeginTooltip();
            ImGui.TextUnformatted(item.Item.Name);
            ImGui.TextDisabled($"ItemId: {item.Item.ItemId}");
            ImGui.Separator();
            ImGui.TextUnformatted($"分类: {item.Item.CategoryName}");
            ImGui.TextUnformatted($"职业: {item.Item.ClassJobCategoryName}");
            ImGui.TextUnformatted($"装备等级: {item.Item.EquipLevel}");
            ImGui.TextUnformatted($"物品等级: {item.Item.ItemLevel}");
            ImGui.TextUnformatted($"可染色: {(item.Item.CanBeDyed ? "是" : "否")}");
            ImGui.TextUnformatted($"来源: {item.Item.SourceInfo}");
            ImGui.TextUnformatted($"拥有: {(item.IsOwned ? GetOwnedDisplayText(item) : "-")}");

            if (item.OwnedLocations.Count > 0)
            {
                ImGui.Separator();
                ImGui.TextUnformatted("拥有位置");
                DrawOwnedLocationList(item.OwnedLocations);
            }

            if (item.AppearanceItemCount > 1)
            {
                ImGui.Separator();
                ImGui.TextUnformatted($"同模装备 ({item.AppearanceItemCount})");
                DrawSameModelList(item);
            }

            ImGui.EndTooltip();
        }

        private static void DrawOwnedLocationList(IReadOnlyList<OwnedItemRecord> locations)
        {
            var listHeight = GetTooltipListHeight(locations.Count, 8);
            if (ImGui.BeginChild("##ownedLocationTooltipList", new Vector2(560f, listHeight), true))
            {
                foreach (var location in locations)
                    ImGui.TextUnformatted($"{GetOwnedQualityText([location])} {location.SourceContainer} / Slot {location.Slot}");
            }

            ImGui.EndChild();
        }

        private static void DrawSameModelList(EquipmentViewModel item)
        {
            var listHeight = GetTooltipListHeight(item.AppearanceItemCount, 14);
            if (ImGui.BeginChild("##sameModelTooltipList", new Vector2(640f, listHeight), true))
            {
                foreach (var appearanceItem in item.AppearanceItems)
                {
                    var locations = item.OwnedLocations
                        .Where(location => GetOwnedBaseItemId(location) == appearanceItem.ItemId)
                        .ToList();
                    var ownedText = locations.Count > 0 ? GetOwnedQualityText(locations) : "-";
                    var line = $"{ownedText} {appearanceItem.Name} ({appearanceItem.ItemId})";

                    if (locations.Count > 0)
                        ImGui.TextColored(new Vector4(0.25f, 0.9f, 0.4f, 1f), line);
                    else
                        ImGui.TextDisabled(line);
                }
            }

            ImGui.EndChild();
        }

        private static float GetTooltipListHeight(int itemCount, int maxVisibleRows)
        {
            var visibleRows = Math.Clamp(itemCount, 1, maxVisibleRows);
            return visibleRows * ImGui.GetTextLineHeightWithSpacing() + ImGui.GetStyle().WindowPadding.Y * 2f;
        }

        private static string GetOwnedQualityText(IReadOnlyList<OwnedItemRecord> locations)
        {
            var hasNormalQuality = locations.Any(location => !location.IsHq);
            var hasHighQuality = locations.Any(location => location.IsHq);

            if (hasNormalQuality && hasHighQuality)
                return "NQ+HQ";

            if (hasHighQuality)
                return "HQ";

            return hasNormalQuality ? "NQ" : "-";
        }

        private static uint GetOwnedBaseItemId(OwnedItemRecord item)
        {
            if (item.BaseItemId != 0)
                return item.BaseItemId;

            if (item.ItemId != 0)
                return NormalizeBaseItemId(item.ItemId);

            return NormalizeBaseItemId(item.RawItemId);
        }

        private static uint NormalizeBaseItemId(uint itemId)
        {
            const uint hqItemIdOffset = 1_000_000;
            return itemId > hqItemIdOffset ? itemId - hqItemIdOffset : itemId;
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

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

        private static readonly (int Value, string Label)[] DisplayModeChips =
        [
            ((int)EquipmentDisplayMode.ByItem, "逐件显示"),
            ((int)EquipmentDisplayMode.ByAppearanceModel, "同模型合并"),
        ];

        private static readonly (int Value, string Label)[] OwnershipChips =
        [
            ((int)EquipmentOwnershipFilter.All, "全部"),
            ((int)EquipmentOwnershipFilter.Owned, "已拥有"),
            ((int)EquipmentOwnershipFilter.Missing, "未拥有"),
        ];

        private static readonly (int Value, string Label)[] QualityChips =
        [
            ((int)EquipmentQualityFilter.All, "全部品质"),
            ((int)EquipmentQualityFilter.HasNormalQuality, "包含 NQ"),
            ((int)EquipmentQualityFilter.HasHighQuality, "包含 HQ"),
            ((int)EquipmentQualityFilter.HasBoth, "NQ+HQ"),
        ];

        private static readonly (int Value, string Label)[] SameModelChips =
        [
            ((int)EquipmentSameModelFilter.All, "全部模型"),
            ((int)EquipmentSameModelFilter.SameModelOnly, "仅同模"),
            ((int)EquipmentSameModelFilter.SingleItemOnly, "仅单件"),
        ];

        private static readonly (int Value, string Label)[] DyeChips =
        [
            ((int)EquipmentDyeFilter.All, "全部染色"),
            ((int)EquipmentDyeFilter.DyeableOnly, "可染色"),
            ((int)EquipmentDyeFilter.NotDyeableOnly, "不可染色"),
        ];

        private static readonly (int Value, string Label)[] SortChips =
        [
            ((int)EquipmentSortMode.Name, "名称"),
            ((int)EquipmentSortMode.Owned, "拥有"),
            ((int)EquipmentSortMode.SameModelCount, "同模数"),
            ((int)EquipmentSortMode.EquipLevel, "装备等级"),
            ((int)EquipmentSortMode.ItemLevel, "物品等级"),
            ((int)EquipmentSortMode.Source, "来源"),
        ];

        private static readonly (int Value, string Label)[] ExpansionChips =
        [
            ((int)ExpansionCategory.ARealmReborn, "2.x 新生"),
            ((int)ExpansionCategory.Heavensward, "3.x 苍穹"),
            ((int)ExpansionCategory.Stormblood, "4.x 红莲"),
            ((int)ExpansionCategory.Shadowbringers, "5.x 暗影"),
            ((int)ExpansionCategory.Endwalker, "6.x 晓月"),
            ((int)ExpansionCategory.Dawntrail, "7.x 黄金"),
        ];

        private static readonly (int Value, string Label)[] SourceChips =
        [
            ((int)SourceCategory.Dungeon, "副本"),
            ((int)SourceCategory.Trial, "讨伐 / 极神"),
            ((int)SourceCategory.Savage, "零式 / 高难"),
            ((int)SourceCategory.Crafting, "制作"),
            ((int)SourceCategory.Shop, "商店购买"),
            ((int)SourceCategory.CurrencyExchange, "货币兑换"),
            ((int)SourceCategory.GoldSaucer, "金碟"),
            ((int)SourceCategory.Pvp, "PVP"),
            ((int)SourceCategory.SeasonalEvent, "季节活动"),
            ((int)SourceCategory.Achievement, "成就奖励"),
            ((int)SourceCategory.Quest, "任务奖励"),
            ((int)SourceCategory.MogStation, "莫古站 / 付费商城"),
            ((int)SourceCategory.Unknown, "未知来源"),
        ];

        private static readonly (int Value, string Label)[] TankJobChips =
        [
            ((int)JobFilter.Paladin, "骑士"),
            ((int)JobFilter.Warrior, "战士"),
            ((int)JobFilter.DarkKnight, "黑骑"),
            ((int)JobFilter.Gunbreaker, "绝枪"),
        ];

        private static readonly (int Value, string Label)[] HealerJobChips =
        [
            ((int)JobFilter.WhiteMage, "白魔"),
            ((int)JobFilter.Scholar, "学者"),
            ((int)JobFilter.Astrologian, "占星"),
            ((int)JobFilter.Sage, "贤者"),
        ];

        private static readonly (int Value, string Label)[] MeleeJobChips =
        [
            ((int)JobFilter.Monk, "武僧"),
            ((int)JobFilter.Dragoon, "龙骑"),
            ((int)JobFilter.Ninja, "忍者"),
            ((int)JobFilter.Samurai, "武士"),
            ((int)JobFilter.Reaper, "镰刀"),
            ((int)JobFilter.Viper, "蝰蛇"),
        ];

        private static readonly (int Value, string Label)[] RangedJobChips =
        [
            ((int)JobFilter.Bard, "诗人"),
            ((int)JobFilter.Machinist, "机工"),
            ((int)JobFilter.Dancer, "舞者"),
        ];

        private static readonly (int Value, string Label)[] CasterJobChips =
        [
            ((int)JobFilter.BlackMage, "黑魔"),
            ((int)JobFilter.Summoner, "召唤"),
            ((int)JobFilter.RedMage, "赤魔"),
            ((int)JobFilter.BlueMage, "青魔"),
            ((int)JobFilter.Pictomancer, "绘灵法师"),
        ];

        private static readonly (int Value, string Label)[] CrafterGathererJobChips =
        [
            ((int)JobFilter.Carpenter, "刻木"),
            ((int)JobFilter.Blacksmith, "锻铁"),
            ((int)JobFilter.Armorer, "铸甲"),
            ((int)JobFilter.Goldsmith, "雕金"),
            ((int)JobFilter.Leatherworker, "制革"),
            ((int)JobFilter.Weaver, "裁衣"),
            ((int)JobFilter.Alchemist, "炼金"),
            ((int)JobFilter.Culinarian, "烹调"),
            ((int)JobFilter.Miner, "采矿"),
            ((int)JobFilter.Botanist, "园艺"),
            ((int)JobFilter.Fisher, "捕鱼"),
        ];

        private static readonly (int Value, string Label)[] SlotChips =
        [
            ((int)EquipSlotFilter.Weapon, "武器"),
            ((int)EquipSlotFilter.Shield, "盾"),
            ((int)EquipSlotFilter.Head, "头"),
            ((int)EquipSlotFilter.Body, "身"),
            ((int)EquipSlotFilter.Hands, "手"),
            ((int)EquipSlotFilter.Waist, "腰"),
            ((int)EquipSlotFilter.Legs, "腿"),
            ((int)EquipSlotFilter.Feet, "脚"),
            ((int)EquipSlotFilter.Earrings, "耳饰"),
            ((int)EquipSlotFilter.Necklace, "项链"),
            ((int)EquipSlotFilter.Bracelets, "手镯"),
            ((int)EquipSlotFilter.Ring, "戒指"),
        ];

        private readonly EquipmentFilterService filterService = new();
        private readonly List<EquipmentViewModel> filteredItems = [];
        private string searchText = string.Empty;
        private string cachedSearchText = string.Empty;
        private string cachedFilterKey = string.Empty;
        private string tryOnStatusText = string.Empty;
        private bool tryOnStatusIsError;
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
                ImGui.Text("当前为早期测试版本，开发中");

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
            var activeFilterCount = this.filterService.GetActiveFilterCount(this.searchText, plugin.Configuration.Filters);
            ImGui.TextUnformatted($"Showing {filtered.Count} / {plugin.Ownership.ViewModels.Count} | 已拥有 {plugin.Ownership.OwnedItemCount} | 启用筛选 {activeFilterCount}");

            this.DrawTryOnStatus();
            this.DrawEquipmentTable(filtered);
        }

        private void DrawCollectionControls(Plugin plugin)
        {
            var config = plugin.Configuration;
            var filters = config.Filters;
            filters.EnsureLists();

            if (ImGui.Button(filters.IsFilterPanelOpen ? "隐藏筛选" : "显示筛选"))
            {
                filters.IsFilterPanelOpen = !filters.IsFilterPanelOpen;
                SaveFilterChange(config);
            }

            ImGui.SameLine();
            if (ImGui.Button("Clear All Filters"))
                ClearAllFilters(config);

            ImGui.SameLine();
            if (ImGui.Button("Reset View"))
                ResetView(plugin);

            if (!filters.IsFilterPanelOpen)
            {
                ImGui.Spacing();
                return;
            }

            ImGui.Spacing();
            DrawFilterSection("显示", false);
            DrawSingleChipGrid("display", DisplayModeChips, config.EquipmentDisplayMode, value =>
            {
                config.EquipmentDisplayMode = value;
                config.Save();
                plugin.Ownership.Refresh();
                InvalidateFilterCache();
            }, allowToggleOff: false);

            DrawFilterSection("常用筛选", false);
            ImGui.SetNextItemWidth(Math.Max(260f, ImGui.GetContentRegionAvail().X * 0.45f));
            if (ImGui.InputText("搜索##search", ref searchText, 128))
                InvalidateFilterCache();

            DrawSingleChipGrid("ownership", OwnershipChips, filters.OwnershipFilter, value =>
            {
                filters.OwnershipFilter = value;
                SaveFilterChange(config);
            });

            DrawMultiChipGroup("职业", "jobs", filters.SelectedJobs, config, false, TankJobChips, HealerJobChips, MeleeJobChips, RangedJobChips, CasterJobChips, CrafterGathererJobChips);
            DrawMultiChipGroup("部位", "slots", filters.SelectedSlots, config, false, SlotChips);

            if (ImGui.Button(filters.IsAdvancedFilterOpen ? "隐藏高级筛选" : "显示高级筛选"))
            {
                filters.IsAdvancedFilterOpen = !filters.IsAdvancedFilterOpen;
                SaveFilterChange(config);
            }

            if (filters.IsAdvancedFilterOpen)
            {
                DrawFilterSection("高级筛选", false);
                DrawMultiChipGroup("资料片 / 大版本", "expansions", filters.SelectedExpansions, config, true, ExpansionChips);
                DrawMultiChipGroup("来源类型", "sources", filters.SelectedSourceCategories, config, true, SourceChips);

                DrawSingleChipGrid("quality", QualityChips, filters.QualityFilter, value =>
                {
                    filters.QualityFilter = value;
                    SaveFilterChange(config);
                });

                DrawSingleChipGrid("dye", DyeChips, filters.DyeFilter, value =>
                {
                    filters.DyeFilter = value;
                    SaveFilterChange(config);
                });

                DrawSingleChipGrid("sameModel", SameModelChips, filters.SameModelFilter, value =>
                {
                    filters.SameModelFilter = value;
                    SaveFilterChange(config);
                });

                DrawLevelRanges(filters, config);
                DrawSortControls(filters, config);
            }

            ImGui.Spacing();
        }

        private static void DrawFilterSection(string label, bool sameLine)
        {
            if (sameLine)
                ImGui.SameLine();
            ImGui.Spacing();
            ImGui.TextUnformatted(label);
            ImGui.Separator();
        }

        private void ClearAllFilters(Configuration config)
        {
            searchText = string.Empty;
            config.Filters.ClearFilters();
            SaveFilterChange(config);
        }

        private void ResetView(Plugin plugin)
        {
            searchText = string.Empty;
            plugin.Configuration.Filters.ResetView();
            plugin.Configuration.EquipmentDisplayMode = (int)EquipmentDisplayMode.ByItem;
            plugin.Configuration.Save();
            plugin.Ownership.Refresh();
            InvalidateFilterCache();
        }

        private IReadOnlyList<EquipmentViewModel> GetFilteredItems(OwnershipService ownership, Configuration config)
        {
            var filterKey = filterService.BuildFilterKey(this.searchText, config.Filters);
            if (this.cachedOwnershipVersion == ownership.Version
                && string.Equals(this.cachedFilterKey, filterKey, StringComparison.Ordinal))
                return this.filteredItems;

            this.filteredItems.Clear();
            this.cachedSearchText = this.searchText;
            this.cachedFilterKey = filterKey;
            this.cachedOwnershipVersion = ownership.Version;
            this.filteredItems.AddRange(filterService.Apply(this.searchText, ownership.ViewModels, config.Filters));
            return this.filteredItems;
        }

        private void SaveFilterChange(Configuration config)
        {
            config.Filters.EnsureLists();
            config.Save();
            InvalidateFilterCache();
        }

        private void DrawSingleChipGrid(
            string id,
            IReadOnlyList<(int Value, string Label)> chips,
            int currentValue,
            Action<int> setValue,
            bool allowToggleOff = true)
        {
            DrawChipGrid(id, chips, value => currentValue == value, value =>
            {
                if (allowToggleOff && currentValue == value && value != 0)
                    setValue(0);
                else
                    setValue(value);
            });
        }

        private void DrawMultiChipGroup(
            string label,
            string id,
            List<int> selectedValues,
            Configuration config,
            bool advanced,
            params (int Value, string Label)[][] groups)
        {
            ImGui.Spacing();
            ImGui.TextUnformatted(label);
            if (selectedValues.Count > 0)
            {
                ImGui.SameLine();
                if (ImGui.SmallButton($"清空##clear_{id}"))
                {
                    selectedValues.Clear();
                    SaveFilterChange(config);
                }
            }

            var groupIndex = 0;
            foreach (var group in groups)
            {
                if (groups.Length > 1)
                {
                    ImGui.TextDisabled(GetJobGroupLabel(groupIndex));
                    groupIndex++;
                }

                DrawChipGrid($"{id}_{groupIndex}_{advanced}", group, selectedValues.Contains, value =>
                {
                    if (!selectedValues.Remove(value))
                        selectedValues.Add(value);
                    SaveFilterChange(config);
                });
            }
        }

        private void DrawLevelRanges(FilterState filters, Configuration config)
        {
            ImGui.Spacing();
            ImGui.TextUnformatted("等级范围");

            var equipMin = filters.EquipLevelMin;
            var equipMax = filters.EquipLevelMax;
            ImGui.SetNextItemWidth(90f);
            var changed = ImGui.InputInt("装备等级 Min", ref equipMin);
            ImGui.SameLine();
            ImGui.SetNextItemWidth(90f);
            changed |= ImGui.InputInt("Max##equipLevelMax", ref equipMax);

            var itemMin = filters.ItemLevelMin;
            var itemMax = filters.ItemLevelMax;
            ImGui.SetNextItemWidth(90f);
            changed |= ImGui.InputInt("物品等级 Min", ref itemMin);
            ImGui.SameLine();
            ImGui.SetNextItemWidth(90f);
            changed |= ImGui.InputInt("Max##itemLevelMax", ref itemMax);

            if (changed)
            {
                filters.EquipLevelMin = Math.Max(0, equipMin);
                filters.EquipLevelMax = Math.Max(0, equipMax);
                filters.ItemLevelMin = Math.Max(0, itemMin);
                filters.ItemLevelMax = Math.Max(0, itemMax);
                SaveFilterChange(config);
            }
        }

        private void DrawSortControls(FilterState filters, Configuration config)
        {
            ImGui.Spacing();
            ImGui.TextUnformatted("排序");
            DrawSingleChipGrid("sort", SortChips, filters.SortMode, value =>
            {
                filters.SortMode = value;
                SaveFilterChange(config);
            }, allowToggleOff: false);

            var sortDescending = filters.SortDescending;
            if (ImGui.Checkbox("降序", ref sortDescending))
            {
                filters.SortDescending = sortDescending;
                SaveFilterChange(config);
            }
        }

        private static string GetJobGroupLabel(int index)
            => index switch
            {
                0 => "坦克",
                1 => "治疗",
                2 => "近战",
                3 => "远敏",
                4 => "法系",
                _ => "生产采集",
            };

        private static void DrawChipGrid(
            string id,
            IReadOnlyList<(int Value, string Label)> chips,
            Func<int, bool> isSelected,
            Action<int> onClick)
        {
            var style = ImGui.GetStyle();
            var startX = ImGui.GetCursorScreenPos().X;
            var rightX = startX + ImGui.GetContentRegionAvail().X;
            var lineX = startX;
            var first = true;

            foreach (var chip in chips)
            {
                var width = ImGui.CalcTextSize(chip.Label).X + style.FramePadding.X * 2f + 2f;
                if (!first)
                {
                    if (lineX + style.ItemSpacing.X + width <= rightX)
                    {
                        ImGui.SameLine();
                        lineX += style.ItemSpacing.X;
                    }
                    else
                    {
                        lineX = startX;
                    }
                }

                if (DrawChip($"{id}_{chip.Value}", chip.Label, isSelected(chip.Value)))
                    onClick(chip.Value);

                lineX += width;
                first = false;
            }

            ImGui.Spacing();
        }

        private static bool DrawChip(string id, string label, bool selected)
        {
            if (selected)
            {
                ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.18f, 0.42f, 0.72f, 1f));
                ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.24f, 0.52f, 0.85f, 1f));
                ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(0.15f, 0.34f, 0.62f, 1f));
            }

            var clicked = ImGui.Button($"{label}##{id}");

            if (selected)
                ImGui.PopStyleColor(3);

            return clicked;
        }

        private void DrawEquipmentTable(IReadOnlyList<EquipmentViewModel> items)
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
                    this.DrawEquipmentRow(items[index], rowHeight, iconSize);
            }

            ImGui.EndTable();
        }

        private void DrawEquipmentRow(EquipmentViewModel item, float rowHeight, float iconSize)
        {
            ImGui.TableNextRow(rowHeight);
            var rowMinY = ImGui.GetCursorScreenPos().Y;
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

            var rowHovered = IsCurrentTableRowHovered(rowMinY, rowHeight);
            if (rowHovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
                this.TryOnEquipment(item);

            if (isHovered || rowHovered)
                DrawEquipmentTooltip(item);
        }

        private void TryOnEquipment(EquipmentViewModel item)
        {
            var result = Plugin.Instance.TryOn.TryOn(item);
            this.tryOnStatusIsError = !result.Success;
            this.tryOnStatusText = result.Success
                ? $"试穿：{item.Item.Name}"
                : result.ErrorMessage ?? "试穿失败。";
        }

        private void DrawTryOnStatus()
        {
            if (string.IsNullOrWhiteSpace(this.tryOnStatusText))
                return;

            var color = this.tryOnStatusIsError
                ? new Vector4(0.95f, 0.32f, 0.28f, 1f)
                : new Vector4(0.25f, 0.9f, 0.4f, 1f);
            ImGui.TextColored(color, this.tryOnStatusText);
        }

        private static bool IsCurrentTableRowHovered(float rowMinY, float rowHeight)
        {
            var rowMin = new Vector2(ImGui.GetWindowPos().X, rowMinY);
            var rowMax = new Vector2(rowMin.X + ImGui.GetWindowWidth(), rowMinY + rowHeight);
            return ImGui.IsMouseHoveringRect(rowMin, rowMax, true);
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

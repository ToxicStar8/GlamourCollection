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
using System.Threading;
using System.Threading.Tasks;

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

        private static readonly (int Value, string Label)[] AppearanceMatchModeChips =
        [
            ((int)EquipmentAppearanceMatchMode.Strict, "严格同模"),
            ((int)EquipmentAppearanceMatchMode.Loose, "宽松同模（防具/饰品）"),
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

        private static readonly (int Value, string Label)[] DetailDataChips =
        [
            ((int)EquipmentDetailDataFilter.All, "全部详细数据"),
            ((int)EquipmentDetailDataFilter.HasDetailedData, "已获取详细数据"),
            ((int)EquipmentDetailDataFilter.MissingDetailedData, "未获取详细数据"),
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
            ((int)ExpansionCategory.Unknown, "未知大版本"),
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
            ((int)SourceCategory.DeepDungeon, "深层迷宫"),
            ((int)SourceCategory.FieldOperation, "特殊探索区域"),
            ((int)SourceCategory.TreasureMap, "藏宝图"),
            ((int)SourceCategory.Other, "其他来源"),
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
        private const float FilterLabelWidth = 150f;
        private const int MaxBulkSourceFetchCount = 1000;
        private const int BulkSourceFetchIntervalMilliseconds = 500;
        private string searchText = string.Empty;
        private string cachedSearchText = string.Empty;
        private string cachedFilterKey = string.Empty;
        private string tryOnStatusText = string.Empty;
        private bool tryOnStatusIsError;
        private readonly Dictionary<uint, Task<GarlandSourceFetchResult>> sourceFetchTasks = [];
        private CancellationTokenSource sourceFetchCts = new();
        private Task? bulkSourceFetchTask;
        private CancellationTokenSource? bulkSourceFetchCts;
        private IReadOnlyList<uint> bulkSourceFetchItemIds = [];
        private int bulkSourceFetchCompleted;
        private int bulkSourceFetchTotal;
        private string sourceFetchStatusText = string.Empty;
        private bool sourceFetchStatusIsError;
        private int cachedOwnershipVersion = -1;
        private bool disposed;

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

                ImGui.Separator();
                ImGui.TextUnformatted("拥有标识");

                var showHoveredItemOwnershipOverlay = config.ShowHoveredItemOwnershipOverlay;
                if (ImGui.Checkbox("显示拥有标识", ref showHoveredItemOwnershipOverlay))
                {
                    config.ShowHoveredItemOwnershipOverlay = showHoveredItemOwnershipOverlay;
                    config.Save();
                }

                var hoveredItemOwnershipUseSameModel = config.HoveredItemOwnershipUseSameModel;
                if (ImGui.Checkbox("计算同模装备", ref hoveredItemOwnershipUseSameModel))
                {
                    config.HoveredItemOwnershipUseSameModel = hoveredItemOwnershipUseSameModel;
                    config.Save();
                }

                ImGui.TextUnformatted("同模规则");
                ImGui.SameLine();
                if (ImGui.RadioButton("严格同模##settingsStrictAppearance", config.EquipmentAppearanceMatchMode == (int)EquipmentAppearanceMatchMode.Strict))
                {
                    config.EquipmentAppearanceMatchMode = (int)EquipmentAppearanceMatchMode.Strict;
                    config.Save();
                    Plugin.Instance.Ownership.Refresh();
                    InvalidateFilterCache();
                }

                ImGui.SameLine();
                if (ImGui.RadioButton("宽松同模（防具/饰品）##settingsLooseAppearance", config.EquipmentAppearanceMatchMode == (int)EquipmentAppearanceMatchMode.Loose))
                {
                    config.EquipmentAppearanceMatchMode = (int)EquipmentAppearanceMatchMode.Loose;
                    config.Save();
                    Plugin.Instance.Ownership.Refresh();
                    InvalidateFilterCache();
                }

                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem(Lang.About))
            {
                ImGui.Text("当前为早期测试版本，开发中");
                ImGui.Text("首次使用时，需先打开雇员/鸟包/幻化柜/收藏柜，才可获取缓存数据");
                ImGui.Separator();
                ImGui.TextUnformatted("详细数据来源");
                ImGui.TextWrapped("装备来源与版本详细数据按需请求自 Garland Tools CN。本插件只缓存玩家主动请求过的装备详情。");

                if (ImGui.Button("打开 Garland Tools CN"))
                    OpenUrl("https://garlandtools.cn/db/");

                ImGui.SameLine();
                if (ImGui.Button("支持 Garland Tools CN 爱发电"))
                    OpenUrl("https://afdian.com/a/cyanclay");

                if (ImGui.Button(Lang.SendIssue))
                {
                    var url = "https://discord.gg/GWMEY9P9BX";
                    OpenUrl(url);
                }

                ImGui.EndTabItem();
            }

            ImGui.EndTabBar();
        }

        public override void OnClose()
        {
            StopAllSourceFetches("窗口已关闭，已停止详细数据请求。", recreateCancellationSource: true, waitForStop: false);
            base.OnClose();
        }

        private void DrawCollection()
        {
            var plugin = Plugin.Instance;
            PollSourceFetchTasks(plugin);
            PollBulkSourceFetchTask(plugin);

            if (ImGui.Button("重新扫描背包/军械库"))
                plugin.RescanOwnedItems();

            ImGui.SameLine();
            if (ImGui.Button("清除雇员缓存"))
                plugin.ClearRetainerInventoryCache();

            ImGui.SameLine();
            if (ImGui.Button("清除陆行鸟背包缓存"))
                plugin.ClearSaddlebagInventoryCache();

            ImGui.SameLine();
            if (ImGui.Button("清除幻化柜缓存"))
                plugin.ClearGlamourDresserInventoryCache();

            ImGui.SameLine();
            if (ImGui.Button("清除收藏柜缓存"))
                plugin.ClearArmoireInventoryCache();

            ImGui.SameLine();
            ImGui.TextUnformatted(plugin.LastInventoryScanStatus);

            if (plugin.LastInventoryScanAt is { } lastScan)
                ImGui.TextDisabled(lastScan.ToString("yyyy-MM-dd HH:mm:ss"));

            ImGui.Separator();
            ImGui.Spacing();
            DrawCollectionControls(plugin);

            var filtered = GetFilteredItems(plugin.Ownership, plugin.Configuration);
            var activeFilterCount = this.filterService.GetActiveFilterCount(this.searchText, plugin.Configuration.Filters);
            ImGui.TextUnformatted($"显示 {filtered.Count} / {plugin.Ownership.ViewModels.Count} | 已拥有 {plugin.Ownership.OwnedItemCount} | 启用筛选 {activeFilterCount}");

            this.DrawTryOnStatus();
            this.DrawSourceFetchStatus();
            this.DrawEquipmentTable(filtered);
        }

        private void DrawCollectionControls(Plugin plugin)
        {
            var config = plugin.Configuration;
            var filters = config.Filters;
            filters.EnsureLists();
            var activeFilterCount = this.filterService.GetActiveFilterCount(this.searchText, filters);
            var filtered = GetFilteredItems(plugin.Ownership, config);

            ImGui.SetNextItemWidth(Math.Max(220f, Math.Min(420f, ImGui.GetContentRegionAvail().X * 0.42f)));
            if (ImGui.InputText("搜索##search", ref searchText, 128))
                InvalidateFilterCache();

            ImGui.SameLine();
            if (ImGui.Button($"筛选 ({activeFilterCount})##openFilters"))
                ImGui.OpenPopup("##filterPopup");

            ImGui.SameLine();
            if (ImGui.Button("清空筛选"))
                ClearAllFilters(config);

            ImGui.SameLine();
            if (ImGui.Button("重置视图"))
                ResetView(plugin);

            ImGui.SameLine();
            var missingDetailCount = CountMissingDetailedData(plugin, filtered);
            var bulkRequestCount = Math.Min(missingDetailCount, MaxBulkSourceFetchCount);
            var bulkEstimatedDuration = FormatBulkFetchDuration(bulkRequestCount);
            var isBulkFetching = IsBulkSourceFetchRunning();
            ImGui.BeginDisabled(isBulkFetching || missingDetailCount == 0);
            if (ImGui.Button($"一键获取详细数据 ({bulkRequestCount})##bulkGarlandSource"))
                StartBulkSourceFetch(plugin, filtered);
            ImGui.EndDisabled();
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip($"只请求当前筛选结果中未获取详细数据的装备，一次最多 {MaxBulkSourceFetchCount} 条，每 0.5 秒请求 1 条。预计耗时：{bulkEstimatedDuration}");

            if (!isBulkFetching && bulkRequestCount > 0)
            {
                ImGui.SameLine();
                ImGui.TextDisabled($"预计 {bulkEstimatedDuration}");
            }

            if (isBulkFetching)
            {
                ImGui.SameLine();
                ImGui.TextDisabled($"剩余约 {FormatBulkFetchDuration(Math.Max(0, this.bulkSourceFetchTotal - this.bulkSourceFetchCompleted))}");
                ImGui.SameLine();
                if (ImGui.SmallButton("停止请求##cancelBulkGarlandSource"))
                    CancelBulkSourceFetch();
            }

            ImGui.SetNextWindowSize(new Vector2(900f, 680f), ImGuiCond.Always);
            if (ImGui.BeginPopup("##filterPopup", ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoSavedSettings))
            {
                DrawFilterPopup(plugin);
                ImGui.EndPopup();
            }

            ImGui.Spacing();
        }

        private void DrawFilterPopup(Plugin plugin)
        {
            var config = plugin.Configuration;
            var filters = config.Filters;
            filters.EnsureLists();

            ImGui.TextUnformatted("筛选");
            ImGui.SameLine();
            ImGui.TextDisabled($"{this.filterService.GetActiveFilterCount(this.searchText, filters)} 个已启用");

            if (ImGui.SmallButton("清空全部##popupClearFilters"))
                ClearAllFilters(config);

            ImGui.SameLine();
            if (ImGui.SmallButton("关闭##closeFilterPopup"))
                ImGui.CloseCurrentPopup();

            DrawFilterSection("基础", false);
            DrawSingleCheckboxGroup("显示", "display", DisplayModeChips, config.EquipmentDisplayMode, value =>
            {
                config.EquipmentDisplayMode = value;
                config.Save();
                plugin.Ownership.Refresh();
                InvalidateFilterCache();
            }, allowToggleOff: false);

            DrawSingleCheckboxGroup("同模规则", "appearanceMatch", AppearanceMatchModeChips, config.EquipmentAppearanceMatchMode, value =>
            {
                config.EquipmentAppearanceMatchMode = value;
                config.Save();
                plugin.Ownership.Refresh();
                InvalidateFilterCache();
            }, allowToggleOff: false);

            DrawFilterSection("常用筛选", false);
            DrawSingleCheckboxGroup("拥有状态", "ownership", OwnershipChips, filters.OwnershipFilter, value =>
            {
                filters.OwnershipFilter = value;
                SaveFilterChange(config);
            });

            DrawMultiCheckboxGroup("职业", "jobs", filters.SelectedJobs, config, false, TankJobChips, HealerJobChips, MeleeJobChips, RangedJobChips, CasterJobChips, CrafterGathererJobChips);
            DrawMultiCheckboxGroup("部位", "slots", filters.SelectedSlots, config, false, SlotChips);

            DrawFilterSection("高级筛选", false);
            DrawMultiCheckboxGroup("大版本", "expansions", filters.SelectedExpansions, config, true, ExpansionChips);
            DrawMultiCheckboxGroup("来源类型", "sources", filters.SelectedSourceCategories, config, true, SourceChips);

            DrawSingleCheckboxGroup("详细数据", "detailData", DetailDataChips, filters.DetailDataFilter, value =>
            {
                filters.DetailDataFilter = value;
                SaveFilterChange(config);
            });

            DrawSingleCheckboxGroup("品质", "quality", QualityChips, filters.QualityFilter, value =>
            {
                filters.QualityFilter = value;
                SaveFilterChange(config);
            });

            DrawSingleCheckboxGroup("染色", "dye", DyeChips, filters.DyeFilter, value =>
            {
                filters.DyeFilter = value;
                SaveFilterChange(config);
            });

            DrawSingleCheckboxGroup("同模", "sameModel", SameModelChips, filters.SameModelFilter, value =>
            {
                filters.SameModelFilter = value;
                SaveFilterChange(config);
            });

            DrawLevelRanges(filters, config);
            DrawSortControls(filters, config);
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

        private void DrawSingleCheckboxGroup(
            string label,
            string id,
            IReadOnlyList<(int Value, string Label)> options,
            int currentValue,
            Action<int> setValue,
            bool allowToggleOff = true)
        {
            ImGui.Spacing();
            DrawInlineFilterLabel(label, false, id, null);
            DrawCheckboxGrid(id, options, value => currentValue == value, value =>
            {
                if (allowToggleOff && currentValue == value && value != 0)
                    setValue(0);
                else
                    setValue(value);
            });
        }

        private void DrawMultiCheckboxGroup(
            string label,
            string id,
            List<int> selectedValues,
            Configuration config,
            bool advanced,
            params (int Value, string Label)[][] groups)
        {
            ImGui.Spacing();
            if (groups.Length == 1)
            {
                DrawInlineFilterLabel(label, selectedValues.Count > 0, id, () =>
                {
                    selectedValues.Clear();
                    SaveFilterChange(config);
                });

                DrawCheckboxGrid($"{id}_0_{advanced}", groups[0], selectedValues.Contains, value =>
                {
                    if (!selectedValues.Remove(value))
                        selectedValues.Add(value);
                    SaveFilterChange(config);
                });
                return;
            }

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
                DrawInlineFilterLabel(GetJobGroupLabel(groupIndex), false, $"{id}_{groupIndex}", null);

                DrawCheckboxGrid($"{id}_{groupIndex}_{advanced}", group, selectedValues.Contains, value =>
                {
                    if (!selectedValues.Remove(value))
                        selectedValues.Add(value);
                    SaveFilterChange(config);
                });

                groupIndex++;
            }
        }

        private void DrawLevelRanges(FilterState filters, Configuration config)
        {
            ImGui.Spacing();
            DrawInlineFilterLabel("等级范围", false, "levels", null);

            var equipMin = filters.EquipLevelMin;
            var equipMax = filters.EquipLevelMax;
            ImGui.SetNextItemWidth(72f);
            var changed = ImGui.InputInt("装备等级下限", ref equipMin);
            ImGui.SameLine();
            ImGui.SetNextItemWidth(72f);
            changed |= ImGui.InputInt("上限##equipLevelMax", ref equipMax);

            var itemMin = filters.ItemLevelMin;
            var itemMax = filters.ItemLevelMax;
            ImGui.SameLine();
            ImGui.SetNextItemWidth(72f);
            changed |= ImGui.InputInt("物品等级下限", ref itemMin);
            ImGui.SameLine();
            ImGui.SetNextItemWidth(72f);
            changed |= ImGui.InputInt("上限##itemLevelMax", ref itemMax);

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
            DrawSingleCheckboxGroup("排序", "sort", SortChips, filters.SortMode, value =>
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

        private static void DrawInlineFilterLabel(string label, bool showClear, string id, Action? onClear)
        {
            ImGui.AlignTextToFramePadding();
            ImGui.TextUnformatted(label);

            if (showClear && onClear != null)
            {
                ImGui.SameLine();
                if (ImGui.SmallButton($"清空##clear_{id}"))
                    onClear();
            }

            ImGui.SameLine(FilterLabelWidth);
        }

        private static void DrawCheckboxGrid(
            string id,
            IReadOnlyList<(int Value, string Label)> options,
            Func<int, bool> isSelected,
            Action<int> onClick)
        {
            var style = ImGui.GetStyle();
            var startX = ImGui.GetCursorScreenPos().X;
            var rightX = startX + ImGui.GetContentRegionAvail().X;
            var lineX = startX;
            var first = true;

            foreach (var option in options)
            {
                var width = ImGui.GetFrameHeight()
                            + style.ItemInnerSpacing.X
                            + ImGui.CalcTextSize(option.Label).X
                            + style.ItemSpacing.X;
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

                var selected = isSelected(option.Value);
                if (ImGui.Checkbox($"{option.Label}##{id}_{option.Value}", ref selected))
                    onClick(option.Value);

                lineX += width;
                first = false;
            }

            ImGui.Spacing();
        }

        private void DrawEquipmentTable(IReadOnlyList<EquipmentViewModel> items)
        {
            const float rowHeight = 36f;
            var tableFlags = ImGuiTableFlags.Borders
                             | ImGuiTableFlags.RowBg
                             | ImGuiTableFlags.Resizable
                             | ImGuiTableFlags.Reorderable
                             | ImGuiTableFlags.Hideable
                             | ImGuiTableFlags.ScrollY
                             | ImGuiTableFlags.ScrollX
                             | ImGuiTableFlags.SizingFixedFit;

            var tableHeight = Math.Max(160f, ImGui.GetContentRegionAvail().Y);
            if (!ImGui.BeginTable("##equipmentList", 10, tableFlags, new Vector2(-1, tableHeight)))
                return;

            var iconSize = GetIconSize(rowHeight);
            ImGui.TableSetupColumn("图标", ImGuiTableColumnFlags.WidthFixed, iconSize + 10f);
            ImGui.TableSetupColumn("名称", ImGuiTableColumnFlags.WidthFixed, 280f);
            ImGui.TableSetupColumn("分类", ImGuiTableColumnFlags.WidthFixed, 110f);
            ImGui.TableSetupColumn("装等", ImGuiTableColumnFlags.WidthFixed, 56f);
            ImGui.TableSetupColumn("品级", ImGuiTableColumnFlags.WidthFixed, 56f);
            ImGui.TableSetupColumn("可染", ImGuiTableColumnFlags.WidthFixed, 56f);
            ImGui.TableSetupColumn("来源", ImGuiTableColumnFlags.WidthFixed, 420f);
            ImGui.TableSetupColumn("同模", ImGuiTableColumnFlags.WidthFixed, 64f);
            ImGui.TableSetupColumn("拥有", ImGuiTableColumnFlags.WidthFixed, 124f);
            ImGui.TableSetupColumn("详细数据", ImGuiTableColumnFlags.WidthFixed, 116f);
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
            var isHovered = false;
            var tryOnHovered = false;

            ImGui.TableSetColumnIndex(0);
            DrawItemIcon(item.Item.IconId, iconSize);
            var iconHovered = ImGui.IsItemHovered();
            isHovered |= iconHovered;
            tryOnHovered |= iconHovered;

            ImGui.TableSetColumnIndex(1);
            ImGui.AlignTextToFramePadding();
            ImGui.TextUnformatted(item.Item.Name);
            var nameHovered = ImGui.IsItemHovered();
            isHovered |= nameHovered;
            tryOnHovered |= nameHovered;

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

            ImGui.TableSetColumnIndex(9);
            DrawSourceCacheButton(item);

            if (tryOnHovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
                this.TryOnEquipment(item);

            if (isHovered)
                DrawEquipmentTooltip(item);
        }

        private void DrawSourceCacheButton(EquipmentViewModel item)
        {
            var itemId = item.Item.ItemId;
            var plugin = Plugin.Instance;
            var isLoading = this.sourceFetchTasks.ContainsKey(itemId);
            var hasCache = plugin.GarlandSources.HasCachedSource(itemId);
            var hasDetailedData = plugin.GarlandSources.HasCachedDetailedData(itemId);
            var label = isLoading
                ? "请求中"
                : hasCache
                    ? "更新详细数据"
                    : "获取详细数据";

            ImGui.BeginDisabled(isLoading);
            if (ImGui.SmallButton($"{label}##garlandSource_{itemId}"))
                StartSourceFetch(item);
            ImGui.EndDisabled();
        }

        private void StartSourceFetch(EquipmentViewModel item)
        {
            if (this.disposed)
                return;

            EnsureSourceFetchCancellationSource();

            var itemId = item.Item.ItemId;
            if (this.sourceFetchTasks.ContainsKey(itemId))
                return;

            this.sourceFetchStatusIsError = false;
            this.sourceFetchStatusText = $"正在获取详细数据：{item.Item.Name}";
            this.sourceFetchTasks[itemId] = Plugin.Instance.GarlandSources.FetchAndCacheAsync(itemId, this.sourceFetchCts.Token);
        }

        private void StartBulkSourceFetch(Plugin plugin, IReadOnlyList<EquipmentViewModel> items)
        {
            if (this.disposed)
                return;

            if (IsBulkSourceFetchRunning())
                return;

            EnsureSourceFetchCancellationSource();

            var itemIds = items
                .SelectMany(item => item.FilterItems)
                .Select(item => item.ItemId)
                .Distinct()
                .Where(itemId => !plugin.GarlandSources.HasCachedDetailedData(itemId))
                .Take(MaxBulkSourceFetchCount)
                .ToList();

            if (itemIds.Count == 0)
            {
                this.sourceFetchStatusIsError = false;
                this.sourceFetchStatusText = "当前筛选结果没有需要获取的详细数据。";
                return;
            }

            this.bulkSourceFetchCts?.Dispose();
            this.bulkSourceFetchCts = CancellationTokenSource.CreateLinkedTokenSource(this.sourceFetchCts.Token);
            this.bulkSourceFetchItemIds = itemIds;
            this.bulkSourceFetchCompleted = 0;
            this.bulkSourceFetchTotal = itemIds.Count;
            this.sourceFetchStatusIsError = false;
            this.sourceFetchStatusText = $"正在批量获取详细数据：0 / {this.bulkSourceFetchTotal}，预计 {FormatBulkFetchDuration(this.bulkSourceFetchTotal)}";
            this.bulkSourceFetchTask = GarlandBulkSourceFetchRunner.RunAsync(
                plugin.GarlandSources,
                itemIds,
                TimeSpan.FromMilliseconds(BulkSourceFetchIntervalMilliseconds),
                () => Interlocked.Increment(ref this.bulkSourceFetchCompleted),
                this.bulkSourceFetchCts.Token);
        }

        private void CancelBulkSourceFetch()
            => CancelBulkSourceFetch($"已停止批量获取：{this.bulkSourceFetchCompleted} / {this.bulkSourceFetchTotal}");

        private void CancelBulkSourceFetch(string statusText)
        {
            if (!IsBulkSourceFetchRunning())
                return;

            this.bulkSourceFetchCts?.Cancel();
            this.sourceFetchStatusIsError = true;
            this.sourceFetchStatusText = statusText;
        }

        private bool IsBulkSourceFetchRunning()
            => this.bulkSourceFetchTask is { IsCompleted: false };

        private void EnsureSourceFetchCancellationSource()
        {
            if (!this.sourceFetchCts.IsCancellationRequested)
                return;

            this.sourceFetchCts.Dispose();
            this.sourceFetchCts = new CancellationTokenSource();
        }

        private void StopAllSourceFetches(string statusText, bool recreateCancellationSource, bool waitForStop)
        {
            var runningTasks = this.sourceFetchTasks.Values
                .Cast<Task>()
                .Where(task => !task.IsCompleted)
                .ToList();
            if (this.bulkSourceFetchTask is { IsCompleted: false } bulkTask)
                runningTasks.Add(bulkTask);

            this.sourceFetchCts.Cancel();
            this.bulkSourceFetchCts?.Cancel();
            this.sourceFetchTasks.Clear();
            this.bulkSourceFetchTask = null;
            this.bulkSourceFetchItemIds = [];

            this.sourceFetchStatusIsError = true;
            this.sourceFetchStatusText = statusText;

            if (waitForStop && runningTasks.Count > 0)
            {
                try
                {
                    Task.WaitAll(runningTasks.ToArray(), TimeSpan.FromSeconds(1));
                }
                catch (AggregateException)
                {
                }
                catch (OperationCanceledException)
                {
                }
            }

            this.bulkSourceFetchCts?.Dispose();
            this.bulkSourceFetchCts = null;

            if (!recreateCancellationSource)
                return;

            this.sourceFetchCts.Dispose();
            this.sourceFetchCts = new CancellationTokenSource();
        }

        private void PollSourceFetchTasks(Plugin plugin)
        {
            if (this.sourceFetchTasks.Count == 0)
                return;

            foreach (var (itemId, task) in this.sourceFetchTasks.ToList())
            {
                if (!task.IsCompleted)
                    continue;

                this.sourceFetchTasks.Remove(itemId);
                GarlandSourceFetchResult result;
                try
                {
                    result = task.GetAwaiter().GetResult();
                }
                catch (OperationCanceledException)
                {
                    this.sourceFetchStatusIsError = true;
                    this.sourceFetchStatusText = "详细数据请求已停止。";
                    continue;
                }
                catch (Exception ex)
                {
                    this.sourceFetchStatusIsError = true;
                    this.sourceFetchStatusText = $"详细数据请求异常：{ex.Message}";
                    continue;
                }

                this.sourceFetchStatusIsError = !result.Success;
                this.sourceFetchStatusText = result.Success
                    ? $"详细数据已更新：{result.Message}"
                    : result.Message;

                if (result.Success)
                    RefreshSourceData(plugin, [itemId]);
            }
        }

        private void PollBulkSourceFetchTask(Plugin plugin)
        {
            if (this.bulkSourceFetchTask is not { } task)
                return;

            if (!task.IsCompleted)
            {
                this.sourceFetchStatusIsError = false;
                this.sourceFetchStatusText = $"正在批量获取详细数据：{this.bulkSourceFetchCompleted} / {this.bulkSourceFetchTotal}，剩余约 {FormatBulkFetchDuration(Math.Max(0, this.bulkSourceFetchTotal - this.bulkSourceFetchCompleted))}";
                return;
            }

            this.bulkSourceFetchTask = null;

            if (task.IsCanceled)
            {
                if (this.bulkSourceFetchCompleted > 0)
                    RefreshSourceData(plugin, GetCompletedBulkSourceItemIds());

                this.sourceFetchStatusIsError = true;
                this.sourceFetchStatusText = $"已停止批量获取：{this.bulkSourceFetchCompleted} / {this.bulkSourceFetchTotal}";
                return;
            }

            if (task.Exception is not null)
            {
                if (this.bulkSourceFetchCompleted > 0)
                    RefreshSourceData(plugin, GetCompletedBulkSourceItemIds());

                this.sourceFetchStatusIsError = true;
                this.sourceFetchStatusText = $"批量获取详细数据异常：{task.Exception.GetBaseException().Message}";
                return;
            }

            RefreshSourceData(plugin, GetCompletedBulkSourceItemIds());
            this.sourceFetchStatusIsError = false;
            this.sourceFetchStatusText = $"批量获取详细数据完成：{this.bulkSourceFetchCompleted} / {this.bulkSourceFetchTotal}";
            this.bulkSourceFetchItemIds = [];
        }

        private IReadOnlyList<uint> GetCompletedBulkSourceItemIds()
            => this.bulkSourceFetchItemIds
                .Take(Math.Clamp(this.bulkSourceFetchCompleted, 0, this.bulkSourceFetchItemIds.Count))
                .ToList();

        private void RefreshSourceData(Plugin plugin, IReadOnlyList<uint> itemIds)
        {
            var changedIds = itemIds
                .Distinct()
                .Where(itemId => plugin.ItemDatabase.RefreshSourceInfo(itemId))
                .ToList();

            if (changedIds.Count > 0)
                plugin.Ownership.RefreshEquipmentData(changedIds);

            InvalidateFilterCache();
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

        private void DrawSourceFetchStatus()
        {
            if (string.IsNullOrWhiteSpace(this.sourceFetchStatusText))
                return;

            var color = this.sourceFetchStatusIsError
                ? new Vector4(0.95f, 0.32f, 0.28f, 1f)
                : new Vector4(0.25f, 0.72f, 1f, 1f);
            ImGui.TextColored(color, this.sourceFetchStatusText);
        }

        private static string GetOwnedDisplayText(EquipmentViewModel item)
        {
            if (item.HasNormalQuality && item.HasHighQuality)
                return "[已拥有] NQ+HQ";

            if (item.HasHighQuality)
                return "[已拥有] HQ";

            return "[已拥有] NQ";
        }

        private static void DrawEquipmentTooltip(EquipmentViewModel item)
        {
            ImGui.BeginTooltip();
            ImGui.TextUnformatted(item.Item.Name);
            ImGui.TextDisabled($"物品ID: {item.Item.ItemId}");
            ImGui.Separator();
            ImGui.TextUnformatted($"分类: {item.Item.CategoryName}");
            ImGui.TextUnformatted($"职业: {item.Item.ClassJobCategoryName}");
            ImGui.TextUnformatted($"装备等级: {item.Item.EquipLevel}");
            ImGui.TextUnformatted($"物品等级: {item.Item.ItemLevel}");
            ImGui.TextUnformatted($"可染色: {(item.Item.CanBeDyed ? "是" : "否")}");
            ImGui.TextUnformatted($"大版本: {item.Item.ExpansionInfo}");
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
            var includeItemName = locations
                .Select(GetOwnedBaseItemId)
                .Distinct()
                .Skip(1)
                .Any();
            var listHeight = GetTooltipListHeight(locations.Count, 8);
            if (ImGui.BeginChild("##ownedLocationTooltipList", new Vector2(560f, listHeight), true))
            {
                foreach (var location in locations)
                    ImGui.TextUnformatted(FormatOwnedLocationLine(location, includeItemName));
            }

            ImGui.EndChild();
        }

        private static string FormatOwnedLocationLine(OwnedItemRecord location, bool includeItemName)
        {
            var qualityText = GetOwnedQualityText([location]);
            var locationText = OwnedLocationFormatter.Format(location);
            if (!includeItemName)
                return $"{qualityText} {locationText}";

            return $"{GetOwnedLocationItemName(location)} - {qualityText} {locationText}";
        }

        private static string GetOwnedLocationItemName(OwnedItemRecord location)
        {
            var itemId = GetOwnedBaseItemId(location);
            if (itemId != 0 && Plugin.Instance.ItemDatabase.TryGetEquipment(itemId, out var item))
                return item.Name;

            if (!string.IsNullOrWhiteSpace(location.ItemName))
                return location.ItemName.Trim();

            return itemId == 0 ? "未知装备" : $"物品 {itemId}";
        }

        private static void DrawSameModelList(EquipmentViewModel item)
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

        private static int CountMissingDetailedData(Plugin plugin, IReadOnlyList<EquipmentViewModel> items)
            => items
                .SelectMany(item => item.FilterItems)
                .Select(item => item.ItemId)
                .Distinct()
                .Count(itemId => !plugin.GarlandSources.HasCachedDetailedData(itemId));

        private static string FormatBulkFetchDuration(int itemCount)
        {
            if (itemCount <= 0)
                return "0 秒";

            var duration = TimeSpan.FromMilliseconds(itemCount * BulkSourceFetchIntervalMilliseconds);
            if (duration.TotalHours >= 1)
                return $"{(int)duration.TotalHours}小时{duration.Minutes}分";

            if (duration.TotalMinutes >= 1)
                return $"{duration.Minutes}分{duration.Seconds}秒";

            return $"{Math.Max(1, duration.Seconds)}秒";
        }

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

        private static void OpenUrl(string url)
            => Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });

        private void InvalidateFilterCache()
        {
            this.cachedOwnershipVersion = -1;
        }

        public void Dispose()
        {
            this.disposed = true;
            StopAllSourceFetches("插件已卸载，已停止详细数据请求。", recreateCancellationSource: false, waitForStop: true);
            this.sourceFetchCts.Dispose();
        }
    }
}

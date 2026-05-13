using Dalamud.Utility;
using ECommons.DalamudServices;
using Lumina.Excel.Sheets;
using Main.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Main.Services;

public sealed class SourceInfoService(GarlandSourceCacheService garlandSourceCache)
{
    private readonly Dictionary<uint, SourceAccumulator> sourceByItemId = [];
    private bool isLoaded;

    public EquipmentSourceInfo GetSourceInfo(Item item)
    {
        if (this.TryGetGarlandSource(item, out var garlandOnly))
            return garlandOnly;

        return new EquipmentSourceInfo(
            "待获取缓存",
            [SourceCategory.Unknown],
            ExpansionCategory.Unknown,
            "待获取缓存",
            false);
    }

    private void Load()
    {
        if (this.isLoaded)
            return;

        this.sourceByItemId.Clear();
        this.LoadRecipeSources();
        this.LoadGilShopSources();
        this.LoadSpecialShopSources();
        this.LoadCurrencyExchangeSources();
        this.LoadAchievementSources();
        this.LoadQuestSources();
        this.LoadPvpSources();
        this.LoadMogStationSources();

        this.isLoaded = true;
    }

    private void LoadRecipeSources()
    {
        foreach (var recipe in Svc.Data.GetExcelSheet<Recipe>())
            this.AddSource(
                recipe.ItemResult.RowId,
                SourceCategory.Crafting,
                recipe.PatchNumber,
                detail: $"制作{FormatPatchSuffix(recipe.PatchNumber)}");
    }

    private void LoadGilShopSources()
    {
        foreach (var shopItemGroup in Svc.Data.GetSubrowExcelSheet<GilShopItem>())
        {
            foreach (var shopItem in shopItemGroup)
                this.AddSource(
                    shopItem.Item.RowId,
                    SourceCategory.Shop,
                    shopItem.Patch,
                    detail: $"商店购买: NPC金币商店{FormatPatchSuffix(shopItem.Patch)}");
        }
    }

    private void LoadSpecialShopSources()
    {
        foreach (var shop in Svc.Data.GetExcelSheet<SpecialShop>())
        {
            var shopName = shop.Name.ExtractText();
            var shopCategory = GetSpecialShopCategory(shop, shopName);

            foreach (var shopItem in shop.Item)
            {
                var itemCategory = GetSpecialShopItemCategory(shopItem, shopCategory, shopName);
                var detail = GetSpecialShopDetail(itemCategory, shopName, shopItem);
                foreach (var receiveItem in shopItem.ReceiveItems)
                    this.AddSource(receiveItem.Item.RowId, itemCategory, shopItem.PatchNumber, detail: detail);
            }
        }
    }

    private void LoadCurrencyExchangeSources()
    {
        foreach (var shopItemGroup in Svc.Data.GetSubrowExcelSheet<GCScripShopItem>())
        {
            foreach (var shopItem in shopItemGroup)
                this.AddSource(shopItem.Item.RowId, SourceCategory.CurrencyExchange, detail: "货币兑换: 军票/筹码商店");
        }

        foreach (var tomestoneItem in Svc.Data.GetExcelSheet<TomestonesItem>())
            this.AddSource(tomestoneItem.Item.RowId, SourceCategory.CurrencyExchange, detail: "货币兑换: 神典石兑换");

        foreach (var shop in Svc.Data.GetExcelSheet<FccShop>())
        {
            var shopName = shop.Name.ExtractText();
            var detail = string.IsNullOrWhiteSpace(shopName)
                ? "货币兑换: 部队信用兑换"
                : $"货币兑换: {shopName}";
            foreach (var itemData in shop.ItemData)
                this.AddSource(itemData.Item.RowId, SourceCategory.CurrencyExchange, detail: detail);
        }
    }

    private void LoadAchievementSources()
    {
        foreach (var achievement in Svc.Data.GetExcelSheet<Achievement>())
        {
            var achievementName = achievement.Name.ExtractText();
            this.AddSource(
                achievement.Item.RowId,
                SourceCategory.Achievement,
                detail: string.IsNullOrWhiteSpace(achievementName)
                    ? "成就奖励"
                    : $"成就奖励: {achievementName}");
        }

        foreach (var reward in Svc.Data.GetExcelSheet<WKSAchievementRewardItem>())
            this.AddSource(reward.Item.RowId, SourceCategory.Achievement, detail: "成就奖励: 宇宙探索成就");
    }

    private void LoadQuestSources()
    {
        foreach (var quest in Svc.Data.GetExcelSheet<Quest>())
        {
            var expansion = FromExVersionRowId(quest.Expansion.RowId);
            var questName = quest.Name.ExtractText();
            var detail = string.IsNullOrWhiteSpace(questName)
                ? "任务奖励"
                : $"任务奖励: {questName}";
            foreach (var reward in quest.Reward)
                this.AddSource(reward.RowId, SourceCategory.Quest, expansion: expansion, detail: detail);

            foreach (var optionalReward in quest.OptionalItemReward)
                this.AddSource(optionalReward.RowId, SourceCategory.Quest, expansion: expansion, detail: detail);
        }

        foreach (var classJobRewardGroup in Svc.Data.GetSubrowExcelSheet<QuestClassJobReward>())
        {
            foreach (var classJobReward in classJobRewardGroup)
            {
                foreach (var reward in classJobReward.RewardItem)
                    this.AddSource(reward.RowId, SourceCategory.Quest, detail: "任务奖励: 职业任务奖励");
            }
        }
    }

    private void LoadPvpSources()
    {
        foreach (var series in Svc.Data.GetExcelSheet<PvPSeries>())
        {
            foreach (var levelReward in series.LevelRewards)
            {
                foreach (var item in levelReward.LevelRewardItem)
                    this.AddSource(item.RowId, SourceCategory.Pvp, detail: "PVP: 系列赛奖励");
            }
        }
    }

    private void LoadMogStationSources()
    {
        foreach (var itemSet in Svc.Data.GetExcelSheet<FittingShopItemSet>())
        {
            var setName = itemSet.Name.ExtractText();
            var detail = string.IsNullOrWhiteSpace(setName)
                ? "莫古站 / 付费商城"
                : $"莫古站 / 付费商城: {setName}";
            foreach (var item in itemSet.Item)
                this.AddSource(item.RowId, SourceCategory.MogStation, detail: detail);
        }
    }

    private void AddSource(
        uint itemId,
        SourceCategory category,
        ushort patchNumber = 0,
        ExpansionCategory expansion = ExpansionCategory.Unknown,
        string? detail = null)
    {
        if (itemId == 0)
            return;

        if (!this.sourceByItemId.TryGetValue(itemId, out var source))
        {
            source = new SourceAccumulator();
            this.sourceByItemId[itemId] = source;
        }

        source.Categories.Add(category);
        if (!string.IsNullOrWhiteSpace(detail))
            source.Details.Add(detail.Trim());

        if (patchNumber > 0)
        {
            source.PatchNumbers.Add(patchNumber);
            source.ExpansionCandidates.Add(FromPatchNumber(patchNumber));
        }

        if (expansion != ExpansionCategory.Unknown)
            source.ExpansionCandidates.Add(expansion);
    }

    private static SourceCategory GetSpecialShopCategory(SpecialShop shop, string shopName)
    {
        if (shop.RequiredFestival.RowId != 0 || ContainsAny(shopName, "季节", "守护天节", "降神节", "星芒", "红莲节", "女儿节", "Valentione", "Moonfire", "All Saints", "Starlight"))
            return SourceCategory.SeasonalEvent;

        if (ContainsAny(shopName, "金碟", "MGP", "Gold Saucer"))
            return SourceCategory.GoldSaucer;

        if (ContainsAny(shopName, "PVP", "PvP", "对人战绩", "狼印战绩", "Wolf Mark", "Trophy Crystal"))
            return SourceCategory.Pvp;

        if (ContainsAny(shopName, "优雷卡", "博兹雅", "扎杜诺尔", "Eureka", "Bozja", "Zadnor"))
            return SourceCategory.FieldOperation;

        return SourceCategory.CurrencyExchange;
    }

    private static SourceCategory GetSpecialShopItemCategory(
        SpecialShop.ItemStruct item,
        SourceCategory defaultCategory,
        string shopName)
    {
        foreach (var category in item.Category)
        {
            if (category.RowId == 0)
                continue;

            var categoryName = category.Value.Name.ExtractText();
            if (ContainsAny(categoryName, "金碟", "MGP", "Gold Saucer"))
                return SourceCategory.GoldSaucer;
            if (ContainsAny(categoryName, "PVP", "PvP", "对人战绩", "狼印战绩", "Wolf Mark", "Trophy Crystal"))
                return SourceCategory.Pvp;
            if (ContainsAny(categoryName, "优雷卡", "博兹雅", "扎杜诺尔", "Eureka", "Bozja", "Zadnor"))
                return SourceCategory.FieldOperation;
        }

        return defaultCategory;
    }

    private static string GetSpecialShopDetail(
        SourceCategory category,
        string shopName,
        SpecialShop.ItemStruct item)
    {
        var detailName = shopName;
        foreach (var shopCategory in item.Category)
        {
            if (shopCategory.RowId == 0)
                continue;

            var categoryName = shopCategory.Value.Name.ExtractText();
            if (!string.IsNullOrWhiteSpace(categoryName))
            {
                detailName = string.IsNullOrWhiteSpace(detailName)
                    ? categoryName
                    : $"{detailName} - {categoryName}";
                break;
            }
        }

        var label = GetSourceLabel(category);
        if (string.IsNullOrWhiteSpace(detailName))
            return $"{label}{FormatPatchSuffix(item.PatchNumber)}";

        return $"{label}: {detailName}{FormatPatchSuffix(item.PatchNumber)}";
    }

    private static ExpansionCategory EstimateExpansion(uint equipLevel)
        => equipLevel switch
        {
            0 => ExpansionCategory.Unknown,
            <= 50 => ExpansionCategory.ARealmReborn,
            <= 60 => ExpansionCategory.Heavensward,
            <= 70 => ExpansionCategory.Stormblood,
            <= 80 => ExpansionCategory.Shadowbringers,
            <= 90 => ExpansionCategory.Endwalker,
            <= 100 => ExpansionCategory.Dawntrail,
            _ => ExpansionCategory.Unknown,
        };

    private static ExpansionCategory FromExVersionRowId(uint rowId)
        => rowId switch
        {
            0 => ExpansionCategory.ARealmReborn,
            1 => ExpansionCategory.Heavensward,
            2 => ExpansionCategory.Stormblood,
            3 => ExpansionCategory.Shadowbringers,
            4 => ExpansionCategory.Endwalker,
            5 => ExpansionCategory.Dawntrail,
            _ => ExpansionCategory.Unknown,
        };

    private static ExpansionCategory FromPatchNumber(ushort patchNumber)
    {
        if (patchNumber == 0)
            return ExpansionCategory.Unknown;

        var major = patchNumber >= 100
            ? patchNumber / 100
            : patchNumber >= 10
                ? patchNumber / 10
                : patchNumber;

        return major switch
        {
            2 => ExpansionCategory.ARealmReborn,
            3 => ExpansionCategory.Heavensward,
            4 => ExpansionCategory.Stormblood,
            5 => ExpansionCategory.Shadowbringers,
            6 => ExpansionCategory.Endwalker,
            7 => ExpansionCategory.Dawntrail,
            _ => ExpansionCategory.Unknown,
        };
    }

    private static string FormatPatchNumber(ushort patchNumber)
    {
        if (patchNumber >= 100)
            return $"补丁 {patchNumber / 100}.{patchNumber % 100:00}";

        if (patchNumber >= 10)
            return $"补丁 {patchNumber / 10}.{patchNumber % 10}";

        return $"补丁 {patchNumber}.x";
    }

    private static string FormatPatchSuffix(ushort patchNumber)
        => patchNumber > 0 ? $"（约 {FormatPatchNumber(patchNumber)}）" : string.Empty;

    private static string GetExpansionLabel(ExpansionCategory expansion)
        => expansion switch
        {
            ExpansionCategory.ARealmReborn => "2.x 新生",
            ExpansionCategory.Heavensward => "3.x 苍穹",
            ExpansionCategory.Stormblood => "4.x 红莲",
            ExpansionCategory.Shadowbringers => "5.x 暗影",
            ExpansionCategory.Endwalker => "6.x 晓月",
            ExpansionCategory.Dawntrail => "7.x 黄金",
            _ => "未知大版本",
        };

    private static string GetSourceLabel(SourceCategory category)
        => category switch
        {
            SourceCategory.Crafting => "制作",
            SourceCategory.Shop => "商店购买",
            SourceCategory.CurrencyExchange => "货币兑换",
            SourceCategory.GoldSaucer => "金碟",
            SourceCategory.Pvp => "PVP",
            SourceCategory.SeasonalEvent => "季节活动",
            SourceCategory.Achievement => "成就奖励",
            SourceCategory.Quest => "任务奖励",
            SourceCategory.MogStation => "莫古站 / 付费商城",
            SourceCategory.DeepDungeon => "深层迷宫",
            SourceCategory.FieldOperation => "特殊探索区域",
            SourceCategory.TreasureMap => "藏宝图",
            SourceCategory.Other => "其他来源",
            SourceCategory.Unknown => "未知来源",
            SourceCategory.Dungeon => "副本",
            SourceCategory.Trial => "讨伐 / 极神",
            SourceCategory.Savage => "零式 / 高难",
            _ => "未知来源",
        };

    private static int GetSourceSort(SourceCategory category)
        => category == SourceCategory.Unknown ? int.MaxValue : (int)category;

    private static bool ContainsAny(string value, params string[] tokens)
        => tokens.Any(token => value.Contains(token, StringComparison.CurrentCultureIgnoreCase));

    private bool TryGetGarlandSource(Item item, out EquipmentSourceInfo sourceInfo)
    {
        if (garlandSourceCache.TryGet(item.RowId, out var garlandSource) && garlandSource.HasSource)
        {
            var expansion = garlandSource.HasPatch ? garlandSource.Expansion : ExpansionCategory.Unknown;
            var expansionText = garlandSource.HasPatch
                ? $"{GetExpansionLabel(expansion)}（Garland {garlandSource.PatchText}）"
                : "待更新详细数据";
            sourceInfo = new EquipmentSourceInfo(
                garlandSource.SourceText,
                garlandSource.Categories.Count > 0 ? garlandSource.Categories : [SourceCategory.Other],
                expansion,
                expansionText,
                false);
            return true;
        }

        sourceInfo = null!;
        return false;
    }

    private sealed class SourceAccumulator
    {
        public HashSet<SourceCategory> Categories { get; } = [];

        public HashSet<ushort> PatchNumbers { get; } = [];

        public HashSet<ExpansionCategory> ExpansionCandidates { get; } = [];

        public HashSet<string> Details { get; } = [];
    }

}

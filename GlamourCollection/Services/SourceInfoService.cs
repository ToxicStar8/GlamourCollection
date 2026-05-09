using Dalamud.Utility;
using ECommons.DalamudServices;
using Lumina.Excel.Sheets;
using Main.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Main.Services;

public sealed class SourceInfoService
{
    private readonly Dictionary<uint, SourceAccumulator> sourceByItemId = [];
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private bool isLoaded;

    public EquipmentSourceInfo GetSourceInfo(Item item)
    {
        this.Load();

        if (!this.sourceByItemId.TryGetValue(item.RowId, out var source) || source.Categories.Count == 0)
        {
            var estimatedExpansion = EstimateExpansion(item.LevelEquip);
            return new EquipmentSourceInfo(
                "未知来源",
                [SourceCategory.Unknown],
                estimatedExpansion,
                $"{GetExpansionLabel(estimatedExpansion)}（按装备等级估算）",
                true);
        }

        var categories = source.Categories
            .OrderBy(GetSourceSort)
            .ToList();
        var sourceText = source.Details.Count > 0
            ? string.Join(" / ", source.Details.OrderBy(detail => detail, StringComparer.CurrentCultureIgnoreCase))
            : string.Join(" / ", categories.Select(GetSourceLabel));

        var exactExpansion = source.ExpansionCandidates
            .Where(expansion => expansion != ExpansionCategory.Unknown)
            .OrderBy(expansion => expansion)
            .Select(expansion => (ExpansionCategory?)expansion)
            .FirstOrDefault();
        var exactPatch = source.PatchNumbers
            .Where(patch => patch > 0)
            .OrderBy(patch => patch)
            .Select(patch => (ushort?)patch)
            .FirstOrDefault();

        if (exactExpansion is { } expansion)
        {
            var expansionText = exactPatch is { } patch
                ? $"{GetExpansionLabel(expansion)}（约 {FormatPatchNumber(patch)}）"
                : GetExpansionLabel(expansion);
            return new EquipmentSourceInfo(sourceText, categories, expansion, expansionText, false);
        }

        var estimated = EstimateExpansion(item.LevelEquip);
        return new EquipmentSourceInfo(
            sourceText,
            categories,
            estimated,
            $"{GetExpansionLabel(estimated)}（按装备等级估算）",
            true);
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
        this.LoadSupplementalSources();

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

    private void LoadSupplementalSources()
    {
        var path = Path.Combine(Plugin.Instance.PluginInterface.ConfigDirectory.FullName, "supplemental-sources.json");
        if (!File.Exists(path))
            return;

        try
        {
            var records = JsonSerializer.Deserialize<List<SupplementalSourceRecord>>(File.ReadAllText(path), JsonOptions);
            if (records is null)
                return;

            foreach (var record in records)
            {
                var category = ParseSourceCategory(record);
                if (category == SourceCategory.Unknown && !IsUnknownCategory(record))
                    continue;

                var expansion = ParseExpansion(record);
                var detail = string.IsNullOrWhiteSpace(record.Detail)
                    ? GetSourceLabel(category)
                    : record.Detail.Trim();

                foreach (var itemId in record.GetItemIds())
                    this.AddSource(itemId, category, record.PatchNumber, expansion, detail);
            }
        }
        catch (Exception ex)
        {
            Svc.Log.Warning(ex, "Failed to load supplemental source JSON.");
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

    private static SourceCategory ParseSourceCategory(SupplementalSourceRecord record)
    {
        if (record.CategoryId is { } categoryId && Enum.IsDefined(typeof(SourceCategory), categoryId))
            return (SourceCategory)categoryId;

        if (!string.IsNullOrWhiteSpace(record.Category)
            && Enum.TryParse<SourceCategory>(record.Category, true, out var parsed))
            return parsed;

        return record.Category?.Trim() switch
        {
            "副本" => SourceCategory.Dungeon,
            "讨伐" or "极神" or "讨伐 / 极神" => SourceCategory.Trial,
            "零式" or "高难" or "零式 / 高难" => SourceCategory.Savage,
            "制作" => SourceCategory.Crafting,
            "商店购买" => SourceCategory.Shop,
            "货币兑换" => SourceCategory.CurrencyExchange,
            "金碟" => SourceCategory.GoldSaucer,
            "PVP" => SourceCategory.Pvp,
            "季节活动" => SourceCategory.SeasonalEvent,
            "成就奖励" => SourceCategory.Achievement,
            "任务奖励" => SourceCategory.Quest,
            "莫古站" or "付费商城" or "莫古站 / 付费商城" => SourceCategory.MogStation,
            "深层迷宫" => SourceCategory.DeepDungeon,
            "特殊探索区域" => SourceCategory.FieldOperation,
            "藏宝图" => SourceCategory.TreasureMap,
            "其他来源" => SourceCategory.Other,
            "未知来源" => SourceCategory.Unknown,
            _ => SourceCategory.Unknown,
        };
    }

    private static bool IsUnknownCategory(SupplementalSourceRecord record)
        => record.CategoryId == (int)SourceCategory.Unknown
           || string.Equals(record.Category, nameof(SourceCategory.Unknown), StringComparison.OrdinalIgnoreCase)
           || string.Equals(record.Category, "未知来源", StringComparison.Ordinal);

    private static ExpansionCategory ParseExpansion(SupplementalSourceRecord record)
    {
        if (record.ExpansionId is { } expansionId && Enum.IsDefined(typeof(ExpansionCategory), expansionId))
            return (ExpansionCategory)expansionId;

        if (!string.IsNullOrWhiteSpace(record.Expansion)
            && Enum.TryParse<ExpansionCategory>(record.Expansion, true, out var parsed))
            return parsed;

        return record.Expansion?.Trim() switch
        {
            "2.x" or "新生" or "2.x 新生" => ExpansionCategory.ARealmReborn,
            "3.x" or "苍穹" or "3.x 苍穹" => ExpansionCategory.Heavensward,
            "4.x" or "红莲" or "4.x 红莲" => ExpansionCategory.Stormblood,
            "5.x" or "暗影" or "5.x 暗影" => ExpansionCategory.Shadowbringers,
            "6.x" or "晓月" or "6.x 晓月" => ExpansionCategory.Endwalker,
            "7.x" or "黄金" or "7.x 黄金" => ExpansionCategory.Dawntrail,
            _ => ExpansionCategory.Unknown,
        };
    }

    private sealed class SourceAccumulator
    {
        public HashSet<SourceCategory> Categories { get; } = [];

        public HashSet<ushort> PatchNumbers { get; } = [];

        public HashSet<ExpansionCategory> ExpansionCandidates { get; } = [];

        public HashSet<string> Details { get; } = [];
    }

    private sealed class SupplementalSourceRecord
    {
        public uint ItemId { get; set; }

        public List<uint>? ItemIds { get; set; }

        public string? Category { get; set; }

        public int? CategoryId { get; set; }

        public string? Detail { get; set; }

        public ushort PatchNumber { get; set; }

        public string? Expansion { get; set; }

        public int? ExpansionId { get; set; }

        public IEnumerable<uint> GetItemIds()
        {
            if (this.ItemId != 0)
                yield return this.ItemId;

            if (this.ItemIds is null)
                yield break;

            foreach (var itemId in this.ItemIds)
            {
                if (itemId != 0)
                    yield return itemId;
            }
        }
    }
}

using ECommons.DalamudServices;
using Main.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Main.Services;

public sealed class GarlandSourceCacheService
{
    private const string BaseUrl = "https://www.garlandtools.cn/db/doc/Item/chs/3";
    private static readonly HttpClient HttpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(12),
    };

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
    };

    private readonly string cachePath;
    private readonly object gate = new();
    private readonly Dictionary<uint, GarlandSourceCacheEntry> entries = [];

    public GarlandSourceCacheService(string configDirectory)
    {
        this.cachePath = Path.Combine(configDirectory, "garland-source-cache.json");
        this.Load();
    }

    public bool TryGet(uint itemId, out GarlandSourceCacheEntry entry)
    {
        lock (this.gate)
            return this.entries.TryGetValue(itemId, out entry!);
    }

    public bool HasCachedSource(uint itemId)
        => this.TryGet(itemId, out var entry) && entry.HasSource;

    public bool HasCachedDetailedData(uint itemId)
        => this.TryGet(itemId, out _);

    public async Task<GarlandSourceFetchResult> FetchAndCacheAsync(uint itemId, CancellationToken cancellationToken = default)
    {
        if (itemId == 0)
            return GarlandSourceFetchResult.Failed(itemId, "物品 ID 无效。");

        var url = $"{BaseUrl}/{itemId}.json";
        try
        {
            using var response = await HttpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
                return GarlandSourceFetchResult.Failed(itemId, $"请求 Garland CN 失败：HTTP {(int)response.StatusCode}");

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);

            var parsed = GarlandSourceParser.Parse(itemId, document.RootElement);
            var entry = new GarlandSourceCacheEntry
            {
                ItemId = itemId,
                SourceText = parsed.SourceText,
                Categories = parsed.Categories,
                PatchText = parsed.PatchText,
                Expansion = parsed.Expansion,
                UpdatedAt = DateTimeOffset.Now,
                Url = url,
            };

            lock (this.gate)
            {
                this.entries[itemId] = entry;
                this.SaveLocked();
            }

            var message = entry.HasPatch
                ? $"{entry.SourceText} / 版本 {entry.PatchText}"
                : $"{entry.SourceText} / 版本未解析";
            return entry.HasSource
                ? GarlandSourceFetchResult.Ok(itemId, message)
                : GarlandSourceFetchResult.Failed(itemId, "已请求 Garland CN，但没有解析到明确来源。");
        }
        catch (OperationCanceledException)
        {
            return GarlandSourceFetchResult.Failed(itemId, "来源请求已取消。");
        }
        catch (Exception ex)
        {
            Svc.Log.Warning(ex, "Failed to fetch Garland source for item {ItemId}.", itemId);
            return GarlandSourceFetchResult.Failed(itemId, $"请求 Garland CN 异常：{ex.Message}");
        }
    }

    private void Load()
    {
        if (!File.Exists(this.cachePath))
            return;

        try
        {
            var loaded = JsonSerializer.Deserialize<List<GarlandSourceCacheEntry>>(File.ReadAllText(this.cachePath), JsonOptions);
            if (loaded is null)
                return;

            foreach (var entry in loaded)
            {
                if (entry.ItemId != 0)
                    this.entries[entry.ItemId] = entry;
            }
        }
        catch (Exception ex)
        {
            Svc.Log.Warning(ex, "Failed to load Garland source cache.");
        }
    }

    private void SaveLocked()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(this.cachePath)!);
            var ordered = this.entries.Values.OrderBy(entry => entry.ItemId).ToList();
            File.WriteAllText(this.cachePath, JsonSerializer.Serialize(ordered, JsonOptions));
        }
        catch (Exception ex)
        {
            Svc.Log.Warning(ex, "Failed to save Garland source cache.");
        }
    }

    private static class GarlandSourceParser
    {
        private static readonly string[] SourceRelationNames =
        [
            "sources",
            "source",
            "obtained",
            "tradeSources",
            "tradeShops",
            "shops",
            "vendors",
            "drops",
            "dropSources",
            "instances",
            "duties",
            "quests",
            "achievements",
            "recipe",
            "recipes",
            "craft",
            "crafts",
        ];

        public static ParsedGarlandSource Parse(uint itemId, JsonElement root)
        {
            var details = new SortedSet<string>(StringComparer.CurrentCultureIgnoreCase);
            var categories = new HashSet<SourceCategory>();
            var item = GetMainItem(root);
            var patchText = GetPatchText(item);
            var expansion = GetExpansionFromPatch(patchText);

            ParseSources(root, item, details, categories);

            if (details.Count == 0)
                return new ParsedGarlandSource("未知来源", [SourceCategory.Unknown], patchText, expansion);

            var sourceText = string.Join(" / ", details.Take(32));
            if (details.Count > 32)
                sourceText += $" / 另 {details.Count - 32} 条来源";

            return new ParsedGarlandSource(
                sourceText,
                categories.Count > 0
                    ? categories.OrderBy(category => category == SourceCategory.Unknown ? int.MaxValue : (int)category).ToList()
                    : [SourceCategory.Other],
                patchText,
                expansion);
        }

        private static JsonElement GetMainItem(JsonElement root)
        {
            if (TryGetPropertyIgnoreCase(root, "item", out var item) && item.ValueKind == JsonValueKind.Object)
                return item;

            return root;
        }

        private static string GetPatchText(JsonElement item)
        {
            if (!TryGetPropertyIgnoreCase(item, "patch", out var patch))
                return string.Empty;

            return patch.ValueKind switch
            {
                JsonValueKind.Number => patch.GetRawText(),
                JsonValueKind.String => patch.GetString() ?? string.Empty,
                _ => string.Empty,
            };
        }

        private static ExpansionCategory GetExpansionFromPatch(string patchText)
        {
            if (string.IsNullOrWhiteSpace(patchText))
                return ExpansionCategory.Unknown;

            var majorText = patchText.Split('.', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
            if (!int.TryParse(majorText, out var major))
                return ExpansionCategory.Unknown;

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

        private static void ParseSources(JsonElement root, JsonElement element, ISet<string> details, ISet<SourceCategory> categories)
        {
            if (element.ValueKind == JsonValueKind.Object)
            {
                foreach (var property in element.EnumerateObject())
                {
                    if (IsSourceRelation(property.Name))
                    {
                        ParseSourceValue(root, property.Name, property.Value, details, categories);
                        continue;
                    }

                    if (ShouldSkipRecursiveProperty(property.Name))
                        continue;

                    ParseSources(root, property.Value, details, categories);
                }
            }
            else if (element.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in element.EnumerateArray())
                    ParseSources(root, item, details, categories);
            }
        }

        private static void ParseSourceValue(JsonElement root, string sourceKey, JsonElement source, ISet<string> details, ISet<SourceCategory> categories)
        {
            if (source.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in source.EnumerateArray())
                    ParseSourceValue(root, sourceKey, item, details, categories);
                return;
            }

            if (source.ValueKind == JsonValueKind.Object)
            {
                if (IsCraftRelation(sourceKey))
                {
                    ParseCraftSource(root, source, details, categories);
                    return;
                }

                if (string.Equals(sourceKey, "tradeShops", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(sourceKey, "tradeShop", StringComparison.OrdinalIgnoreCase))
                {
                    ParseTradeShop(root, source, sourceKey, details, categories);
                    return;
                }

                if (LooksLikeSourceObject(source))
                {
                    ParseSourceObject(root, sourceKey, source, details, categories);
                    return;
                }

                foreach (var property in source.EnumerateObject())
                    ParseSourceValue(root, sourceKey, property.Value, details, categories);
                return;
            }

            var name = GetReferenceDisplay(root, source, sourceKey, includeAmount: false);
            if (string.IsNullOrWhiteSpace(name))
                return;

            var category = Categorize(sourceKey, name);
            categories.Add(category);
            details.Add($"{GetCategoryLabel(category)}: {name}");
        }

        private static void ParseSourceObject(JsonElement root, string sourceKey, JsonElement source, ISet<string> details, ISet<SourceCategory> categories)
        {
            if (source.ValueKind != JsonValueKind.Object)
                return;

            var type = GetString(source, "type") ?? GetString(source, "kind") ?? GetString(source, "partial") ?? sourceKey;
            var id = GetUInt(source, "id") ?? GetUInt(source, "target") ?? GetUInt(source, "targetId") ?? 0;
            var name = ResolvePartialName(root, type, id)
                       ?? ResolvePartialName(root, sourceKey, id)
                       ?? GetString(source, "name")
                       ?? GetString(source, "shop")
                       ?? GetString(source, "text");

            var category = Categorize($"{sourceKey} {type}", name);
            categories.Add(category);

            var label = GetCategoryLabel(category);
            if (!string.IsNullOrWhiteSpace(name))
                details.Add($"{label}: {name}");
            else if (!string.IsNullOrWhiteSpace(type))
                details.Add(label);
        }

        private static void ParseCraftSource(JsonElement root, JsonElement craft, ISet<string> details, ISet<SourceCategory> categories)
        {
            categories.Add(SourceCategory.Crafting);

            var parts = new List<string>();
            var jobName = GetCraftJobName(GetUInt(craft, "job") ?? 0);
            if (!string.IsNullOrWhiteSpace(jobName))
                parts.Add(jobName);

            var level = GetUInt(craft, "lvl") ?? GetUInt(craft, "rlvl") ?? 0;
            var stars = GetUInt(craft, "stars") ?? 0;
            if (level > 0)
                parts.Add(stars > 0 ? $"{level}级{new string('★', (int)Math.Min(stars, 5))}" : $"{level}级");

            var unlockId = GetUInt(craft, "unlockId") ?? 0;
            var unlockName = ResolvePartialName(root, "item", unlockId);
            if (!string.IsNullOrWhiteSpace(unlockName))
                parts.Add(unlockName);

            var recipeId = GetUInt(craft, "id") ?? 0;
            if (parts.Count == 0 && recipeId > 0)
                parts.Add($"配方 {recipeId}");

            details.Add(parts.Count > 0
                ? $"制作: {string.Join(" - ", parts)}"
                : "制作");
        }

        private static void ParseTradeShop(JsonElement root, JsonElement source, string sourceKey, ISet<string> details, ISet<SourceCategory> categories)
        {
            var shopName = GetString(source, "shop")
                           ?? GetString(source, "name")
                           ?? GetString(source, "text")
                           ?? "兑换商店";
            var npcNames = GetReferenceNames(root, source, "npcs", "npc").Distinct().Take(2).ToList();
            var costs = GetTradeShopCosts(root, source).Distinct().Take(3).ToList();

            var categoryText = $"{shopName} {string.Join(' ', npcNames)} {string.Join(' ', costs)}";
            var category = Categorize(sourceKey, categoryText);
            categories.Add(category);

            var detailParts = new List<string> { shopName };
            detailParts.AddRange(npcNames);
            detailParts.AddRange(costs);
            details.Add($"{GetCategoryLabel(category)}: {string.Join(" - ", detailParts.Where(part => !string.IsNullOrWhiteSpace(part)))}");
        }

        private static IEnumerable<string> GetReferenceNames(JsonElement root, JsonElement source, string propertyName, string expectedType)
        {
            if (!TryGetPropertyIgnoreCase(source, propertyName, out var references))
                yield break;

            if (references.ValueKind == JsonValueKind.Array)
            {
                foreach (var reference in references.EnumerateArray())
                {
                    var name = GetReferenceDisplay(root, reference, expectedType, includeAmount: false);
                    if (!string.IsNullOrWhiteSpace(name))
                        yield return name;
                }
            }
            else
            {
                var name = GetReferenceDisplay(root, references, expectedType, includeAmount: false);
                if (!string.IsNullOrWhiteSpace(name))
                    yield return name;
            }
        }

        private static IEnumerable<string> GetTradeShopCosts(JsonElement root, JsonElement source)
        {
            if (!TryGetPropertyIgnoreCase(source, "listings", out var listings) || listings.ValueKind != JsonValueKind.Array)
                yield break;

            foreach (var listing in listings.EnumerateArray())
            {
                if (!TryGetPropertyIgnoreCase(listing, "currency", out var currencies) || currencies.ValueKind != JsonValueKind.Array)
                    continue;

                foreach (var currency in currencies.EnumerateArray())
                {
                    var display = GetReferenceDisplay(root, currency, "item", includeAmount: true);
                    if (!string.IsNullOrWhiteSpace(display))
                        yield return display;
                }
            }
        }

        private static bool LooksLikeSourceObject(JsonElement source)
            => TryGetPropertyIgnoreCase(source, "id", out _)
               || TryGetPropertyIgnoreCase(source, "target", out _)
               || TryGetPropertyIgnoreCase(source, "targetId", out _)
               || TryGetPropertyIgnoreCase(source, "type", out _)
               || TryGetPropertyIgnoreCase(source, "kind", out _)
               || TryGetPropertyIgnoreCase(source, "partial", out _)
               || TryGetPropertyIgnoreCase(source, "name", out _)
               || TryGetPropertyIgnoreCase(source, "shop", out _)
               || TryGetPropertyIgnoreCase(source, "text", out _)
               || TryGetPropertyIgnoreCase(source, "n", out _);

        private static bool IsSourceRelation(string propertyName)
            => SourceRelationNames.Any(name => string.Equals(name, propertyName, StringComparison.OrdinalIgnoreCase));

        private static bool IsCraftRelation(string propertyName)
            => string.Equals(propertyName, "craft", StringComparison.OrdinalIgnoreCase)
               || string.Equals(propertyName, "crafts", StringComparison.OrdinalIgnoreCase)
               || string.Equals(propertyName, "recipe", StringComparison.OrdinalIgnoreCase)
               || string.Equals(propertyName, "recipes", StringComparison.OrdinalIgnoreCase);

        private static bool ShouldSkipRecursiveProperty(string propertyName)
            => ContainsAny(propertyName, "ingredients", "partials", "sharedModels", "models", "attr", "complexity", "en", "ja", "fr", "de", "tc", "ko");

        private static string GetCraftJobName(uint jobId)
            => jobId switch
            {
                8 => "刻木匠",
                9 => "锻铁匠",
                10 => "铸甲匠",
                11 => "雕金匠",
                12 => "制革匠",
                13 => "裁衣匠",
                14 => "炼金术士",
                15 => "烹调师",
                _ => string.Empty,
            };

        private static string? ResolvePartialName(JsonElement root, string type, uint id)
        {
            if (id == 0 || string.IsNullOrWhiteSpace(type))
                return null;

            if (!TryGetPropertyIgnoreCase(root, "partials", out var partials) || partials.ValueKind != JsonValueKind.Object)
            {
                if (partials.ValueKind == JsonValueKind.Array)
                    return ResolvePartialNameFromArray(partials, type, id);

                return null;
            }

            var candidates = new[]
            {
                type,
                type.ToLowerInvariant(),
                $"{type}s",
                type.Replace("-", string.Empty, StringComparison.OrdinalIgnoreCase),
            };

            foreach (var candidate in candidates)
            {
                if (!TryGetPropertyIgnoreCase(partials, candidate, out var section))
                    continue;

                if (TryFindPartialById(section, id, out var name))
                    return name;
            }

            return null;
        }

        private static string? ResolvePartialNameFromArray(JsonElement partials, string type, uint id)
        {
            string? fallback = null;
            foreach (var entry in partials.EnumerateArray())
            {
                if ((GetUInt(entry, "id") ?? GetUInt(entry, "key") ?? 0) != id)
                    continue;

                if (!TryReadName(entry, out var name))
                    continue;

                var entryType = GetString(entry, "type") ?? string.Empty;
                if (string.Equals(entryType, type, StringComparison.OrdinalIgnoreCase)
                    || string.Equals($"{entryType}s", type, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(entryType, type.TrimEnd('s'), StringComparison.OrdinalIgnoreCase))
                    return name;

                fallback ??= name;
            }

            return fallback;
        }

        private static bool TryFindPartialById(JsonElement element, uint id, out string? name)
        {
            name = null;
            if (element.ValueKind == JsonValueKind.Object)
            {
                if (TryGetPropertyIgnoreCase(element, id.ToString(), out var keyed)
                    && TryReadName(keyed, out name))
                    return true;

                if ((GetUInt(element, "id") ?? GetUInt(element, "key") ?? 0) == id
                    && TryReadName(element, out name))
                    return true;

                foreach (var property in element.EnumerateObject())
                {
                    if (TryFindPartialById(property.Value, id, out name))
                        return true;
                }
            }
            else if (element.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in element.EnumerateArray())
                {
                    if (TryFindPartialById(item, id, out name))
                        return true;
                }
            }

            return false;
        }

        private static SourceCategory Categorize(string? rawType, string? rawName)
        {
            var value = $"{rawType} {rawName}".Trim();

            if (ContainsAny(value, "recipe", "craft", "制作", "配方"))
                return SourceCategory.Crafting;
            if (ContainsAny(value, "achievement", "成就"))
                return SourceCategory.Achievement;
            if (ContainsAny(value, "quest", "任务"))
                return SourceCategory.Quest;
            if (ContainsAny(value, "gold saucer", "mgp", "金碟"))
                return SourceCategory.GoldSaucer;
            if (ContainsAny(value, "pvp", "对人", "狼印", "战利水晶", "trophy crystal", "wolf mark", "水晶冲突"))
                return SourceCategory.Pvp;
            if (ContainsAny(value, "mog", "商城", "付费"))
                return SourceCategory.MogStation;
            if (ContainsAny(value, "festival", "seasonal", "季节", "星芒", "红莲节", "守护天节", "女儿节", "降神节"))
                return SourceCategory.SeasonalEvent;
            if (ContainsAny(value, "treasure", "map", "宝图", "藏宝图"))
                return SourceCategory.TreasureMap;
            if (ContainsAny(value, "eureka", "bozja", "zadnor", "优雷卡", "博兹雅", "扎杜诺尔"))
                return SourceCategory.FieldOperation;
            if (ContainsAny(value, "deep dungeon", "死者宫殿", "天宫", "正统优雷卡", "深层迷宫"))
                return SourceCategory.DeepDungeon;
            if (ContainsAny(value, "savage", "零式", "绝境战", "高难"))
                return SourceCategory.Savage;
            if (ContainsAny(value, "trial", "讨伐", "歼殛", "歼灭", "极 "))
                return SourceCategory.Trial;
            if (ContainsAny(value, "dungeon", "instance", "副本", "迷宫", "城塞", "神殿", "宫殿"))
                return SourceCategory.Dungeon;
            if (ContainsAny(value, "tradeShops", "tradeShop", "trade shop", "兑换商店", "交易商店"))
                return SourceCategory.CurrencyExchange;
            if (ContainsAny(value, "shop", "npc", "vendor", "merchant", "商店", "兑换", "商人", "货币"))
                return ContainsAny(value, "兑换", "currency", "token", "tomestone", "神典石", "战绩", "票据")
                    ? SourceCategory.CurrencyExchange
                    : SourceCategory.Shop;

            return SourceCategory.Other;
        }

        private static bool TryReadName(JsonElement element, out string? name)
        {
            if (TryGetPropertyIgnoreCase(element, "obj", out var obj)
                && obj.ValueKind == JsonValueKind.Object
                && TryReadName(obj, out name))
                return true;

            name = GetString(element, "name")
                   ?? GetString(element, "Name")
                   ?? GetString(element, "title")
                   ?? GetString(element, "text")
                   ?? GetString(element, "n");
            return !string.IsNullOrWhiteSpace(name);
        }

        private static string? GetReferenceDisplay(JsonElement root, JsonElement element, string expectedType, bool includeAmount)
        {
            uint id = 0;
            string? name = null;
            string? amount = null;

            if (element.ValueKind == JsonValueKind.Number && element.TryGetUInt32(out var numberId))
                id = numberId;
            else if (element.ValueKind == JsonValueKind.String)
            {
                var text = element.GetString();
                if (!uint.TryParse(text, out id))
                    return text;
            }
            else if (element.ValueKind == JsonValueKind.Object)
            {
                name = TryReadName(element, out var directName) ? directName : null;
                id = GetUInt(element, "id") ?? GetUInt(element, "target") ?? GetUInt(element, "targetId") ?? 0;
                amount = GetScalarString(element, "amount") ?? GetScalarString(element, "count") ?? GetScalarString(element, "quantity");
                var type = GetString(element, "type") ?? GetString(element, "kind") ?? GetString(element, "partial") ?? expectedType;
                name ??= ResolvePartialName(root, type, id) ?? ResolvePartialName(root, expectedType, id);
            }

            name ??= ResolvePartialName(root, expectedType, id);
            if (string.IsNullOrWhiteSpace(name))
                return id == 0 ? null : id.ToString();

            return includeAmount && !string.IsNullOrWhiteSpace(amount)
                ? $"{name} x{amount}"
                : name;
        }

        private static string? GetString(JsonElement element, string propertyName)
        {
            if (!TryGetPropertyIgnoreCase(element, propertyName, out var property))
                return null;

            return property.ValueKind == JsonValueKind.String
                ? property.GetString()
                : null;
        }

        private static uint? GetUInt(JsonElement element, string propertyName)
        {
            if (!TryGetPropertyIgnoreCase(element, propertyName, out var property))
                return null;

            if (property.ValueKind == JsonValueKind.Number && property.TryGetUInt32(out var value))
                return value;

            if (property.ValueKind == JsonValueKind.String
                && uint.TryParse(property.GetString(), out var parsed))
                return parsed;

            return null;
        }

        private static string? GetScalarString(JsonElement element, string propertyName)
        {
            if (!TryGetPropertyIgnoreCase(element, propertyName, out var property))
                return null;

            return property.ValueKind switch
            {
                JsonValueKind.String => property.GetString(),
                JsonValueKind.Number => property.GetRawText(),
                _ => null,
            };
        }

        private static bool TryGetPropertyIgnoreCase(JsonElement element, string propertyName, out JsonElement property)
        {
            if (element.ValueKind == JsonValueKind.Object)
            {
                foreach (var item in element.EnumerateObject())
                {
                    if (string.Equals(item.Name, propertyName, StringComparison.OrdinalIgnoreCase))
                    {
                        property = item.Value;
                        return true;
                    }
                }
            }

            property = default;
            return false;
        }

        private static bool ContainsAny(string value, params string[] tokens)
            => tokens.Any(token => value.Contains(token, StringComparison.CurrentCultureIgnoreCase));

        private static string GetCategoryLabel(SourceCategory category)
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
                SourceCategory.Dungeon => "副本",
                SourceCategory.Trial => "讨伐 / 极神",
                SourceCategory.Savage => "零式 / 高难",
                SourceCategory.Other => "其他来源",
                _ => "未知来源",
            };
    }
}

public sealed class GarlandSourceCacheEntry
{
    public uint ItemId { get; set; }

    public string SourceText { get; set; } = "未知来源";

    public List<SourceCategory> Categories { get; set; } = [SourceCategory.Unknown];

    public string PatchText { get; set; } = string.Empty;

    public ExpansionCategory Expansion { get; set; } = ExpansionCategory.Unknown;

    public DateTimeOffset UpdatedAt { get; set; }

    public string Url { get; set; } = string.Empty;

    public bool HasSource
        => !string.IsNullOrWhiteSpace(this.SourceText)
           && !string.Equals(this.SourceText, "未知来源", StringComparison.Ordinal)
           && this.Categories.Any(category => category != SourceCategory.Unknown);

    public bool HasPatch
        => !string.IsNullOrWhiteSpace(this.PatchText)
           && this.Expansion != ExpansionCategory.Unknown;
}

public sealed record GarlandSourceFetchResult(uint ItemId, bool Success, string Message)
{
    public static GarlandSourceFetchResult Ok(uint itemId, string message)
        => new(itemId, true, message);

    public static GarlandSourceFetchResult Failed(uint itemId, string message)
        => new(itemId, false, message);
}

internal sealed record ParsedGarlandSource(
    string SourceText,
    List<SourceCategory> Categories,
    string PatchText,
    ExpansionCategory Expansion);

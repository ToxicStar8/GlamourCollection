using Main.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Main.Services;

public sealed class EquipmentFilterService
{
    public IReadOnlyList<EquipmentViewModel> Apply(
        string searchText,
        IEnumerable<EquipmentViewModel> source,
        FilterState filters)
    {
        filters.EnsureLists();

        var query = source;
        query = ApplySearch(query, searchText);
        query = ApplyFilters(query, filters);
        query = ApplySort(query, filters);
        return query.ToList();
    }

    public string BuildFilterKey(string searchText, FilterState filters)
    {
        filters.EnsureLists();

        return string.Join(
            '|',
            searchText,
            filters.OwnershipFilter,
            filters.QualityFilter,
            filters.SameModelFilter,
            filters.DyeFilter,
            filters.DetailDataFilter,
            string.Join(',', filters.SelectedJobs.OrderBy(value => value)),
            string.Join(',', filters.SelectedSlots.OrderBy(value => value)),
            string.Join(',', filters.SelectedExpansions.OrderBy(value => value)),
            string.Join(',', filters.SelectedSourceCategories.OrderBy(value => value)),
            filters.EquipLevelMin,
            filters.EquipLevelMax,
            filters.ItemLevelMin,
            filters.ItemLevelMax,
            filters.SortMode,
            filters.SortDescending);
    }

    public int GetActiveFilterCount(string searchText, FilterState filters)
    {
        filters.EnsureLists();

        var count = string.IsNullOrWhiteSpace(searchText) ? 0 : 1;
        if ((EquipmentOwnershipFilter)filters.OwnershipFilter != EquipmentOwnershipFilter.All)
            count++;
        if ((EquipmentQualityFilter)filters.QualityFilter != EquipmentQualityFilter.All)
            count++;
        if ((EquipmentSameModelFilter)filters.SameModelFilter != EquipmentSameModelFilter.All)
            count++;
        if ((EquipmentDyeFilter)filters.DyeFilter != EquipmentDyeFilter.All)
            count++;
        if ((EquipmentDetailDataFilter)filters.DetailDataFilter != EquipmentDetailDataFilter.All)
            count++;

        count += filters.SelectedJobs.Count;
        count += filters.SelectedSlots.Count;
        count += filters.SelectedExpansions.Count;
        count += filters.SelectedSourceCategories.Count;

        if (filters.EquipLevelMin > 0 || filters.EquipLevelMax > 0)
            count++;
        if (filters.ItemLevelMin > 0 || filters.ItemLevelMax > 0)
            count++;

        return count;
    }

    private static IEnumerable<EquipmentViewModel> ApplySearch(IEnumerable<EquipmentViewModel> query, string searchText)
    {
        if (string.IsNullOrWhiteSpace(searchText))
            return query;

        return query.Where(item => item.FilterItems.Any(
            appearanceItem => appearanceItem.Name.Contains(searchText, StringComparison.CurrentCultureIgnoreCase)));
    }

    private static IEnumerable<EquipmentViewModel> ApplyFilters(IEnumerable<EquipmentViewModel> query, FilterState filters)
    {
        query = (EquipmentOwnershipFilter)filters.OwnershipFilter switch
        {
            EquipmentOwnershipFilter.Owned => query.Where(item => item.IsOwned),
            EquipmentOwnershipFilter.Missing => query.Where(item => !item.IsOwned),
            _ => query,
        };

        query = (EquipmentQualityFilter)filters.QualityFilter switch
        {
            EquipmentQualityFilter.HasNormalQuality => query.Where(item => item.HasNormalQuality),
            EquipmentQualityFilter.HasHighQuality => query.Where(item => item.HasHighQuality),
            EquipmentQualityFilter.HasBoth => query.Where(item => item.HasNormalQuality && item.HasHighQuality),
            _ => query,
        };

        query = (EquipmentSameModelFilter)filters.SameModelFilter switch
        {
            EquipmentSameModelFilter.SameModelOnly => query.Where(item => item.AppearanceItemCount > 1),
            EquipmentSameModelFilter.SingleItemOnly => query.Where(item => item.AppearanceItemCount == 1),
            _ => query,
        };

        query = (EquipmentDyeFilter)filters.DyeFilter switch
        {
            EquipmentDyeFilter.DyeableOnly => query.Where(item => item.FilterItems.Any(appearanceItem => appearanceItem.CanBeDyed)),
            EquipmentDyeFilter.NotDyeableOnly => query.Where(item => item.FilterItems.All(appearanceItem => !appearanceItem.CanBeDyed)),
            _ => query,
        };

        query = (EquipmentDetailDataFilter)filters.DetailDataFilter switch
        {
            EquipmentDetailDataFilter.HasDetailedData => query.Where(item => item.FilterItems.All(HasDetailedData)),
            EquipmentDetailDataFilter.MissingDetailedData => query.Where(item => item.FilterItems.Any(appearanceItem => !HasDetailedData(appearanceItem))),
            _ => query,
        };

        if (filters.SelectedJobs.Count > 0)
            query = query.Where(item => item.FilterItems.Any(appearanceItem => filters.SelectedJobs.Any(job => MatchesJob(appearanceItem, (JobFilter)job))));

        if (filters.SelectedSlots.Count > 0)
            query = query.Where(item => item.FilterItems.Any(appearanceItem => filters.SelectedSlots.Any(slot => MatchesSlot(appearanceItem, (EquipSlotFilter)slot))));

        if (filters.SelectedExpansions.Count > 0)
            query = query.Where(item => item.FilterItems.Any(appearanceItem => filters.SelectedExpansions.Contains((int)GetExpansion(appearanceItem))));

        if (filters.SelectedSourceCategories.Count > 0)
            query = query.Where(item => item.FilterItems.Any(appearanceItem => MatchesSourceCategory(appearanceItem, filters.SelectedSourceCategories)));

        if (filters.EquipLevelMin > 0)
            query = query.Where(item => item.FilterItems.Any(appearanceItem => appearanceItem.EquipLevel >= filters.EquipLevelMin));
        if (filters.EquipLevelMax > 0)
            query = query.Where(item => item.FilterItems.Any(appearanceItem => appearanceItem.EquipLevel <= filters.EquipLevelMax));
        if (filters.ItemLevelMin > 0)
            query = query.Where(item => item.FilterItems.Any(appearanceItem => appearanceItem.ItemLevel >= filters.ItemLevelMin));
        if (filters.ItemLevelMax > 0)
            query = query.Where(item => item.FilterItems.Any(appearanceItem => appearanceItem.ItemLevel <= filters.ItemLevelMax));

        return query;
    }

    private static IEnumerable<EquipmentViewModel> ApplySort(IEnumerable<EquipmentViewModel> query, FilterState filters)
        => ((EquipmentSortMode)filters.SortMode, filters.SortDescending) switch
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

    private static ExpansionCategory GetExpansion(EquipmentRecord item)
        => item.Expansion;

    private static bool MatchesSourceCategory(EquipmentRecord item, IReadOnlyCollection<int> selectedSourceCategories)
        => item.SourceCategories.Any(category => selectedSourceCategories.Contains((int)category));

    private static bool HasDetailedData(EquipmentRecord item)
        => item.HasDetailedData;

    private static bool MatchesJob(EquipmentRecord item, JobFilter job)
    {
        var category = item.ClassJobCategoryName;
        if (IsAllClasses(category))
            return true;
        if (IsDiscipleOfWarOrMagic(category) && IsCombatJob(job))
            return true;
        if (IsDiscipleOfHand(category) && IsCrafterJob(job))
            return true;
        if (IsDiscipleOfLand(category) && IsGathererJob(job))
            return true;

        return GetJobTokens(job).Any(token => category.Contains(token, StringComparison.CurrentCultureIgnoreCase));
    }

    private static bool MatchesSlot(EquipmentRecord item, EquipSlotFilter slot)
    {
        var category = item.CategoryName;
        return slot switch
        {
            EquipSlotFilter.Weapon => ContainsAny(category, "Arm", "Weapon", "主手", "武器", "魔导书", "幻具", "咒具", "刀", "剑", "枪", "弓", "斧", "爪", "手杖", "刺剑", "圆月轮", "绘笔"),
            EquipSlotFilter.Shield => ContainsAny(category, "Shield", "盾"),
            EquipSlotFilter.Head => ContainsAny(category, "Head", "头"),
            EquipSlotFilter.Body => ContainsAny(category, "Body", "身"),
            EquipSlotFilter.Hands => ContainsAny(category, "Hands", "手"),
            EquipSlotFilter.Waist => ContainsAny(category, "Waist", "腰"),
            EquipSlotFilter.Legs => ContainsAny(category, "Legs", "腿"),
            EquipSlotFilter.Feet => ContainsAny(category, "Feet", "脚"),
            EquipSlotFilter.Earrings => ContainsAny(category, "Earrings", "耳"),
            EquipSlotFilter.Necklace => ContainsAny(category, "Necklace", "项"),
            EquipSlotFilter.Bracelets => ContainsAny(category, "Bracelets", "手镯"),
            EquipSlotFilter.Ring => ContainsAny(category, "Ring", "戒指") && !ContainsAny(category, "Earring", "耳"),
            _ => false,
        };
    }

    private static bool ContainsAny(string value, params string[] tokens)
        => tokens.Any(token => value.Contains(token, StringComparison.CurrentCultureIgnoreCase));

    private static bool IsAllClasses(string value)
        => ContainsAny(value, "All Classes", "全职业", "所有职业");

    private static bool IsDiscipleOfWarOrMagic(string value)
        => ContainsAny(value, "Disciple of War", "Disciple of Magic", "战斗精英", "魔法导师", "防护职业", "进攻职业", "治疗职业");

    private static bool IsDiscipleOfHand(string value)
        => ContainsAny(value, "Disciple of the Hand", "能工巧匠", "生产职业");

    private static bool IsDiscipleOfLand(string value)
        => ContainsAny(value, "Disciple of the Land", "大地使者", "采集职业");

    private static bool IsCombatJob(JobFilter job)
        => (int)job <= (int)JobFilter.Pictomancer;

    private static bool IsCrafterJob(JobFilter job)
        => job is >= JobFilter.Carpenter and <= JobFilter.Culinarian;

    private static bool IsGathererJob(JobFilter job)
        => job is >= JobFilter.Miner and <= JobFilter.Fisher;

    private static string[] GetJobTokens(JobFilter job)
        => job switch
        {
            JobFilter.Paladin => ["PLD", "GLA", "骑士", "剑术"],
            JobFilter.Warrior => ["WAR", "MRD", "战士", "斧术"],
            JobFilter.DarkKnight => ["DRK", "黑骑", "暗黑"],
            JobFilter.Gunbreaker => ["GNB", "绝枪"],
            JobFilter.WhiteMage => ["WHM", "CNJ", "白魔", "幻术"],
            JobFilter.Scholar => ["SCH", "学者"],
            JobFilter.Astrologian => ["AST", "占星"],
            JobFilter.Sage => ["SGE", "贤者"],
            JobFilter.Monk => ["MNK", "PGL", "武僧", "格斗"],
            JobFilter.Dragoon => ["DRG", "LNC", "龙骑", "枪术"],
            JobFilter.Ninja => ["NIN", "ROG", "忍者", "双剑"],
            JobFilter.Samurai => ["SAM", "武士"],
            JobFilter.Reaper => ["RPR", "镰刀"],
            JobFilter.Viper => ["VPR", "蝰蛇"],
            JobFilter.Bard => ["BRD", "ARC", "诗人", "弓术"],
            JobFilter.Machinist => ["MCH", "机工"],
            JobFilter.Dancer => ["DNC", "舞者"],
            JobFilter.BlackMage => ["BLM", "THM", "黑魔", "咒术"],
            JobFilter.Summoner => ["SMN", "ACN", "召唤", "秘术"],
            JobFilter.RedMage => ["RDM", "赤魔"],
            JobFilter.BlueMage => ["BLU", "青魔"],
            JobFilter.Pictomancer => ["PCT", "绘灵"],
            JobFilter.Carpenter => ["CRP", "刻木"],
            JobFilter.Blacksmith => ["BSM", "锻铁"],
            JobFilter.Armorer => ["ARM", "铸甲"],
            JobFilter.Goldsmith => ["GSM", "雕金"],
            JobFilter.Leatherworker => ["LTW", "制革"],
            JobFilter.Weaver => ["WVR", "裁衣"],
            JobFilter.Alchemist => ["ALC", "炼金"],
            JobFilter.Culinarian => ["CUL", "烹调"],
            JobFilter.Miner => ["MIN", "采矿"],
            JobFilter.Botanist => ["BTN", "园艺"],
            JobFilter.Fisher => ["FSH", "捕鱼"],
            _ => [],
        };
}

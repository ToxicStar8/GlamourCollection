using Main.Models;
using System;

namespace Main.Services;

public static class OwnedLocationFormatter
{
    public static string Format(OwnedItemRecord location)
        => $"{GetSourceText(location)} / 格子 {location.Slot}";

    public static string GetSourceText(OwnedItemRecord location)
    {
        if (!string.IsNullOrWhiteSpace(location.RetainerName) || location.RetainerId != 0)
            return $"雇员: {NormalizeRetainerName(location)}";

        var source = location.SourceContainer;
        if (source.StartsWith("Retainer:", StringComparison.Ordinal))
            return $"雇员: {source["Retainer:".Length..].Trim()}";

        return source switch
        {
            "Inventory 1" => "背包 1",
            "Inventory 2" => "背包 2",
            "Inventory 3" => "背包 3",
            "Inventory 4" => "背包 4",
            "Equipped" => "已装备",
            "Armoury Main Hand" => "军械库 主手",
            "Armoury Off Hand" => "军械库 副手",
            "Armoury Head" => "军械库 头部",
            "Armoury Body" => "军械库 身体",
            "Armoury Hands" => "军械库 手部",
            "Armoury Legs" => "军械库 腿部",
            "Armoury Feet" => "军械库 脚部",
            "Armoury Earrings" => "军械库 耳饰",
            "Armoury Necklace" => "军械库 项链",
            "Armoury Bracelet" => "军械库 手镯",
            "Armoury Rings" => "军械库 戒指",
            "Saddlebag" => "陆行鸟背包",
            "Premium Saddlebag" => "高级陆行鸟背包",
            "Glamour Dresser" => "幻化柜",
            "Armoire" => "收藏柜",
            _ => source,
        };
    }

    private static string NormalizeRetainerName(OwnedItemRecord location)
    {
        if (!string.IsNullOrWhiteSpace(location.RetainerName))
            return location.RetainerName.Trim();

        if (location.SourceContainer.StartsWith("Retainer:", StringComparison.Ordinal))
        {
            var name = location.SourceContainer["Retainer:".Length..].Trim();
            if (!string.IsNullOrWhiteSpace(name))
                return name;
        }

        if (location.SourceContainer.StartsWith("雇员:", StringComparison.Ordinal))
        {
            var name = location.SourceContainer["雇员:".Length..].Trim();
            if (!string.IsNullOrWhiteSpace(name))
                return name;
        }

        return "未知雇员";
    }
}

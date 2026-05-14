using System.Collections.Generic;
using System.Linq;

namespace Main.Models;

public sealed record EquipmentRecord(
    uint ItemId,
    string Name,
    uint IconId,
    string CategoryName,
    string ClassJobCategoryName,
    uint EquipLevel,
    uint ItemLevel,
    bool CanBeDyed,
    string SourceInfo,
    IReadOnlyList<SourceCategory> SourceCategories,
    bool HasDetailedData,
    ExpansionCategory Expansion,
    string ExpansionInfo,
    bool IsExpansionEstimated,
    uint ItemUICategoryId,
    uint EquipSlotCategoryId,
    ulong ModelMain,
    ulong ModelSub)
{
    public string AppearanceKey => this.GetAppearanceKey(EquipmentAppearanceMatchMode.Strict);

    public string GetAppearanceKey(EquipmentAppearanceMatchMode mode)
        => mode == EquipmentAppearanceMatchMode.Loose
            ? this.LooseAppearanceKey
            : this.StrictAppearanceKey;

    private string StrictAppearanceKey => this.ModelMain == 0 && this.ModelSub == 0
        ? $"item:{this.ItemId}"
        : $"model:{this.ItemUICategoryId}:{this.EquipSlotCategoryId}:{this.ModelMain}:{this.ModelSub}";

    private string LooseAppearanceKey => this.ModelMain == 0 && this.ModelSub == 0
        ? $"item:{this.ItemId}"
        : this.CanUseLooseAppearanceKey
            ? $"model:{this.ItemUICategoryId}:{this.EquipSlotCategoryId}:{GetModelBaseId(this.ModelMain)}:{GetModelBaseId(this.ModelSub)}"
            : this.StrictAppearanceKey;

    private bool CanUseLooseAppearanceKey
        => this.ItemUICategoryId is >= 34 and <= 43
           && !ContainsAny(this.CategoryName, "Arm", "Weapon", "主手", "副手", "武器", "盾", "魔导书", "幻具", "咒具", "刀", "剑", "枪", "弓", "斧", "爪", "手杖", "刺剑", "圆月轮", "绘笔", "工具");

    public static ulong GetModelBaseId(ulong model)
    {
        if (model == 0)
            return 0;

        var low = model & 0xFFFF;
        if (low != 0)
            return low;

        for (var shift = 48; shift >= 16; shift -= 16)
        {
            var part = (model >> shift) & 0xFFFF;
            if (part != 0)
                return part;
        }

        return model;
    }

    private static bool ContainsAny(string value, params string[] tokens)
        => tokens.Any(token => value.Contains(token, System.StringComparison.CurrentCultureIgnoreCase));
}

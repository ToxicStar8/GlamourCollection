using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using Lumina.Excel.Sheets;
using Main.Models;
using System;

namespace Main.Services;

public interface ITryOnService
{
    bool CanTryOn(uint itemId);

    TryOnResult TryOn(EquipmentViewModel item);

    TryOnResult TryOn(EquipmentRecord item);

    TryOnResult TryOn(uint itemId);
}

public sealed class TryOnService : ITryOnService
{
    private const uint HighQualityItemIdOffset = 1_000_000;

    public bool CanTryOn(uint itemId)
    {
        var normalizedItemId = NormalizeBaseItemId(itemId);
        if (normalizedItemId == 0)
            return false;

        var sheet = Svc.Data.GetExcelSheet<Item>();
        if (!sheet.TryGetRow(normalizedItemId, out var item))
            return false;

        return CanTryOn(item);
    }

    public TryOnResult TryOn(EquipmentViewModel item)
        => this.TryOn(item.Item);

    public TryOnResult TryOn(EquipmentRecord item)
        => this.TryOn(item.ItemId);

    public TryOnResult TryOn(uint itemId)
    {
        var normalizedItemId = NormalizeBaseItemId(itemId);
        if (normalizedItemId == 0)
            return TryOnResult.Failed("无效的物品 ID。");

        if (!Svc.ClientState.IsLoggedIn)
            return TryOnResult.Failed("请先登录角色再试穿。");

        if (!this.CanTryOn(normalizedItemId))
            return TryOnResult.Failed("该物品不支持试穿。");

        try
        {
            return AgentTryon.TryOn(0, normalizedItemId)
                ? TryOnResult.Ok()
                : TryOnResult.Failed("游戏拒绝了试穿请求。");
        }
        catch (Exception ex)
        {
            return TryOnResult.Failed($"试穿失败：{ex.Message}");
        }
    }

    private static bool CanTryOn(Item item)
        => item.EquipSlotCategory.RowId switch
        {
            0 => false,
            2 when item.FilterGroup != 3 => false,
            6 => false,
            17 => false,
            _ => true,
        };

    private static uint NormalizeBaseItemId(uint itemId)
        => itemId > HighQualityItemIdOffset ? itemId - HighQualityItemIdOffset : itemId;
}

public sealed record TryOnResult(bool Success, string? ErrorMessage = null)
{
    public static TryOnResult Ok()
        => new(true);

    public static TryOnResult Failed(string message)
        => new(false, message);
}

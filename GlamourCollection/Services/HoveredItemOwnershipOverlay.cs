using Dalamud.Bindings.ImGui;
using ECommons.DalamudServices;
using Main.Models;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace Main.Services;

public sealed class HoveredItemOwnershipOverlay(
    Configuration configuration,
    ItemDatabaseService itemDatabase,
    OwnedItemRepository ownedItems)
{
    private const uint HighQualityItemIdOffset = 1_000_000;

    public void Draw()
    {
        if (!configuration.ShowHoveredItemOwnershipOverlay || !Svc.ClientState.IsLoggedIn)
            return;

        var rawItemId = (uint)Svc.GameGui.HoveredItem;
        if (rawItemId == 0)
            return;

        var itemId = NormalizeBaseItemId(rawItemId);
        if (!itemDatabase.TryGetEquipment(itemId, out var hoveredItem))
            return;

        var useSameModel = configuration.HoveredItemOwnershipUseSameModel;
        var locations = FindOwnedLocations(hoveredItem, useSameModel);
        var exactLocations = FindExactOwnedLocations(itemId);
        var isExactOwned = exactLocations.Count > 0;
        var isOwned = locations.Count > 0;

        var mouse = ImGui.GetMousePos();
        ImGui.SetNextWindowPos(mouse + new Vector2(18f, 20f), ImGuiCond.Always);
        ImGui.SetNextWindowBgAlpha(0.92f);

        const ImGuiWindowFlags flags =
            ImGuiWindowFlags.NoDecoration
            | ImGuiWindowFlags.AlwaysAutoResize
            | ImGuiWindowFlags.NoSavedSettings
            | ImGuiWindowFlags.NoFocusOnAppearing
            | ImGuiWindowFlags.NoNav
            | ImGuiWindowFlags.NoInputs;

        if (!ImGui.Begin("##GlamourCollectionHoveredItemOwnership", flags))
        {
            ImGui.End();
            return;
        }

        ImGui.TextUnformatted("幻化收藏");
        ImGui.Separator();

        if (isExactOwned)
        {
            ImGui.TextColored(new Vector4(0.25f, 0.9f, 0.4f, 1f), "已拥有");
            DrawLocations(exactLocations);
        }
        else if (isOwned && useSameModel)
        {
            ImGui.TextColored(new Vector4(0.95f, 0.78f, 0.28f, 1f), "同模已拥有");
            DrawLocations(locations);
        }
        else
        {
            ImGui.TextDisabled(useSameModel ? "未拥有 / 未发现同模" : "未拥有");
        }

        ImGui.End();
    }

    private IReadOnlyList<OwnedItemRecord> FindOwnedLocations(EquipmentRecord hoveredItem, bool useSameModel)
    {
        if (!useSameModel)
            return FindExactOwnedLocations(hoveredItem.ItemId);

        var locations = new List<OwnedItemRecord>();
        foreach (var location in ownedItems.Records)
        {
            var ownedItemId = GetOwnedBaseItemId(location);
            if (!itemDatabase.TryGetEquipment(ownedItemId, out var ownedItem))
                continue;

            if (ownedItem.AppearanceKey == hoveredItem.AppearanceKey)
                locations.Add(location);
        }

        return locations;
    }

    private IReadOnlyList<OwnedItemRecord> FindExactOwnedLocations(uint itemId)
        => ownedItems.Records
            .Where(location => GetOwnedBaseItemId(location) == itemId)
            .ToList();

    private static void DrawLocations(IReadOnlyList<OwnedItemRecord> locations)
    {
        var maxLines = locations.Count;
        for (var index = 0; index < maxLines; index++)
            ImGui.TextUnformatted(OwnedLocationFormatter.Format(locations[index]));
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
        => itemId > HighQualityItemIdOffset ? itemId - HighQualityItemIdOffset : itemId;
}

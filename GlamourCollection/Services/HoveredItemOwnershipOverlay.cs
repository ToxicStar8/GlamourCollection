using Dalamud.Bindings.ImGui;
using Dalamud.Game.NativeWrapper;
using ECommons.DalamudServices;
using Main.Models;
using System;
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
    private static readonly string[] ItemDetailAddonNames = ["ItemDetail", "ItemDetailCompare"];

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

        if (!TryFindVisibleItemDetailAddon(out var addon))
            return;

        var useSameModel = configuration.HoveredItemOwnershipUseSameModel;
        var appearanceMatchMode = (EquipmentAppearanceMatchMode)configuration.EquipmentAppearanceMatchMode;
        var locations = FindOwnedLocations(hoveredItem, useSameModel, appearanceMatchMode);
        var exactLocations = FindExactOwnedLocations(itemId);
        var isExactOwned = exactLocations.Count > 0;
        var isOwned = locations.Count > 0;

        var statusText = BuildStatusText(isExactOwned, isOwned, useSameModel, exactLocations.Count, locations.Count);
        var locationText = isExactOwned
            ? BuildLocationSummary(exactLocations)
            : isOwned && useSameModel
                ? BuildLocationSummary(locations)
                : string.Empty;
        var hasLocationText = !string.IsNullOrWhiteSpace(locationText);

        var width = MathF.Max(260f, addon.ScaledWidth);
        var padding = new Vector2(8f, 5f);
        var lineHeight = ImGui.CalcTextSize("A").Y;
        var statusLines = 1f;
        var locationLines = hasLocationText ? (float)locationText.Split('\n').Length : 0f;
        var stripHeight = (statusLines + locationLines) * lineHeight + padding.Y * 2
            + (hasLocationText ? 2f : 0f);
        var position = new Vector2(addon.X, addon.Y - stripHeight - 3f);
        var viewport = ImGui.GetMainViewport();
        if (position.Y < viewport.WorkPos.Y)
            position.Y = addon.Y + addon.ScaledHeight + 3f;

        ImGui.SetNextWindowPos(position, ImGuiCond.Always);
        ImGui.SetNextWindowSize(new Vector2(width, stripHeight), ImGuiCond.Always);
        ImGui.SetNextWindowBgAlpha(0.9f);

        var backgroundColor = isExactOwned
            ? new Vector4(0.08f, 0.22f, 0.12f, 0.94f)
            : isOwned && useSameModel
                ? new Vector4(0.24f, 0.18f, 0.06f, 0.94f)
                : new Vector4(0.14f, 0.14f, 0.16f, 0.94f);
        var textColor = isExactOwned
            ? new Vector4(0.45f, 1f, 0.55f, 1f)
            : isOwned && useSameModel
                ? new Vector4(1f, 0.82f, 0.36f, 1f)
                : new Vector4(0.78f, 0.78f, 0.82f, 1f);

        ImGui.PushStyleColor(ImGuiCol.WindowBg, backgroundColor);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, padding);

        const ImGuiWindowFlags flags =
            ImGuiWindowFlags.NoDecoration
            | ImGuiWindowFlags.NoMove
            | ImGuiWindowFlags.NoResize
            | ImGuiWindowFlags.NoScrollbar
            | ImGuiWindowFlags.NoSavedSettings
            | ImGuiWindowFlags.NoFocusOnAppearing
            | ImGuiWindowFlags.NoNav
            | ImGuiWindowFlags.NoInputs;

        if (!ImGui.Begin("##GlamourCollectionItemDetailOwnership", flags))
        {
            ImGui.End();
            ImGui.PopStyleVar();
            ImGui.PopStyleColor();
            return;
        }

        ImGui.TextColored(textColor, statusText);
        if (hasLocationText)
            ImGui.TextUnformatted(locationText);

        ImGui.End();
        ImGui.PopStyleVar();
        ImGui.PopStyleColor();
    }

    private static bool TryFindVisibleItemDetailAddon(out AtkUnitBasePtr addon)
    {
        foreach (var addonName in ItemDetailAddonNames)
        {
            for (var index = 1; index <= 3; index++)
            {
                addon = Svc.GameGui.GetAddonByName(addonName, index);
                if (addon.IsNull)
                    continue;

                if (addon.IsVisible)
                    return true;
            }
        }

        addon = default;
        return false;
    }

    private IReadOnlyList<OwnedItemRecord> FindOwnedLocations(
        EquipmentRecord hoveredItem,
        bool useSameModel,
        EquipmentAppearanceMatchMode appearanceMatchMode)
    {
        if (!useSameModel)
            return FindExactOwnedLocations(hoveredItem.ItemId);

        var locations = new List<OwnedItemRecord>();
        var hoveredAppearanceKey = hoveredItem.GetAppearanceKey(appearanceMatchMode);
        foreach (var location in ownedItems.Records)
        {
            var ownedItemId = GetOwnedBaseItemId(location);
            if (!itemDatabase.TryGetEquipment(ownedItemId, out var ownedItem))
                continue;

            if (ownedItem.GetAppearanceKey(appearanceMatchMode) == hoveredAppearanceKey)
                locations.Add(location);
        }

        return locations;
    }

    private IReadOnlyList<OwnedItemRecord> FindExactOwnedLocations(uint itemId)
        => ownedItems.Records
            .Where(location => GetOwnedBaseItemId(location) == itemId)
            .ToList();

    private static string BuildStatusText(
        bool isExactOwned,
        bool isOwned,
        bool useSameModel,
        int exactLocationCount,
        int sameModelLocationCount)
    {
        if (isExactOwned)
            return $"Glamour Collection: 已拥有 ({exactLocationCount} 个位置)";

        if (isOwned && useSameModel)
            return $"Glamour Collection: 同模已拥有 ({sameModelLocationCount} 个位置)";

        return useSameModel
            ? "Glamour Collection: 未拥有 / 未发现同模"
            : "Glamour Collection: 未拥有";
    }

    private static string BuildLocationSummary(IReadOnlyList<OwnedItemRecord> locations)
    {
        const int maxShownLocations = 3;
        var shown = locations
            .Take(maxShownLocations)
            .Select(OwnedLocationFormatter.Format)
            .ToList();

        if (locations.Count > maxShownLocations)
            shown.Add($"另 {locations.Count - maxShownLocations} 个位置");

        return string.Join("\n", shown);
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

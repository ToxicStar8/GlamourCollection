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
    private Dictionary<uint, IReadOnlyList<OwnedItemRecord>> ownedLocationsByItemId = [];
    private Dictionary<string, IReadOnlyList<OwnedItemRecord>> ownedLocationsByAppearanceKey = [];
    private int cachedOwnedItemVersion = -1;
    private bool hasAppearanceIndex;

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
        EnsureOwnedIndexes(useSameModel);

        var locations = FindOwnedLocations(hoveredItem, useSameModel, appearanceMatchMode);
        var exactLocations = FindExactOwnedLocations(itemId);
        var isExactOwned = exactLocations.Count > 0;
        var isOwned = locations.Count > 0;

        var hasAdditionalSameModelLocations = useSameModel && locations.Count > exactLocations.Count;
        var statusText = BuildStatusText(
            isExactOwned,
            isOwned,
            useSameModel,
            exactLocations.Count,
            locations.Count,
            hasAdditionalSameModelLocations);
        var locationText = hasAdditionalSameModelLocations
            ? BuildLocationSummary(locations, includeItemName: true)
            : isExactOwned
                ? BuildLocationSummary(exactLocations, includeItemName: false)
                : isOwned && useSameModel
                    ? BuildLocationSummary(locations, includeItemName: true)
                    : string.Empty;
        var hasLocationText = !string.IsNullOrWhiteSpace(locationText);

        var width = MathF.Max(260f, addon.ScaledWidth);
        var padding = new Vector2(8f, 5f);
        var lineHeight = ImGui.CalcTextSize("A").Y;
        var statusLines = 1f;
        var locationLines = hasLocationText ? CountLines(locationText) : 0f;
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

        var hoveredAppearanceKey = hoveredItem.GetAppearanceKey(appearanceMatchMode);
        if (appearanceMatchMode == EquipmentAppearanceMatchMode.Strict)
        {
            return this.ownedLocationsByAppearanceKey.TryGetValue(hoveredAppearanceKey, out var strictLocations)
                ? strictLocations
                : [];
        }

        var looseAppearanceKey = hoveredItem.GetAppearanceKey(EquipmentAppearanceMatchMode.Loose);
        var locationKeys = new HashSet<string>(StringComparer.Ordinal);
        var locations = new List<OwnedItemRecord>();
        AddAppearanceLocations(hoveredAppearanceKey, locationKeys, locations);
        if (!string.Equals(looseAppearanceKey, hoveredAppearanceKey, StringComparison.Ordinal))
            AddAppearanceLocations(looseAppearanceKey, locationKeys, locations);

        return locations;
    }

    private IReadOnlyList<OwnedItemRecord> FindExactOwnedLocations(uint itemId)
        => this.ownedLocationsByItemId.TryGetValue(itemId, out var locations)
            ? locations
            : [];

    private void EnsureOwnedIndexes(bool includeAppearanceIndex)
    {
        if (this.cachedOwnedItemVersion != ownedItems.Version)
            RebuildExactOwnedIndex();

        if (includeAppearanceIndex && !this.hasAppearanceIndex)
            RebuildAppearanceOwnedIndex();
    }

    private void RebuildExactOwnedIndex()
    {
        this.ownedLocationsByItemId = ownedItems.Records
            .GroupBy(GetOwnedBaseItemId)
            .Where(group => group.Key != 0)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<OwnedItemRecord>)group.ToList());

        this.ownedLocationsByAppearanceKey = [];
        this.cachedOwnedItemVersion = ownedItems.Version;
        this.hasAppearanceIndex = false;
    }

    private void RebuildAppearanceOwnedIndex()
    {
        var indexed = new Dictionary<string, List<OwnedItemRecord>>();
        foreach (var (itemId, locations) in this.ownedLocationsByItemId)
        {
            if (!itemDatabase.TryGetEquipment(itemId, out var item))
                continue;

            AddIndexedLocations(indexed, item.GetAppearanceKey(EquipmentAppearanceMatchMode.Strict), locations);

            var looseAppearanceKey = item.GetAppearanceKey(EquipmentAppearanceMatchMode.Loose);
            if (!string.Equals(looseAppearanceKey, item.GetAppearanceKey(EquipmentAppearanceMatchMode.Strict), StringComparison.Ordinal))
                AddIndexedLocations(indexed, looseAppearanceKey, locations);
        }

        this.ownedLocationsByAppearanceKey = indexed.ToDictionary(
            pair => pair.Key,
            pair => (IReadOnlyList<OwnedItemRecord>)pair.Value);
        this.hasAppearanceIndex = true;
    }

    private static void AddIndexedLocations(
        IDictionary<string, List<OwnedItemRecord>> indexed,
        string appearanceKey,
        IReadOnlyList<OwnedItemRecord> locations)
    {
        if (!indexed.TryGetValue(appearanceKey, out var appearanceLocations))
        {
            appearanceLocations = [];
            indexed[appearanceKey] = appearanceLocations;
        }

        appearanceLocations.AddRange(locations);
    }

    private void AddAppearanceLocations(
        string appearanceKey,
        ISet<string> locationKeys,
        ICollection<OwnedItemRecord> locations)
    {
        if (!this.ownedLocationsByAppearanceKey.TryGetValue(appearanceKey, out var indexedLocations))
            return;

        foreach (var location in indexedLocations)
        {
            if (locationKeys.Add(GetLocationKey(location)))
                locations.Add(location);
        }
    }

    private static string GetLocationKey(OwnedItemRecord location)
        => $"{GetOwnedBaseItemId(location)}|{location.SourceContainer}|{location.ContainerType}|{location.ContainerId}|{location.Slot}|{location.RetainerId}";

    private static string BuildStatusText(
        bool isExactOwned,
        bool isOwned,
        bool useSameModel,
        int exactLocationCount,
        int sameModelLocationCount,
        bool hasAdditionalSameModelLocations)
    {
        if (isExactOwned && hasAdditionalSameModelLocations)
            return $"Glamour Collection: 已拥有 / 同模已拥有 ({sameModelLocationCount} 个位置)";

        if (isExactOwned)
            return $"Glamour Collection: 已拥有 ({exactLocationCount} 个位置)";

        if (isOwned && useSameModel)
            return $"Glamour Collection: 同模已拥有 ({sameModelLocationCount} 个位置)";

        return useSameModel
            ? "Glamour Collection: 未拥有 / 未发现同模"
            : "Glamour Collection: 未拥有";
    }

    private string BuildLocationSummary(IReadOnlyList<OwnedItemRecord> locations, bool includeItemName)
    {
        const int maxShownLocations = 3;
        var shown = locations
            .Take(maxShownLocations)
            .Select(location => FormatLocationSummaryLine(location, includeItemName))
            .ToList();

        if (locations.Count > maxShownLocations)
            shown.Add($"另 {locations.Count - maxShownLocations} 个位置");

        return string.Join("\n", shown);
    }

    private string FormatLocationSummaryLine(OwnedItemRecord location, bool includeItemName)
    {
        var locationText = OwnedLocationFormatter.Format(location);
        if (!includeItemName)
            return locationText;

        return $"{GetOwnedItemName(location)} - {locationText}";
    }

    private string GetOwnedItemName(OwnedItemRecord location)
    {
        var itemId = GetOwnedBaseItemId(location);
        if (itemId != 0 && itemDatabase.TryGetEquipment(itemId, out var item))
            return item.Name;

        if (!string.IsNullOrWhiteSpace(location.ItemName))
            return location.ItemName.Trim();

        return itemId == 0 ? "未知装备" : $"物品 {itemId}";
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

    private static float CountLines(string text)
    {
        var lines = 1;
        foreach (var character in text)
        {
            if (character == '\n')
                lines++;
        }

        return lines;
    }
}

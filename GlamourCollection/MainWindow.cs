using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Windowing;
using ECommons.DalamudServices;
using System;
using System.Diagnostics;
using System.Linq;
using System.Numerics;

namespace Main
{
    public unsafe class MainWindow : Window, IDisposable
    {
        private string searchText = string.Empty;

        public MainWindow() : base(Plugin.Instance.Name)
        {
            SizeConstraints = new WindowSizeConstraints
            {
                MinimumSize = new Vector2(520, 320),
                MaximumSize = new Vector2(1000, 900),
            };
        }

        public override void Draw()
        {
            var config = Plugin.Instance.Configuration;

            if (!ImGui.BeginTabBar(Plugin.Instance.Name))
                return;

            if (ImGui.BeginTabItem("Collection"))
            {
                DrawCollection();
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem(Lang.Setting))
            {
                var isLoginedOpenWindow = config.IsLoginedOpenWindow;
                ImGui.Checkbox(Lang.LoginShow, ref isLoginedOpenWindow);
                if (config.IsLoginedOpenWindow != isLoginedOpenWindow)
                {
                    config.IsLoginedOpenWindow = isLoginedOpenWindow;
                    config.Save();
                }

                var isEscCloseWindow = config.IsEscCloseWindow;
                ImGui.Checkbox(Lang.EscClose, ref isEscCloseWindow);
                if (config.IsEscCloseWindow != isEscCloseWindow)
                {
                    config.IsEscCloseWindow = isEscCloseWindow;
                    RespectCloseHotkey = isEscCloseWindow;
                    config.Save();
                }

                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem(Lang.About))
            {
                if (ImGui.Button(Lang.SendIssue))
                {
                    var url = "https://discord.gg/GWMEY9P9BX";
                    Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
                }

                ImGui.EndTabItem();
            }

            ImGui.EndTabBar();
        }

        private void DrawCollection()
        {
            var plugin = Plugin.Instance;
            var items = plugin.Ownership.ViewModels;

            if (ImGui.Button("Rescan"))
                plugin.RescanOwnedItems();

            ImGui.SameLine();
            ImGui.TextUnformatted(plugin.LastInventoryScanStatus);

            if (plugin.LastInventoryScanAt is { } lastScan)
                ImGui.TextDisabled(lastScan.ToString("yyyy-MM-dd HH:mm:ss"));

            ImGui.InputText("Search", ref searchText, 128);
            ImGui.TextUnformatted($"Owned {plugin.Ownership.OwnedItemCount} / {items.Count}");

            var filtered = string.IsNullOrWhiteSpace(searchText)
                ? items
                : items.Where(item => item.Item.Name.Contains(searchText, StringComparison.CurrentCultureIgnoreCase)).ToList();

            var tableFlags = ImGuiTableFlags.Borders
                             | ImGuiTableFlags.RowBg
                             | ImGuiTableFlags.ScrollY
                             | ImGuiTableFlags.SizingStretchProp;

            if (!ImGui.BeginTable("##equipmentList", 3, tableFlags, new Vector2(-1, -1)))
                return;

            ImGui.TableSetupColumn("Icon", ImGuiTableColumnFlags.WidthFixed, 42f);
            ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableSetupColumn("Owned", ImGuiTableColumnFlags.WidthFixed, 56f);
            ImGui.TableHeadersRow();

            foreach (var item in filtered)
            {
                ImGui.TableNextRow();

                ImGui.TableSetColumnIndex(0);
                DrawItemIcon(item.Item.IconId);

                ImGui.TableSetColumnIndex(1);
                ImGui.AlignTextToFramePadding();
                ImGui.TextUnformatted(item.Item.Name);

                ImGui.TableSetColumnIndex(2);
                ImGui.AlignTextToFramePadding();
                if (item.IsOwned)
                    ImGui.TextColored(new Vector4(0.25f, 0.9f, 0.4f, 1f), "✓");
                else
                    ImGui.TextDisabled("-");
            }

            ImGui.EndTable();
        }

        private static void DrawItemIcon(uint iconId)
        {
            var size = new Vector2(32, 32);
            if (iconId != 0
                && Svc.Texture.TryGetFromGameIcon(new GameIconLookup(iconId), out var icon)
                && icon.TryGetWrap(out var texture, out _))
            {
                ImGui.Image(texture.Handle, size);
                return;
            }

            ImGui.Dummy(size);
        }

        public void Dispose()
        {
        }
    }
}

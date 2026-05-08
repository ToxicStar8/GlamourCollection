using Dalamud.Interface.Windowing;
using Dalamud.Bindings.ImGui;
using ECommons.DalamudServices;
using System;
using System.Diagnostics;
using System.Numerics;

namespace Main
{
    public unsafe class MainWindow : Window, IDisposable
    {
        public MainWindow() : base(Plugin.Instance.Name, ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)
        {
            //设置窗口大小
            this.SizeConstraints = new WindowSizeConstraints
            {
                MinimumSize = new Vector2(260, 150),
                MaximumSize = new Vector2(600, 600)
            };
        }

        public override void Draw()
        {
            var config = Plugin.Instance.Configuration;

            ImGui.BeginTabBar(Plugin.Instance.Name);

            //设置
            if (ImGui.BeginTabItem(Lang.Setting))
            {
                //是否登录就显示本窗口
                bool isLoginedOpenWindow = config.IsLoginedOpenWindow;
                ImGui.Checkbox(Lang.LoginShow, ref isLoginedOpenWindow);
                if (config.IsLoginedOpenWindow != isLoginedOpenWindow)
                {
                    config.IsLoginedOpenWindow = isLoginedOpenWindow;
                    config.Save();
                }

                //禁用esc关闭，仅可使用x关闭
                bool isEscCloseWindow = config.IsEscCloseWindow;
                ImGui.Checkbox(Lang.EscClose, ref isEscCloseWindow);
                if (config.IsEscCloseWindow != isEscCloseWindow)
                {
                    config.IsEscCloseWindow = isEscCloseWindow;
                    RespectCloseHotkey = isEscCloseWindow;
                    config.Save();
                }

                ImGui.EndTabItem();
            }

            //关于
            if (ImGui.BeginTabItem(Lang.About))
            {
                //反馈问题
                if (ImGui.Button(Lang.SendIssue))
                {
                    var url = "https://discord.gg/GWMEY9P9BX";
                    Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
                }
                ImGui.EndTabItem();
            }

            ImGui.EndTabBar();
        }

        public void Dispose()
        {

        }
    }
}

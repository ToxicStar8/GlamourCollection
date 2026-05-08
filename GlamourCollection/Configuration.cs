using Dalamud.Configuration;
using Dalamud.Plugin;
using ECommons.DalamudServices;
using System;
using System.Collections.Generic;

namespace Main
{
    [Serializable]
    public class Configuration : IPluginConfiguration
    {
        public int Version { get; set; } = 0;

        //是否登录就显示窗口
        public bool IsLoginedOpenWindow { get; set; } = false;
        //是否使用Esc可以关闭窗口
        public bool IsEscCloseWindow { get; set; } = true;
        //语言类型 zh=0 todo:待实现
        public int LangIndex { get; set; } = 0;

        public void Init()
        {

        }

        public void Save()
        {
            Plugin.Instance.PluginInterface!.SavePluginConfig(this);
        }
    }
}

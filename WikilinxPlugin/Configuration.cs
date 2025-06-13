using Dalamud.Configuration;
using System;

namespace Wikilinx;

[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 1;

    public bool WikiEnabled { get; set; } = true;

    public string WikiUrl { get; set; } = "https://ffxiv.consolegameswiki.com/wiki/";

    public bool ReplaceWhitespace { get; set; } = false;
    public string WhitespaceReplacement { get; set; } = string.Empty;

    public bool LodestoneEnabled { get; set; } = false;

    public void Save()
    {
        Plugin.PluginInterface.SavePluginConfig(this);
    }
}

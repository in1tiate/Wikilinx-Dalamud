using Dalamud.Interface.Windowing;
using ImGuiNET;
using System;
using System.Numerics;

namespace WikilinxPlugin.Windows;

public class ConfigWindow : Window, IDisposable
{
    private Configuration Configuration;

    // We give this window a constant ID using ###
    // This allows for labels being dynamic, like "{FPS Counter}fps###XYZ counter window",
    // and the window ID will always be "###XYZ counter window" for ImGui
    public ConfigWindow(Plugin plugin) : base("Wikilinx Configs")
    {
        Flags = ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoScrollbar |
                ImGuiWindowFlags.NoScrollWithMouse;

        Size = new Vector2(350, 250);
        SizeCondition = ImGuiCond.Always;

        Configuration = plugin.Configuration;
    }

    public void Dispose() { }

    public override void Draw()
    {
        var spacing = Vector2.Create(0.0f, 20.0f);

        var lodestone = Configuration.LodestoneEnabled;
        if (ImGui.Checkbox("Enable Eorzea DB Integration", ref lodestone))
        {
            Configuration.LodestoneEnabled = lodestone;
            Configuration.Save();
        }

        ImGui.Dummy(spacing);

        var wiki = Configuration.WikiEnabled;
        if (ImGui.Checkbox("Enable Wiki Integration", ref wiki))
        {
            Configuration.WikiEnabled = wiki;
            Configuration.Save();
        }


        ImGui.TextUnformatted("Wiki URL:");
        var wikiurl = Configuration.WikiUrl ?? "";
        ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
        if (ImGui.InputText("###wiki-url", ref wikiurl, 1_000))
        {
            Configuration.WikiUrl = wikiurl;
            Configuration.Save();
        }

        var whitespace = Configuration.ReplaceWhitespace;
        if (ImGui.Checkbox("Replace whitespace in item name", ref whitespace))
        {
            Configuration.ReplaceWhitespace = whitespace;
            Configuration.Save();
        }

        ImGui.TextUnformatted("Replace whitespace with:");
        var whitespace_str = Configuration.WhitespaceReplacement ?? "";
        ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
        if (ImGui.InputText("###whitespace-str", ref whitespace_str, 1_000))
        {
            Configuration.WhitespaceReplacement = whitespace_str;
            Configuration.Save();
        }

    }
}

using Dalamud.Interface.Windowing;
using ImGuiNET;
using System;
using System.Numerics;

namespace Wikilinx.Windows;

public class ConfigWindow : Window, IDisposable
{
    private readonly Configuration configuration;

    private readonly Plugin pluginRef;
    public ConfigWindow(Plugin plugin) : base("Wikilinx Configs")
    {
        Flags = ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoScrollbar |
                ImGuiWindowFlags.NoScrollWithMouse;

        Size = new Vector2(350, 250);
        SizeCondition = ImGuiCond.Always;

        configuration = plugin.Configuration;
        pluginRef = plugin;
    }

    public void Dispose() { GC.SuppressFinalize(this); }

    public override void Draw()
    {
        var spacing = Vector2.Create(0.0f, 20.0f);

        var lodestone = configuration.LodestoneEnabled;
        if (ImGui.Checkbox(pluginRef.Translate("Enable Eorzea DB Integration"), ref lodestone))
        {
            configuration.LodestoneEnabled = lodestone;
            configuration.Save();
        }

        ImGui.Dummy(spacing);

        var wiki = configuration.WikiEnabled;
        if (ImGui.Checkbox(pluginRef.Translate("Enable Wiki Integration"), ref wiki))
        {
            configuration.WikiEnabled = wiki;
            configuration.Save();
        }


        ImGui.TextUnformatted(pluginRef.Translate(("Wiki URL:")));
        var wikiurl = configuration.WikiUrl ?? "";
        ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
        if (ImGui.InputText("###wiki-url", ref wikiurl, 1_000))
        {
            configuration.WikiUrl = wikiurl;
            configuration.Save();
        }

        var whitespace = configuration.ReplaceWhitespace;
        if (ImGui.Checkbox(pluginRef.Translate("Replace whitespace in item name"), ref whitespace))
        {
            configuration.ReplaceWhitespace = whitespace;
            configuration.Save();
        }

        ImGui.TextUnformatted(pluginRef.Translate("Replace whitespace with:"));
        var whitespace_str = configuration.WhitespaceReplacement ?? "";
        ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
        if (ImGui.InputText("###whitespace-str", ref whitespace_str, 1_000))
        {
            configuration.WhitespaceReplacement = whitespace_str;
            configuration.Save();
        }

    }
}

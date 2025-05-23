using Dalamud.Game.Gui.ContextMenu;
using Dalamud.Game.Text;
using Dalamud.Interface.ImGuiNotification;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using Lumina.Excel.Sheets;
using System;
using System.IO;
using System.Text;
using WikilinxPlugin.Windows;

namespace WikilinxPlugin;

public sealed class Plugin : IDalamudPlugin
{
    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static IDataManager DataManager { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;

    [PluginService] internal static IGameGui GameGui { get; private set; } = null!;
    [PluginService] internal static IContextMenu ContextMenu { get; private set; } = null!;

    [PluginService] internal static INotificationManager NotificationManager { get; private set; } = null!;

    public Configuration Configuration { get; init; }

    public readonly WindowSystem WindowSystem = new("Wikilinx");

    private ConfigWindow ConfigWindow { get; init; }

    public Plugin()
    {
        Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();

        ConfigWindow = new ConfigWindow(this);

        ContextMenu.OnMenuOpened += this.OnContextMenuOpened;

        WindowSystem.AddWindow(ConfigWindow);

        PluginInterface.UiBuilder.Draw += DrawUI;

        PluginInterface.UiBuilder.OpenConfigUi += ToggleConfigUI;
    }

    public void Dispose()
    {
        WindowSystem.RemoveAllWindows();

        ConfigWindow.Dispose();
    }

    private void OnContextMenuOpened(IMenuOpenedArgs args)
    {
        // Item ID retrieval code borrowed from MarketBoard

        uint itemId;

        if (args.MenuType == ContextMenuType.Inventory)
        {
            itemId = (args.Target as MenuTargetInventory)?.TargetItem?.BaseItemId ?? 0u;
        }
        else
        {
            itemId = this.GetItemIdFromAgent(args.AddonName);

            if (itemId == 0u)
            {
                Log.Warning("Failed to get item ID from agent {0}. Attempting hovered item.", args.AddonName ?? "null");
                itemId = (uint)GameGui.HoveredItem % 500000;
            }
        }

        if (itemId == 0u)
        {
            Log.Warning("Failed to get item ID");
            return;
        }

        var item = DataManager.Excel.GetSheet<Item>().GetRowOrDefault(itemId);

        if (!item.HasValue)
        {
            Log.Warning("Failed to get item data for item ID {0}", itemId);
            return;
        }
        if (Configuration.WikiEnabled)
        {
            args.AddMenuItem(new MenuItem
            {
                Name = "Open Wiki Page",
                OnClicked = this.GetMenuItemClickedHandler(itemId),
                Prefix = SeIconChar.BoxedLetterW,
                PrefixColor = 28,
            });
        }
        if (Configuration.LodestoneEnabled)
        {
            args.AddMenuItem(new MenuItem
            {
                Name = "Open Eorzea DB Page",
                OnClicked = this.GetLodestoneMenuItemClickedHandler(itemId),
                Prefix = SeIconChar.BoxedLetterE,
                PrefixColor = 28,
            });
        }

    }

    private unsafe uint GetItemIdFromAgent(string? addonName)
    {
        var itemId = addonName switch
        {
            "ChatLog" => AgentChatLog.Instance()->ContextItemId,
            "GatheringNote" => *(uint*)((IntPtr)AgentGatheringNote.Instance() + 0xA0),
            "GrandCompanySupplyList" => *(uint*)((IntPtr)AgentGrandCompanySupply.Instance() + 0x54),
            "ItemSearch" => (uint)AgentContext.Instance()->UpdateCheckerParam,
            "RecipeNote" => AgentRecipeNote.Instance()->ContextMenuResultItemId,
            _ => 0u,
        };

        return itemId % 500000;
    }

    private Action<IMenuItemClickedArgs> GetMenuItemClickedHandler(uint itemId)
    {
        return (IMenuItemClickedArgs args) =>
        {
            try
            {
                var item = DataManager.Excel.GetSheet<Item>().GetRowOrDefault(itemId);
                var itemName = item.HasValue ? item.Value.Name : "Couldn't get name";
                Log.Debug(itemName.ToString() + "(ID " + itemId.ToString() + ")");
                Util.OpenLink(Configuration.WikiUrl + itemName.ToString());
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed on context menu for itemId" + itemId);
            }
        };
    }

    private Action<IMenuItemClickedArgs> GetLodestoneMenuItemClickedHandler(uint itemId)
    {
        return (IMenuItemClickedArgs args) =>
        {
            try
            {
                var id = GetLodestoneIdFromItemId(itemId);
                if (id != "")
                {
                    Util.OpenLink("https://na.finalfantasyxiv.com/lodestone/playguide/db/item/" + id.ToString());
                }
                else
                {
                    Notification error = new Notification();
                    error.Title = "Wikilinx Error";
                    var item = DataManager.Excel.GetSheet<Item>().GetRowOrDefault(itemId);
                    var itemName = item.HasValue ? item.Value.Name : "Couldn't get name";
                    error.Content = "Unable to find Eorzea DB page for item: " + itemName.ToString();
                    error.MinimizedText = "Couldn't find Eorzea DB page...";
                    NotificationManager.AddNotification(error);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed on context menu for itemId" + itemId);
            }
        };
    }

    private string GetLodestoneIdFromItemId(uint itemId)
    {
        try
        {
            string line;

            using (StreamReader file = new StreamReader(GenerateStreamFromString(global::Wikilinx.Properties.Resources.LodestoneItemId)))
            {
                for (int i = 1; i < itemId; i++)
                {
                    file.ReadLine();

                    if (file.EndOfStream)
                    {
                        throw new IndexOutOfRangeException("Item ID out of Lodestone resource file range!");
                    }
                }
                line = file.ReadLine() ?? "";
                return line;
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Couldn't fetch Lodestone ID for item ID " + itemId.ToString());
            return "";
        }
    }

    public static MemoryStream GenerateStreamFromString(string value)
    {
        return new MemoryStream(Encoding.UTF8.GetBytes(value ?? ""));
    }

    private void DrawUI() => WindowSystem.Draw();

    public void ToggleConfigUI() => ConfigWindow.Toggle();
}

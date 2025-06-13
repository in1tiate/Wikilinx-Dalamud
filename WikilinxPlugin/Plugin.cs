using Dalamud.Game;
using Dalamud.Game.Command;
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
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Wikilinx.Windows;

namespace Wikilinx
{
    public sealed class Plugin : IDalamudPlugin
    {
        [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
        [PluginService] internal static IDataManager DataManager { get; private set; } = null!;
        [PluginService] internal static IPluginLog Log { get; private set; } = null!;

        [PluginService] internal static IClientState ClientState { get; private set; } = null!;

        [PluginService] internal static IGameGui GameGui { get; private set; } = null!;
        [PluginService] internal static IContextMenu ContextMenu { get; private set; } = null!;

        [PluginService] internal static INotificationManager NotificationManager { get; private set; } = null!;

        [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;

        public Configuration Configuration { get; init; }

        public readonly WindowSystem WindowSystem = new("Wikilinx");

        private const string CommandName = "/wikilinx";

        private ConfigWindow ConfigWindow { get; init; }

        public Dictionary<string, string> I18nDict = [];
        public string LodestoneUrl = "";

        public Plugin()
        {
            Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();

            ConfigWindow = new ConfigWindow(this);

            ContextMenu.OnMenuOpened += this.OnContextMenuOpened;

            WindowSystem.AddWindow(ConfigWindow);

            CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
            {
                HelpMessage = "A useful message to display in /xlhelp"
            });

            PluginInterface.UiBuilder.Draw += DrawUI;

            PluginInterface.UiBuilder.OpenConfigUi += ToggleConfigUI;

            I18nDict = [];
            string lang_subdomain;

            switch (ClientState.ClientLanguage)
            {
                case ClientLanguage.Japanese:
                    lang_subdomain = "jp";
                    I18nDict = JsonConvert.DeserializeObject<Dictionary<string, string>>(Encoding.UTF8.GetString(Properties.Resources.JapaneseI18n)) ?? [];
                    break;
                case ClientLanguage.German:
                    I18nDict = JsonConvert.DeserializeObject<Dictionary<string, string>>(Encoding.UTF8.GetString(Properties.Resources.GermanI18n)) ?? [];
                    lang_subdomain = "de";
                    break;
                case ClientLanguage.French:
                    I18nDict = JsonConvert.DeserializeObject<Dictionary<string, string>>(Encoding.UTF8.GetString(Properties.Resources.FrenchI18n)) ?? [];
                    lang_subdomain = "fr";
                    break;
                default: // implicitly ClientLanguage.English
                    lang_subdomain = "na";
                    break;
            }
            LodestoneUrl = "https://" + lang_subdomain + ".finalfantasyxiv.com/lodestone/playguide/db/item/";
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
                    Name = Translate("Open Wiki Page"),
                    OnClicked = this.GetMenuItemClickedHandler(itemId),
                    Prefix = SeIconChar.BoxedLetterW,
                    PrefixColor = 28,
                });
            }
            if (Configuration.LodestoneEnabled)
            {
                args.AddMenuItem(new MenuItem
                {
                    Name = Translate("Open Eorzea DB Page"),
                    OnClicked = GetLodestoneMenuItemClickedHandler(itemId),
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
            return args =>
            {
                try
                {
                    var item = DataManager.Excel.GetSheet<Item>().GetRowOrDefault(itemId);
                    var itemName = item.HasValue ? item.Value.Name : Translate("Couldn't get name");
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
            return args =>
            {
                try
                {
                    var id = GetLodestoneIdFromItemId(itemId);
                    if (id != "")
                    {
                        Util.OpenLink(LodestoneUrl + id.ToString());
                    }
                    else
                    {
                        var error = new Notification();
                        error.Title = "Wikilinx Error";
                        var item = DataManager.Excel.GetSheet<Item>().GetRowOrDefault(itemId);
                        var itemName = item.HasValue ? item.Value.Name : Translate("Couldn't get name");
                        error.Content = Translate("Unable to find Eorzea DB page for item: {0}").Replace("{0}", itemName.ToString());
                        error.MinimizedText = Translate("Couldn't find Eorzea DB page...");
                        NotificationManager.AddNotification(error);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Failed on context menu for itemId" + itemId);
                }
            };
        }

        private static string GetLodestoneIdFromItemId(uint itemId)
        {
            try
            {
                string line;

                using var file = new StreamReader(GenerateStreamFromString(Properties.Resources.LodestoneItemId));
                for (var i = 1; i < itemId; i++)
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
            catch (Exception ex)
            {
                Log.Error(ex, "Couldn't fetch Lodestone ID for item ID " + itemId.ToString());
                return "";
            }
        }

        public string Translate(string input)
        {
            // lookup translated string
            if (!I18nDict.TryGetValue(input, out var output))
            {
                return input; // fallback to english
            }
            return output;
        }

        public static MemoryStream GenerateStreamFromString(string value)
        {
            return new MemoryStream(Encoding.UTF8.GetBytes(value ?? ""));
        }

        private void DrawUI() => WindowSystem.Draw();

        private void OnCommand(string command, string args) => ToggleConfigUI();

        public void ToggleConfigUI() => ConfigWindow.Toggle();
    }
}

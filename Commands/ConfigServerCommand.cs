using System;
using System.Text;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace TeleportationNetwork
{
    public class ConfigServerCommand : ServerChatCommand
    {
        public ConfigServerCommand(ICoreServerAPI api, Type type)
        {
            var manager = api.ModLoader.GetModSystem<ConfigManager>();

            Command = "cfg";
            Description = "";
            Syntax = "/cfg or /cfg [name] [value]";
            RequiredPrivilege = Privilege.controlserver;

            handler = (player, groupId, args) =>
            {
                string? name = args.PopWord();
                string? value = args.PopAll()?.Trim();

                var config = manager.GetConfig(type);

                if (name == null || value == null)
                {
                    var sb = new StringBuilder();
                    foreach (string str in ConfigUtil.GetAll(type, config))
                    {
                        sb.AppendLine(str + "");
                    }
                    api.SendMessage(player, groupId, sb.ToString(), EnumChatType.CommandSuccess);
                    return;
                }

                string? error = null;
                if (ConfigUtil.TrySetValue(type, config, name, value, ref error))
                {
                    manager.MarkConfigDirty(type);
                    api.SendMessage(player, groupId, "done", EnumChatType.CommandSuccess);
                }
                else
                {
                    api.SendMessage(player, groupId, error, EnumChatType.CommandError);
                }
            };
        }
    }
}

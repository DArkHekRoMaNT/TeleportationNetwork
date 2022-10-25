using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.ServerMods.WorldEdit;

namespace TeleportationNetwork
{
    public class ImportSchematicCommand : ServerChatCommand
    {
        public ImportSchematicCommand()
        {
            Command = "tpimp";
            Description = Core.ModPrefix + "Import teleport schematic";
            Syntax = "/tpimp [list] or  /tpimp [paste|import] name";
            RequiredPrivilege = Privilege.gamemode;
            handler = Handler;
        }

        private void Handler(IServerPlayer player, int groupId, CmdArgs args)
        {
            switch (args?.PopWord())
            {
                case "help":

                    player.SendMessage(groupId, "/tpimp [list|paste|import]",
                        EnumChatType.CommandError);
                    break;


                case "list":

                    List<IAsset> schematics = player.Entity.Api.Assets
                        .GetMany(Constants.TeleportSchematicPath);

                    if (schematics == null || schematics.Count == 0)
                    {
                        player.SendMessage(groupId, Lang.Get(Core.ModId + ":tpimp-empty"),
                            EnumChatType.CommandError);
                        break;
                    }

                    var list = new StringBuilder();
                    foreach (var sch in schematics)
                    {
                        list.AppendLine(sch.Name.Remove(sch.Name.Length - 5));
                    }
                    player.SendMessage(groupId, list.ToString(), EnumChatType.CommandSuccess);

                    break;


                case "paste":
                    BlockPos pos = player.Entity.Pos.AsBlockPos.Add(0, -1, 0);
                    LoadSchematic(player, groupId, args, "/tpimp paste [name]",
                        (schema) => PasteSchematic(schema, player.Entity.World, pos));
                    break;

                case "import":
                    LoadSchematic(player, groupId, args, "/tpimp import [name]",
                        (schema) => ImportSchematic(schema, player));
                    break;


                default:
                    player.SendMessage(groupId, "/tpimp [list|paste|import]",
                        EnumChatType.CommandError);
                    break;
            }
        }

        private static void LoadSchematic(IServerPlayer player, int groupId, CmdArgs args, string help,
            Action<BlockSchematic> action)
        {
            string name = args.PopWord();
            if (name == null || name.Length == 0)
            {
                player.SendMessage(groupId, help, EnumChatType.CommandError);
                return;
            }

            IAsset schema = player.Entity.Api.Assets
                .TryGet($"{Constants.TeleportSchematicPath}/{name}.json");

            if (schema == null)
            {
                player.SendMessage(groupId, Lang.Get(Core.ModId + ":tpimp-empty"),
                    EnumChatType.CommandError);
                return;
            }

            string? error = null;
            string fullpath = Path.Combine(schema.Origin.OriginPath, schema.Location.Path);
            BlockSchematic schematic = BlockSchematic.LoadFromFile(fullpath, ref error);

            if (error != null)
            {
                player.SendMessage(groupId, error, EnumChatType.CommandError);
                return;
            }

            action.Invoke(schematic);
        }

        private static void PasteSchematic(BlockSchematic schematic, IWorldAccessor world, BlockPos startPos, EnumOrigin origin = EnumOrigin.BottomCenter)
        {
            BlockPos originPos = schematic.GetStartPos(startPos, origin);
            schematic.Place(world.BlockAccessor, world, originPos);
        }

        private static void ImportSchematic(BlockSchematic schematic, IServerPlayer player)
        {
            var we = player.Entity.Api.ModLoader.GetModSystem<WorldEdit>();
            if (we.CanUseWorldEdit(player, true))
            {
                FieldInfo toolFieldInfo = typeof(WorldEditWorkspace)
                    .GetField("ToolInstance", BindingFlags.NonPublic | BindingFlags.Instance);

                FieldInfo clipboardFieldInfo = typeof(WorldEditWorkspace)
                    .GetField("clipboardBlockData", BindingFlags.NonPublic | BindingFlags.Instance);

                var workspace = we.GetWorkSpace(player.PlayerUID);
                workspace.ToolsEnabled = true;
                workspace.SetTool("Import");

                clipboardFieldInfo.SetValue(workspace, schematic);
                var tool = (ImportTool)toolFieldInfo.GetValue(workspace);

                tool.OnWorldEditCommand(we, new CmdArgs("imc"));

                we.SendPlayerWorkSpace(player.PlayerUID);
            }
        }
    }
}

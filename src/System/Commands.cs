using System.Collections.Generic;
using System.IO;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace TeleportationNetwork
{
    public class Commands : ModSystem
    {
        ICoreServerAPI _sapi = null!;

        public override void StartServerSide(ICoreServerAPI api)
        {
            this._sapi = api;

            api.RegisterCommand("tpimp", Core.ModPrefix + "Import teleport schematic", "[list|paste]",
                ImportSchematicCommand, Privilege.controlserver);

            api.RegisterCommand("rndtp", Core.ModPrefix + "Teleport player to random location", "[range]",
                (player, groupId, args) => TeleportUtil.RandomTeleport(player, args.PopInt() ?? -1), Privilege.tp);
        }

        private void ImportSchematicCommand(IServerPlayer player, int groupId, CmdArgs args)
        {
            switch (args?.PopWord())
            {
                case "help":

                    player.SendMessage(groupId, "/tpimp [list|paste]", EnumChatType.CommandError);
                    break;


                case "list":

                    List<IAsset> schematics = _sapi.Assets.GetMany(Constants.TeleportSchematicPath);

                    if (schematics == null || schematics.Count == 0)
                    {
                        player.SendMessage(groupId, Lang.Get(Core.ModId + ":tpimp-empty"), EnumChatType.CommandError);
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

                    string name = args.PopWord();
                    if (name == null || name.Length == 0)
                    {
                        player.SendMessage(groupId, "/tpimp paste [name]", EnumChatType.CommandError);
                        break;
                    }

                    IAsset schema = _sapi.Assets.TryGet($"{Constants.TeleportSchematicPath}/{name}.json");
                    if (schema == null)
                    {
                        player.SendMessage(groupId, Lang.Get(Core.ModId + ":tpimp-empty"), EnumChatType.CommandError);
                        break;
                    }

                    string? error = null;
                    string fullpath = Path.Combine(schema.Origin.OriginPath, schema.Location.Path);
                    BlockSchematic schematic = BlockSchematic.LoadFromFile(fullpath, ref error);

                    if (error != null)
                    {
                        player.SendMessage(groupId, error, EnumChatType.CommandError);
                        break;
                    }

                    PasteSchematic(schematic, player.Entity.Pos.AsBlockPos.Add(0, -1, 0));

                    break;


                default:
                    player.SendMessage(groupId, "/tpimp [list|paste]", EnumChatType.CommandError);
                    break;
            }
        }

        private void PasteSchematic(BlockSchematic blockData, BlockPos startPos, EnumOrigin origin = EnumOrigin.BottomCenter)
        {
            BlockPos originPos = blockData.GetStartPos(startPos, origin);
            blockData.Place(_sapi.World.BlockAccessor, _sapi.World, originPos);
        }

        public override void StartClientSide(ICoreClientAPI api)
        {
            api.RegisterCommand("tpdlg", Core.ModPrefix + "Open teleport dialog (creative only)", "", (int groupId, CmdArgs args) =>
            {
                if (api.World.Player.WorldData.CurrentGameMode == EnumGameMode.Creative)
                {
                    GuiDialogTeleportList dialog = new(api, null);
                    dialog.TryOpen();
                }
            });
        }
    }
}

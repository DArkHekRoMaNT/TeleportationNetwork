using System;
using System.Collections.Generic;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

namespace TeleportationNetwork
{
    public class Commands : ModSystem
    {

        #region server

        ICoreServerAPI sapi;
        public override void StartServerSide(ICoreServerAPI api)
        {
            base.StartServerSide(api);

            this.sapi = api;

            api.RegisterCommand("tpimp", Constants.PREFIX_DSC + "Import teleport schematic", "[list|paste]",
                (IServerPlayer player, int groupId, CmdArgs args) =>
                {
                    switch (args?.PopWord())
                    {
                        case "help":

                            player.SendMessage(groupId, "/tpimp [list|paste]", EnumChatType.CommandError);
                            break;


                        case "list":

                            List<IAsset> schematics = api.Assets.GetMany(Constants.TELEPORT_SCHEMATIC_PATH, Constants.MOD_ID);

                            if (schematics == null || schematics.Count == 0)
                            {
                                player.SendMessage(groupId, Lang.Get("Not found"), EnumChatType.CommandError);
                                break;
                            }

                            StringBuilder list = new StringBuilder();
                            foreach (var sch in schematics)
                            {
                                list.AppendLine(sch.Location.Path.Substring(
                                    Constants.TELEPORT_SCHEMATIC_PATH.Length + 1,
                                    sch.Location.Path.Length - Constants.TELEPORT_SCHEMATIC_PATH.Length + 1 + 5
                                ));
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

                            IAsset schema = api.Assets.TryGet($"{Constants.MOD_ID}:{Constants.TELEPORT_SCHEMATIC_PATH}/{name}.json");
                            if (schema == null)
                            {
                                player.SendMessage(groupId, Lang.Get("Not found"), EnumChatType.CommandError);
                                break;
                            }

                            string error = null;

                            BlockSchematic schematic = BlockSchematic.LoadFromFile(
                                schema.Origin.OriginPath + "/" + Constants.MOD_ID + "/" + schema.Location.Path, ref error);

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
                },
                Privilege.controlserver
            );

            api.RegisterCommand("rndtp", Constants.PREFIX_DSC + "Teleport player to random location", "[range]",
                (IServerPlayer player, int groupId, CmdArgs args) =>
                {
                    RandomTeleport(player, (int)args.PopInt(-1));
                },
                Privilege.tp
            );

            api.RegisterCommand("tpnetconfig", Constants.PREFIX_DSC + "Config for TPNet", "[shared|unbreakable]",
                (IServerPlayer player, int groupId, CmdArgs args) =>
                {
                    switch (args?.PopWord())
                    {
                        case "help":

                            player.SendMessage(groupId, "/tpnetconfig [shared|unbreakable]", EnumChatType.CommandError);
                            break;


                        case "shared":

                            Config.Current.SharedTeleports.Val = !Config.Current.SharedTeleports.Val;
                            api.StoreModConfig<Config>(Config.Current, api.GetWorldId() + "/" + Constants.MOD_ID);
                            player.SendMessage(groupId, "Shared teleports now is " + (Config.Current.SharedTeleports.Val ? "On" : "Off"), EnumChatType.CommandSuccess);
                            break;


                        case "unbreakable":

                            Config.Current.Unbreakable.Val = !Config.Current.Unbreakable.Val;
                            api.StoreModConfig<Config>(Config.Current, api.GetWorldId() + "/" + Constants.MOD_ID);
                            player.SendMessage(groupId, "Unbreakable teleports now is " + (Config.Current.Unbreakable.Val ? "On" : "Off"), EnumChatType.CommandSuccess);
                            break;


                        default:
                            player.SendMessage(groupId, "/tpnetconfig [shared|unbreakable]", EnumChatType.CommandError);
                            break;
                    }
                },
                Privilege.tp
            );


        }

        private void PasteSchematic(BlockSchematic blockData, BlockPos startPos, EnumOrigin origin = EnumOrigin.MiddleCenter)
        {
            BlockPos originPos = blockData.GetStartPos(startPos, origin);
            blockData.Place(sapi.World.BlockAccessor, sapi.World, originPos);
        }

        public static void RandomTeleport(IServerPlayer player, int range = -1)
        {
            try
            {
                ICoreServerAPI api = player.Entity.Api as ICoreServerAPI;

                int x, y, z;
                if (range != -1)
                {
                    x = api.World.Rand.Next(range * 2) - range + player.Entity.Pos.XYZInt.X;
                    z = api.World.Rand.Next(range * 2) - range + player.Entity.Pos.XYZInt.Z;
                }
                else
                {
                    x = api.World.Rand.Next(api.WorldManager.MapSizeX);
                    z = api.World.Rand.Next(api.WorldManager.MapSizeZ);
                }

                //y = (api.WorldManager.GetSurfacePosY(x, z) ?? api.WorldManager.MapSizeY);
                //y += 2;

                y = api.WorldManager.MapSizeY;

                player.SendMessageAsClient("/tp =" + x + " " + y + " =" + z);
                player.Entity.PositionBeforeFalling = new Vec3d(x, 0, z);
            }
            catch (Exception e)
            {
                player?.Entity?.Api?.Logger?.ModError("Failed to teleport player to random location.");
                player?.Entity?.Api?.Logger?.ModError(e.Message);
            }
        }

        #endregion

        #region client

        ICoreClientAPI capi;
        public override void StartClientSide(ICoreClientAPI api)
        {
            this.capi = api;

            api.RegisterCommand("csc", Constants.PREFIX_DSC + "Clear shapes cache", "", (int groupId, CmdArgs args) =>
            {
                api.ObjectCache.RemoveAll((str, obj) => str.StartsWith(Constants.MOD_ID));
            });

            // TODO Need move dialog to TPNetManager -.-
            api.RegisterCommand("tpdlg", Constants.PREFIX_DSC + "Open teleport dialog", "", (int groupId, CmdArgs args) =>
            {
                if (capi.World.Player.WorldData.CurrentGameMode != EnumGameMode.Creative) return;

                TPNetManager manager = api.ModLoader.GetModSystem<TPNetManager>();

                GuiDialogTeleport dialog = new GuiDialogTeleport(capi, null);
                dialog.TryOpen();
            });
        }

        #endregion
    }
}
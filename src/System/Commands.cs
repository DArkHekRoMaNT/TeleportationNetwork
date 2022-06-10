using System;
using System.Collections.Generic;
using System.Text;
using SharedUtils.Extensions;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace TeleportationNetwork
{
    public class Commands : ModSystem
    {
        ICoreServerAPI sapi;
        public override void StartServerSide(ICoreServerAPI api)
        {
            base.StartServerSide(api);

            sapi = api;

            api.RegisterCommand("tpimp", Core.ModPrefix + "Import teleport schematic", "[list|paste]",
                (IServerPlayer player, int groupId, CmdArgs args) =>
                {
                    switch (args?.PopWord())
                    {
                        case "help":

                            player.SendMessage(groupId, "/tpimp [list|paste]", EnumChatType.CommandError);
                            break;


                        case "list":

                            List<IAsset> schematics = api.Assets.GetMany(Constants.TeleportSchematicPath);

                            if (schematics == null || schematics.Count == 0)
                            {
                                player.SendMessage(groupId, Lang.Get(Core.ModId + ":tpimp-empty"), EnumChatType.CommandError);
                                break;
                            }

                            StringBuilder list = new StringBuilder();
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

                            IAsset schema = api.Assets.TryGet($"{Constants.TeleportSchematicPath}/{name}.json");
                            if (schema == null)
                            {
                                player.SendMessage(groupId, Lang.Get(Core.ModId + ":tpimp-empty"), EnumChatType.CommandError);
                                break;
                            }

                            string error = null;

                            BlockSchematic schematic = BlockSchematic.LoadFromFile(
                                schema.Origin.OriginPath + "/" + schema.Location.Path, ref error);

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

            api.RegisterCommand("rndtp", Core.ModPrefix + "Teleport player to random location", "[range]",
                (IServerPlayer player, int groupId, CmdArgs args) =>
                {
                    RandomTeleport(player, (int)args.PopInt(-1));
                },
                Privilege.tp
            );

            api.RegisterCommand("tpnetconfig", Core.ModPrefix + "Config for TPNet", "[shared|unbreakable]",
                (IServerPlayer player, int groupId, CmdArgs args) =>
                {
                    switch (args?.PopWord())
                    {
                        case "help":

                            player.SendMessage(groupId, "/tpnetconfig [shared|unbreakable]", EnumChatType.CommandError);
                            break;


                        case "shared":

                            Config.Current.LegacySharedTeleports.Val = !Config.Current.LegacySharedTeleports.Val;
                            api.StoreModConfig<Config>(Config.Current, api.GetWorldId() + "/" + Core.ModId);
                            player.SendMessage(groupId, Lang.Get(Core.ModId + ":config-shared", Config.Current.LegacySharedTeleports.Val ? "on" : "off"), EnumChatType.CommandSuccess);
                            break;


                        case "unbreakable":

                            Config.Current.Unbreakable.Val = !Config.Current.Unbreakable.Val;
                            api.StoreModConfig<Config>(Config.Current, api.GetWorldId() + "/" + Core.ModId);
                            player.SendMessage(groupId, Lang.Get(Core.ModId + ":config-unbreakable", Config.Current.Unbreakable.Val ? "on" : "off"), EnumChatType.CommandSuccess);
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

        public static void RandomTeleport(IServerPlayer player, int range = -1, Vec3i pos = null)
        {
            try
            {
                ICoreServerAPI api = player.Entity.Api as ICoreServerAPI;


                int x, z;
                if (range != -1)
                {
                    if (pos == null) pos = player.Entity.Pos.XYZInt;

                    x = api.World.Rand.Next(range * 2) - range + pos.X;
                    z = api.World.Rand.Next(range * 2) - range + pos.Z;
                }
                else
                {
                    x = api.World.Rand.Next(api.WorldManager.MapSizeX);
                    z = api.World.Rand.Next(api.WorldManager.MapSizeZ);
                }

                int chunkSize = api.WorldManager.ChunkSize;
                player.Entity.TeleportToDouble(x + 0.5f, api.WorldManager.MapSizeY + 2, z + 0.5f);
                api.WorldManager.LoadChunkColumnPriority(x / chunkSize, z / chunkSize, new ChunkLoadOptions()
                {
                    OnLoaded = () =>
                    {
                        var y = api.WorldManager.GetSurfacePosY(x, z);
                        player.Entity.TeleportToDouble(x + 0.5f, (int)y + 2, z + 0.5f);
                    }
                });
            }
            catch (Exception e)
            {
                Core.ModLogger.Error("Failed to teleport player to random location.");
                Core.ModLogger.Error(e.Message);
            }
        }

        public override void StartClientSide(ICoreClientAPI api)
        {
            api.RegisterCommand("tpdlg", Core.ModPrefix + "Open teleport dialog", "", (int groupId, CmdArgs args) =>
            {
                if (api.World.Player.WorldData.CurrentGameMode != EnumGameMode.Creative) return;
                GuiDialogTeleportList dialog = new GuiDialogTeleportList(api, null);
                dialog.TryOpen();
            });
        }
    }
}
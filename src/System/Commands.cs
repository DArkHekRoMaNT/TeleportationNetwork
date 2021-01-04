using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

namespace TeleportationNetwork
{
    public class Commands : ModSystem
    {
        string prefix_dsc = "[" + Constants.MOD_ID + "] ";

        #region server

        ICoreServerAPI sapi;
        public override void StartServerSide(ICoreServerAPI api)
        {
            base.StartServerSide(api);

            this.sapi = api;

            api.RegisterCommand("gentp", prefix_dsc + "Generate teleport", "[xyz]",
                (IServerPlayer player, int groupId, CmdArgs args) =>
                {
                    if (args?.PeekWord() == "help")
                    {
                        player.SendMessage(groupId, "/gentp [xyz]", EnumChatType.CommandError);
                        return;
                    }

                    Vec3d pos;

                    if (args?.Length >= 3)
                    {
                        pos = args.PopFlexiblePos(player.Entity.Pos.XYZ, api.World.DefaultSpawnPosition.XYZ);
                    }
                    else pos = player.Entity.Pos.XYZ.AddCopy(0, -1, 0);

                    if (pos == null)
                    {
                        player.SendMessage(groupId, "/gentp [xyz]", EnumChatType.CommandError);
                        return;
                    }

                    //WorldGen.GenerateTeleport(api, pos.AsBlockPos);
                }, Privilege.useblockseverywhere
            );

            api.RegisterCommand("rndtp", prefix_dsc + "Teleport player to random location", "",
                (IServerPlayer player, int groupId, CmdArgs args) =>
                {
                    RandomTeleport(player);
                }, Privilege.tp
            );
        }
        public static void RandomTeleport(IServerPlayer player)
        {
            try
            {
                ICoreServerAPI api = player.Entity.Api as ICoreServerAPI;

                int x = api.World.Rand.Next(api.WorldManager.MapSizeX);
                int z = api.World.Rand.Next(api.WorldManager.MapSizeZ);

                x -= x / 2;
                z -= z / 2;

                api.WorldManager.LoadChunkColumnPriority(x / api.WorldManager.ChunkSize, z / api.WorldManager.ChunkSize);

                int y = -1;
                /*for (int i = api.WorldManager.MapSizeY - 1; i >= 0; i--)
                {
                    if (api.World.BlockAccessor.GetBlock(x, i, z).SideOpaque[BlockFacing.UP.Index])
                    {
                        y = i + 2;
                        break;
                    }
                }*/

                if (y == -1) y = 300;
                player.SendMessageAsClient("/tp " + x + " " + y + " " + z);
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

            api.RegisterCommand("csc", prefix_dsc + "Clear shapes cache", "", (int groupId, CmdArgs args) =>
            {
                api.ObjectCache.RemoveAll((str, obj) => str.StartsWith(Constants.MOD_ID));
            });

            // api.RegisterCommand("tpdlg", prefix_dsc + "Open teleport dialog", "", (int groupId, CmdArgs args) =>
            // {
            //     TPNetManager manager = api.ModLoader.GetModSystem<TPNetManager>();
            // });
        }

        #endregion
    }
}
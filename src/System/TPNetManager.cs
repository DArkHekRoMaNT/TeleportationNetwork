using System;
using System.Collections.Generic;
using System.Linq;
using ProtoBuf;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

namespace TeleportationNetwork
{
    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class TeleportData
    {
        public bool Available;
        public string Name;
        public BlockPos Pos;
    }

    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class ForTeleportingData
    {
        public Vec3d SourcePos;
        public Vec3d TargetPos;
    }

    public class TPNetManager : ModSystem
    {
        #region server

        internal static Dictionary<BlockPos, TeleportData> AllTeleports = new Dictionary<BlockPos, TeleportData>();
        public static List<string> defNames;

        ICoreServerAPI sapi;
        IServerNetworkChannel serverChannel;

        public override void StartServerSide(ICoreServerAPI api)
        {
            this.sapi = api;

            api.Event.SaveGameLoaded += OnLoadGame;
            api.Event.GameWorldSave += OnSaveGame;

            defNames = api.Assets.Get(new AssetLocation(Constants.MOD_ID, "config/names.json"))?.ToObject<List<string>>();
            if (defNames == null) defNames = new List<string>(new string[] { "null" });

            serverChannel = api.Network
                .RegisterChannel("tpnet")
                .RegisterMessageType(typeof(ForTeleportingData))
                .SetMessageHandler<ForTeleportingData>(OnTeleport);
        }

        internal TeleportData GetOrCreateData(BlockPos pos, bool available = false)
        {
            if (pos == null) return null;

            TeleportData data = null;
            if (AllTeleports.TryGetValue(pos, out data))
            {
                return data;
            }

            data = new TeleportData()
            {
                Pos = pos.Copy(),
                Available = available,
                Name = defNames.ElementAt(sapi.World.Rand.Next(defNames.Count))
            };
            AllTeleports.Add(data.Pos, data);
            string type = data.Available ? "normal" : "broken";
            sapi.World.Logger.ModNotification($"Added teleport {data.Name} ({type}) at {data.Pos} to teleports list");

            return data;
        }

        public void DeleteData(BlockPos pos)
        {
            string type = AllTeleports[pos].Available ? "normal" : "broken";
            string name = AllTeleports[pos].Name;
            AllTeleports.Remove(pos);
            sapi.World.Logger.ModNotification($"Removed teleport {name} ({type}) at {pos} from teleports list");
        }

        private void OnSaveGame()
        {
            sapi.WorldManager.SaveGame.StoreData("TPNetData", SerializerUtil.Serialize(AllTeleports));

            foreach (var player in sapi.World.AllPlayers)
            {
                SaveAvailableTeleportsFromPlayer(player as IServerPlayer);
            }
        }

        private void OnLoadGame()
        {
            try
            {
                sapi.World.Logger.ModNotification("Start loading data");
                byte[] data = sapi.WorldManager.SaveGame.GetData("TPNetData");
                if (data != null) AllTeleports = SerializerUtil.Deserialize<Dictionary<BlockPos, TeleportData>>(data);

                foreach (var tp in AllTeleports)
                {
                    sapi.World.Logger.ModDebug($"Loaded teleport data for {tp.Value.Name} at {tp.Key}");
                }

                foreach (var player in sapi.World.AllPlayers)
                {
                    LoadAvailableTeleportsToPlayer(player as IServerPlayer);
                }

                sapi.World.Logger.ModNotification("Data loaded");


            }
            catch (Exception e)
            {
                sapi.World.Logger.ModError($"Failed loading data: {e}");
            }
        }

        internal void SaveAvailableTeleportsFromPlayer(IServerPlayer player)
        {
            BlockPos[] at = player.Entity.WatchedAttributes.GetBlockPosArray("availableteleports");
            byte[] data = SerializerUtil.Serialize<BlockPos[]>(at);

            player.SetModdata(Constants.MOD_ID + "availableteleports", data);
            sapi.World.Logger.ModDebug($"Saved available teleports from {player.PlayerName} ({player.PlayerUID})");
        }

        internal void LoadAvailableTeleportsToPlayer(IServerPlayer player)
        {
            byte[] data = player.GetModdata(Constants.MOD_ID + "availableteleports");
            BlockPos[] at = SerializerUtil.Deserialize<BlockPos[]>(data);

            player.Entity.WatchedAttributes.SetBlockPosArray("availableteleports", at);
            sapi.World.Logger.ModDebug($"Loaded available teleports to {player.PlayerName} ({player.PlayerUID})");
        }

        public void TeleportTo(Vec3d targetPos, Vec3d sourcePos = null)
        {
            clientChannel.SendPacket(new ForTeleportingData()
            {
                SourcePos = sourcePos,
                TargetPos = targetPos
            });
        }

        private void OnTeleport(IServerPlayer fromPlayer, ForTeleportingData data)
        {
            Entity[] tpEntities;
            Vec3d currCenterPos;

            if (data.TargetPos == null) return;

            if (data.SourcePos == null)
            {
                if (fromPlayer.Entity == null) return;

                tpEntities = new Entity[1] { fromPlayer.Entity };
                currCenterPos = fromPlayer.Entity.Pos.XYZ;
            }
            else
            {
                BlockEntityTeleport bet = sapi.World.BlockAccessor.GetBlockEntity(data.SourcePos.AsBlockPos) as BlockEntityTeleport;
                if (bet == null) return;

                tpEntities = bet.GetInCircleEntities();
                currCenterPos = bet.Pos.ToVec3d().Add(0.5, 1, 0.5);
            }

            if (tpEntities == null) return;


            string name = AllTeleports[data.TargetPos.AsBlockPos]?.Name;
            foreach (var entity in tpEntities)
            {
                double x = data.TargetPos.X + (entity.Pos.X - currCenterPos.X) + 0.5;
                double y = data.TargetPos.Y + (entity.Pos.Y - currCenterPos.Y) + 2;
                double z = data.TargetPos.Z + (entity.Pos.Z - currCenterPos.Z) + 0.5;

                entity.TeleportToDouble(x, y, z);

                if (entity is EntityPlayer player)
                {
                    player.SetActivityRunning("teleportCooldown", 5000);
                }

                sapi.World.Logger.ModNotification($"{entity?.GetName()} teleported to {x}, {y}, {z} ({name})");
            }
        }




        public static BlockPos[] GetAvailableTeleports(IPlayer player)
        {
            return player.Entity.WatchedAttributes.GetBlockPosArray("availableteleports");
        }

        public static void SetAvailableTeleports(IPlayer player, BlockPos[] atPos)
        {
            player.Entity.WatchedAttributes.SetBlockPosArray("availableteleports", atPos);
        }

        public static void AddAvailableTeleport(IPlayer player, BlockPos pos)
        {
            List<BlockPos> atPos = GetAvailableTeleports(player)?.ToList() ?? new List<BlockPos>();
            if (!atPos.Contains(pos))
            {
                atPos.Add(pos);
                SetAvailableTeleports(player, atPos.ToArray());
            }
        }

        public static void RemoveAvailableTeleport(IPlayer player, BlockPos pos)
        {
            List<BlockPos> atPos = GetAvailableTeleports(player)?.ToList();
            if (atPos.Contains(pos))
            {
                atPos?.Remove(pos);
                SetAvailableTeleports(player, atPos?.ToArray());
            }
        }

        public static Dictionary<BlockPos, TeleportData> GetAvailableTeleportsWithData(IPlayer player)
        {
            if (player.WorldData.CurrentGameMode == EnumGameMode.Creative) return AllTeleports;
            if (Config.Current.SharedTeleports.Val)
            {
                return AllTeleports.Where((dict) => dict.Value.Available)?.ToDictionary(dict => dict.Key, dict => dict.Value);
            }

            BlockPos[] atPos = GetAvailableTeleports(player);
            if (atPos == null || atPos.Length == 0) return null;

            return AllTeleports.Where((dict) => atPos.Contains(dict.Key))?.ToDictionary(dict => dict.Key, dict => dict.Value);
        }


        #endregion

        #region client

        ICoreClientAPI capi;
        IClientNetworkChannel clientChannel;

        public override void StartClientSide(ICoreClientAPI api)
        {
            this.capi = api;

            clientChannel = api.Network
                .RegisterChannel("tpnet")
                .RegisterMessageType(typeof(ForTeleportingData));
        }

        #endregion
    }
}
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Newtonsoft.Json.Converters;
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

        internal TeleportData GetOrCreateData(BlockPos pos)
        {
            TeleportData data = null;
            if (AllTeleports.TryGetValue(pos, out data))
            {
                return data;
            }

            data = new TeleportData()
            {
                Pos = pos.Copy(),
                Available = false,
                Name = defNames.ElementAt(sapi.World.Rand.Next(defNames.Count))
            };
            AllTeleports.Add(data.Pos, data);
            sapi.World.Logger.ModNotification($"Added teleport {data.Name} at {data.Pos} to teleports list");

            return data;
        }

        public void DeleteData(BlockPos pos)
        {
            string name = AllTeleports[pos].Name;
            AllTeleports.Remove(pos);
            sapi.World.Logger.ModNotification($"Removed teleport {name} at {pos} from teleports list");
        }

        private void OnSaveGame()
        {
            sapi.WorldManager.SaveGame.StoreData("TPNetData", SerializerUtil.Serialize(AllTeleports));

            foreach (var player in sapi.World.AllPlayers)
            {
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

                sapi.World.Logger.ModNotification("Data loaded");
            }
            catch (Exception e)
            {
                sapi.World.Logger.ModError($"Failed loading data: {e}");
            }
        }


        internal static void SaveAvailableTeleports(IServerPlayer player)
        {
            string[] at = player.Entity.WatchedAttributes.GetStringArray("availableteleports");
            byte[] data = SerializerUtil.Serialize<string[]>(at);

            player.SetModdata(Constants.MOD_ID + "availableteleports", data);
        }

        internal static void LoadAvailableTeleports(IServerPlayer player)
        {
            byte[] data = player.GetModdata(Constants.MOD_ID + "availableteleports");
            string[] at = SerializerUtil.Deserialize<string[]>(data);

            player.Entity.WatchedAttributes.SetStringArray("availableteleports", at);
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
                BETeleport bet = sapi.World.BlockAccessor.GetBlockEntity(data.SourcePos.AsBlockPos) as BETeleport;
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
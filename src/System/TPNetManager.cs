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
    [ProtoContract]
    public class TeleportData
    {
        [ProtoMember(1)]
        public string Name;

        [ProtoMember(2)]
        public bool Available;

        [ProtoMember(3)]
        public List<string> ActivatedBy = new List<string>();

        public TeleportData()
        {

        }

        public TeleportData(string name, List<string> activatedBy = null, bool available = false)
        {
            this.Name = name;
            this.Available = available;

            if (activatedBy != null)
            {
                this.ActivatedBy = activatedBy;
            }
        }
    }

    [ProtoContract]
    public class TeleportMsg
    {
        [ProtoMember(1)]
        public BlockPos Pos;

        [ProtoMember(2)]
        public TeleportData Data;

        [ProtoMember(3)]
        public bool DoRemove;

        public TeleportMsg()
        {

        }

        public TeleportMsg(BlockPos pos, TeleportData data, bool doRemove = false)
        {
            this.Pos = pos;
            this.Data = data;
            this.DoRemove = doRemove;
        }
    }

    [ProtoContract]
    public class ForTeleportingData
    {
        [ProtoMember(1)]
        public Vec3d SourcePos;

        [ProtoMember(2)]
        public Vec3d TargetPos;
    }

    public class TPNetManager : ModSystem
    {
        #region server

        private static Dictionary<BlockPos, TeleportData> AllTeleports = new Dictionary<BlockPos, TeleportData>();
        public static List<string> defNames;

        static ICoreServerAPI sapi;
        static IServerNetworkChannel serverChannel;

        public override void StartServerSide(ICoreServerAPI api)
        {
            sapi = api;

            api.Event.SaveGameLoaded += OnLoadGame;
            api.Event.GameWorldSave += OnSaveGame;
            api.Event.PlayerJoin += PushAvailableTeleports;

            defNames = api.Assets.Get(new AssetLocation(Constants.MOD_ID, "config/names.json"))?.ToObject<List<string>>();
            if (defNames == null) defNames = new List<string>(new string[] { "null" });

            serverChannel = api.Network
                .RegisterChannel("tpnet")
                .RegisterMessageType(typeof(ForTeleportingData))
                .RegisterMessageType(typeof(TeleportMsg))
                .SetMessageHandler<ForTeleportingData>(OnTeleport)
            ;
        }

        private void PushAvailableTeleports(IServerPlayer byPlayer)
        {
            string playerUID = byPlayer?.PlayerUID;

            Dictionary<BlockPos, TeleportData> availableTeleports;

            if (Config.Current.SharedTeleports.Val)
            {
                availableTeleports = AllTeleports;
            }
            else
            {
                availableTeleports = AllTeleports
                    ?.Where((dict) => dict.Value.ActivatedBy.Contains(playerUID))
                    ?.ToDictionary(dict => dict.Key, dict => dict.Value)
                ;
            }

            if (availableTeleports != null)
            {
                foreach (var tp in availableTeleports)
                {
                    serverChannel.SendPacket(new TeleportMsg(
                        tp.Key,
                        tp.Value
                    ));
                }
            }
        }

        internal static void AddAvailableTeleport(IServerPlayer byPlayer, BlockPos pos)
        {
            if (byPlayer == null) throw new ArgumentNullException();
            if (byPlayer?.Entity == null) return;

            if (AllTeleports.ContainsKey(pos))
            {
                if (!AllTeleports[pos].ActivatedBy.Contains(byPlayer.PlayerUID))
                {
                    AllTeleports[pos].ActivatedBy.Add(byPlayer.PlayerUID);
                }

                serverChannel.SendPacket(new TeleportMsg(
                    pos,
                    AllTeleports[pos]
                ), byPlayer);
            }
        }

        internal static void AddTeleport(BlockPos pos, TeleportData data)
        {
            if (!AllTeleports.ContainsKey(pos))
            {
                AllTeleports.Add(pos, data);
                foreach (string playerUID in data.ActivatedBy)
                {
                    IServerPlayer player = sapi.World.PlayerByUid(playerUID) as IServerPlayer;
                    if (player != null)
                    {
                        serverChannel.SendPacket(new TeleportMsg(
                            pos,
                            data
                        ), player);
                    }
                }

                string type = data.Available ? "normal" : "broken";
                sapi.World.Logger.ModNotification($"Added teleport {data.Name} ({type}) at {pos} to teleports list");
            }
        }

        internal static void RemoveTeleport(BlockPos pos)
        {
            if (sapi == null) return;
            if (AllTeleports.ContainsKey(pos))
            {
                string type = AllTeleports[pos].Available ? "normal" : "broken";
                string name = AllTeleports[pos].Name;

                serverChannel.SendPacket(new TeleportMsg(
                    pos,
                    AllTeleports[pos],
                    true
                ), sapi.World.AllOnlinePlayers as IServerPlayer[]);
                AllTeleports.Remove(pos);

                sapi.World.Logger.ModNotification($"Removed teleport {name} ({type}) at {pos} from teleports list");
            }
        }

        internal static TeleportData GetTeleport(BlockPos pos)
        {
            if (capi != null)
            {
                return AvailableTeleports.ContainsKey(pos) ? AvailableTeleports[pos] : null;
            }
            else if (sapi != null)
            {
                return AllTeleports.ContainsKey(pos) ? AllTeleports[pos] : null;
            }
            else return null;
        }


        internal static TeleportData GetOrCreateData(BlockPos pos, bool available = false)
        {
            if (pos == null) throw new ArgumentNullException();

            TeleportData data = null;
            if (AllTeleports.TryGetValue(pos, out data))
            {
                return data;
            }

            data = new TeleportData()
            {
                Available = available,
                Name = defNames.ElementAt(sapi.World.Rand.Next(defNames.Count))
            };

            AddTeleport(pos.Copy(), data);

            return data;
        }

        private void OnSaveGame()
        {
            sapi.WorldManager.SaveGame.StoreData("TPNetData", SerializerUtil.Serialize(AllTeleports));
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

        public static void TeleportTo(Vec3d targetPos, Vec3d sourcePos = null)
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

        #endregion

        #region client

        internal static Dictionary<BlockPos, TeleportData> AvailableTeleports = new Dictionary<BlockPos, TeleportData>();
        static ICoreClientAPI capi;
        static IClientNetworkChannel clientChannel;

        public override void StartClientSide(ICoreClientAPI api)
        {
            capi = api;

            clientChannel = api.Network
                .RegisterChannel("tpnet")
                .RegisterMessageType(typeof(ForTeleportingData))
                .RegisterMessageType(typeof(TeleportMsg))
                .SetMessageHandler<TeleportMsg>(OnClientReceiveTeleportMsg)
            ;
        }

        private void OnClientReceiveTeleportMsg(TeleportMsg msg)
        {
            if (!msg.DoRemove)
            {
                if (AvailableTeleports.ContainsKey(msg.Pos))
                {
                    AvailableTeleports[msg.Pos] = msg.Data;
                }
                else
                {
                    AvailableTeleports.Add(msg.Pos, msg.Data);
                }
            }
            else
            {
                if (AvailableTeleports.ContainsKey(msg.Pos))
                {
                    AvailableTeleports.Remove(msg.Pos);
                }
            }
        }

        #endregion

        public override void Dispose()
        {
            base.Dispose();

            capi = null;
            sapi = null;
        }
    }
}
using System;
using System.Collections.Generic;
using System.Linq;
using ProtoBuf;
using SharedUtils;
using SharedUtils.Extensions;
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

        [ProtoMember(4)]
        public bool Synced;

        public TeleportMsg()
        {

        }

        public TeleportMsg(BlockPos pos, TeleportData data, bool doRemove = false, bool synced = false)
        {
            this.Pos = pos;
            this.Data = data;
            this.DoRemove = doRemove;
            this.Synced = synced;
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

        public static List<string> defNames;

        static ICoreServerAPI sapi;
        static IServerNetworkChannel serverChannel;

        public override void StartServerSide(ICoreServerAPI api)
        {
            sapi = api;

            api.Event.SaveGameLoaded += OnLoadGame;
            api.Event.GameWorldSave += OnSaveGame;
            api.Event.PlayerJoin += PushTeleports;

            serverChannel = api.Network
                .RegisterChannel("tpnet")
                .RegisterMessageType(typeof(ForTeleportingData))
                .RegisterMessageType(typeof(TeleportMsg))
                .SetMessageHandler<ForTeleportingData>(OnTeleport)
                .SetMessageHandler<TeleportMsg>(OnServerReceiveTeleportMsg)
            ;
        }

        private void OnServerReceiveTeleportMsg(IServerPlayer fromPlayer, TeleportMsg msg)
        {
            if (!msg.DoRemove)
            {
                if (Teleports.ContainsKey(msg.Pos))
                {
                    Teleports[msg.Pos] = msg.Data;
                }
                else
                {
                    Teleports.Add(msg.Pos, msg.Data);
                }
            }
            else
            {
                if (Teleports.ContainsKey(msg.Pos))
                {
                    Teleports.Remove(msg.Pos);
                }
            }

            if (!msg.Synced)
            {
                serverChannel.BroadcastPacket(new TeleportMsg(
                    msg.Pos,
                    msg.Data,
                    msg.DoRemove,
                    true
                ));
            }
        }

        private void PushTeleports(IServerPlayer byPlayer)
        {
            foreach (var tp in Teleports)
            {
                serverChannel.SendPacket(new TeleportMsg(
                    tp.Key,
                    tp.Value
                ), byPlayer);
            }
        }

        private void OnSaveGame()
        {
            sapi?.WorldManager.SaveGame.StoreData("TPNetData", SerializerUtil.Serialize(Teleports));
        }

        private void OnLoadGame()
        {
            try
            {
                sapi.World.Logger.ModNotification("Start loading data");
                byte[] data = sapi.WorldManager.SaveGame.GetData("TPNetData");
                sapi.WorldManager.SaveGame.StoreData("test", new byte[] { 0x1 });

                if (data != null) Teleports = SerializerUtil.Deserialize<Dictionary<BlockPos, TeleportData>>(data);

                foreach (var tp in Teleports)
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


            string name = Teleports[data.TargetPos.AsBlockPos]?.Name;
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

        #region common

        private static Dictionary<BlockPos, TeleportData> Teleports = new Dictionary<BlockPos, TeleportData>();

        private static ICoreAPI api => (sapi as ICoreAPI) ?? (capi as ICoreAPI);

        public override void Start(ICoreAPI api)
        {
            base.Start(api);

            defNames = api.Assets.Get(new AssetLocation(ConstantsCore.ModId, "config/names.json"))?.ToObject<List<string>>();
            if (defNames == null) defNames = new List<string>(new string[] { "null" });
        }

        internal static void AddAvailableTeleport(IPlayer byPlayer, BlockPos pos)
        {
            if (byPlayer == null) throw new ArgumentNullException("byPlayer");
            if (pos == null) throw new ArgumentNullException("pos");

            if (byPlayer?.Entity == null) return;

            if (Teleports.ContainsKey(pos))
            {
                if (!Teleports[pos].ActivatedBy.Contains(byPlayer.PlayerUID))
                {
                    Teleports[pos].ActivatedBy.Add(byPlayer.PlayerUID);
                }

                if (sapi != null)
                {
                    serverChannel.BroadcastPacket(new TeleportMsg(
                        pos,
                        Teleports[pos]
                    ));
                }
                else if (capi != null)
                {
                    clientChannel.SendPacket(new TeleportMsg(
                        pos,
                        Teleports[pos]
                    ));
                }
            }
        }

        internal static void AddTeleport(BlockPos pos, TeleportData data)
        {
            if (!Teleports.ContainsKey(pos))
            {
                Teleports.Add(pos, data);

                if (sapi != null)
                {
                    serverChannel.BroadcastPacket(new TeleportMsg(
                        pos,
                        data
                    ));
                }
                else if (capi != null)
                {
                    clientChannel.SendPacket(new TeleportMsg(
                        pos,
                        data
                    ));
                }

                string type = data.Available ? "normal" : "broken";
                api.World.Logger.ModNotification($"Added teleport {data.Name} ({type}) at {pos} to teleports list");
            }
        }

        internal static void SetTeleport(BlockPos pos, TeleportData data)
        {
            if (!Teleports.ContainsKey(pos))
            {
                AddTeleport(pos, data);
                return;
            }

            Teleports[pos] = data;

            if (sapi != null)
            {
                serverChannel.BroadcastPacket(new TeleportMsg(
                    pos,
                    data
                ));
            }
            else if (capi != null)
            {
                clientChannel.SendPacket(new TeleportMsg(
                    pos,
                    data
                ));
            }

            string type = data.Available ? "normal" : "broken";
            api.World.Logger.ModNotification($"Modified teleport {data.Name} ({type}) at {pos}");
        }

        internal static void RemoveTeleport(BlockPos pos)
        {
            if (Teleports.ContainsKey(pos))
            {
                string type = Teleports[pos].Available ? "normal" : "broken";
                string name = Teleports[pos].Name;

                if (sapi != null)
                {
                    serverChannel.BroadcastPacket(new TeleportMsg(
                        pos,
                        Teleports[pos],
                        true
                    ));
                }
                else if (capi != null)
                {
                    clientChannel.SendPacket(new TeleportMsg(
                        pos,
                        Teleports[pos],
                        true
                    ));
                }

                Teleports.Remove(pos);

                api.World.Logger.ModNotification($"Removed teleport {name} ({type}) at {pos} from teleports list");
            }
        }

        internal static TeleportData GetTeleport(BlockPos pos)
        {
            return Teleports.ContainsKey(pos) ? Teleports[pos] : null;
        }

        internal static void TryCreateData(BlockPos pos, bool available = false)
        {
            if (pos == null) throw new ArgumentNullException();
            if (Teleports.ContainsKey(pos)) return;

            TeleportData data = new TeleportData()
            {
                Available = available,
                Name = defNames.ElementAt(api.World.Rand.Next(defNames.Count))
            };

            AddTeleport(pos.Copy(), data);
        }

        internal static Dictionary<BlockPos, TeleportData> GetAvailableTeleports(IPlayer forPlayer)
        {
            if (forPlayer.WorldData.CurrentGameMode == EnumGameMode.Creative) return Teleports;

            if (Config.Current.SharedTeleports.Val) return Teleports
                ?.Where((dict) => dict.Value.Available)
                ?.ToDictionary(dict => dict.Key, dict => dict.Value)
            ;

            return Teleports
                ?.Where((dict) =>
                    dict.Value.Available &&
                    dict.Value.ActivatedBy.Contains(forPlayer.PlayerUID)
                )?.ToDictionary(dict => dict.Key, dict => dict.Value)
            ;
        }

        #endregion

        #region client

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
                if (Teleports.ContainsKey(msg.Pos))
                {
                    Teleports[msg.Pos] = msg.Data;
                }
                else
                {
                    Teleports.Add(msg.Pos, msg.Data);
                }
            }
            else
            {
                if (Teleports.ContainsKey(msg.Pos))
                {
                    Teleports.Remove(msg.Pos);
                }
            }

            if (!msg.Synced)
            {
                clientChannel.SendPacket(new TeleportMsg(
                    msg.Pos,
                    msg.Data,
                    msg.DoRemove
                ));
            }
        }

        #endregion

        public override void Dispose()
        {
            base.Dispose();

            OnSaveGame();

            capi = null;
            sapi = null;
        }
    }
}
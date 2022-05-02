using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace TeleportationNetwork
{
    public class TeleportNetworkServer : TeleportNetwork, ITeleportNetworkServer
    {
        ICoreServerAPI ServerApi => Api as ICoreServerAPI;
        IServerNetworkChannel ServerChannel => Channel as IServerNetworkChannel;

        public override void Init(ICoreAPI api, ITeleportManager manager)
        {
            base.Init(api, manager);

            ServerApi.Event.PlayerJoin += PushTeleports;
            ServerApi.Event.SaveGameLoaded += OnLoadGame;
            ServerApi.Event.GameWorldSave += OnSaveGame;

            ServerChannel
                .RegisterMessageType(typeof(TeleportingData))
                .RegisterMessageType(typeof(SyncTeleportMessage))
                .SetMessageHandler<TeleportingData>(OnTeleport)
                .SetMessageHandler<SyncTeleportMessage>(OnReceiveSyncPacket);
        }


        private void PushTeleports(IServerPlayer byPlayer)
        {
            foreach (var teleport in Manager.GetAllTeleports())
            {
                ServerChannel.SendPacket(new SyncTeleportMessage
                {
                    Teleport = teleport as Teleport,
                    DoRemove = false
                }, byPlayer);
            }
        }

        private void OnSaveGame()
        {
            var list = Manager.GetAllTeleports().Select(t => t as Teleport).ToList();
            ServerApi.WorldManager.SaveGame.StoreData("TPNetData", SerializerUtil.Serialize(list));
        }

        private void OnLoadGame()
        {
            try
            {
                Core.ModLogger.Event("Start loading data");
                byte[] data = ServerApi.WorldManager.SaveGame.GetData("TPNetData");

                var list = new List<ITeleport>();

                if (data != null) list = SerializerUtil
                        .Deserialize<List<Teleport>>(data)
                        .Select(t => t as ITeleport)
                        .ToList();

                foreach (var teleport in list)
                {
                    Core.ModLogger.Debug($"Loaded teleport data for {teleport.Name} at {teleport.Pos}");
                }

                Core.ModLogger.Event("Data loaded");
            }
            catch (Exception e)
            {
                Core.ModLogger.Error($"Failed loading data: {e}");
            }
        }

        private void OnTeleport(IServerPlayer fromPlayer, TeleportingData data)
        {
            Entity[] tpEntities = null;
            Vec3d currCenterPos = null;

            if (data.SourcePos == null)
            {
                if (fromPlayer.Entity != null)
                {
                    tpEntities = new Entity[] { fromPlayer.Entity };
                    currCenterPos = fromPlayer.Entity.Pos.XYZ;
                }
            }
            else
            {
                if (ServerApi.World.BlockAccessor.GetBlockEntity(data.SourcePos.AsBlockPos) is BETeleport bet)
                {
                    tpEntities = bet.GetInCircleEntities();
                    currCenterPos = bet.Pos.ToVec3d().Add(0.5, 1, 0.5);
                }
            }

            if (tpEntities == null || data.TargetPos == null) return;


            var systemTemporalStability = ServerApi.ModLoader.GetModSystem<SystemTemporalStability>();
            bool stabilityEnabled = ServerApi.World.Config.GetBool("temporalStability", true);

            string name = Manager.GetTeleport(data.TargetPos.AsBlockPos)?.Name;

            foreach (var entity in tpEntities)
            {
                double x = data.TargetPos.X + (entity.Pos.X - currCenterPos.X) + 0.5;
                double y = data.TargetPos.Y + (entity.Pos.Y - currCenterPos.Y) + 2;
                double z = data.TargetPos.Z + (entity.Pos.Z - currCenterPos.Z) + 0.5;

                if (entity is EntityPlayer entityPlayer)
                {
                    entityPlayer.SetActivityRunning(Core.ModId + "_teleportCooldown", Config.Current.TeleportCooldown);

                    bool unstableTeleport = Config.Current.StabilityTeleportMode.Val == "always";

                    if (stabilityEnabled)
                    {
                        double currStability = entityPlayer.WatchedAttributes.GetDouble("temporalStability");
                        double newStability = currStability - Config.Current.StabilityConsumable.Val;

                        if (newStability < 0 || systemTemporalStability.StormData.nowStormActive)
                        {
                            entityPlayer.WatchedAttributes.SetDouble("temporalStability", Math.Max(0, newStability));
                            unstableTeleport = true;
                        }
                        else if (0 < newStability && newStability < currStability)
                        {
                            entityPlayer.WatchedAttributes.SetDouble("temporalStability", newStability);
                        }
                    }

                    if (Config.Current.StabilityTeleportMode.Val != "off" && unstableTeleport)
                    {
                        Commands.RandomTeleport(fromPlayer, Config.Current.UnstableTeleportRange.Val, new Vec3i((int)x, (int)y, (int)z));
                    }
                    else
                    {
                        entity.TeleportToDouble(x, y, z);
                    }
                }
                else
                {
                    entity.TeleportToDouble(x, y, z);
                }

                Core.ModLogger.Notification($"{entity?.GetName()} teleported to {x}, {y}, {z} ({name})");
            }
        }

        private void OnReceiveSyncPacket(IServerPlayer fromPlayer, SyncTeleportMessage message)
        {
            if (message.Teleport != null)
            {
                if (message.DoRemove)
                {
                    Manager.RemoveTeleport(message.Teleport);
                }
                else
                {
                    Manager.SetTeleport(message.Teleport);
                }

                ServerChannel.BroadcastPacket(message, fromPlayer);
            }
        }

        protected override void OnTeleportAdded(ITeleport teleport)
        {
            ServerChannel.BroadcastPacket(new SyncTeleportMessage
            {
                Teleport = teleport as Teleport,
                DoRemove = false
            });

            base.OnTeleportAdded(teleport);
        }

        protected override void OnTeleportModified(ITeleport teleport)
        {
            ServerChannel.BroadcastPacket(new SyncTeleportMessage
            {
                Teleport = teleport as Teleport,
                DoRemove = false
            });

            base.OnTeleportModified(teleport);
        }

        protected override void OnTeleportRemoved(ITeleport teleport)
        {
            ServerChannel.BroadcastPacket(new SyncTeleportMessage
            {
                Teleport = teleport as Teleport,
                DoRemove = true
            });

            base.OnTeleportRemoved(teleport);
        }

        protected override void OnTeleportActivatedByPlayer(ITeleport teleport, IPlayer player)
        {
            ServerChannel.BroadcastPacket(new SyncTeleportMessage
            {
                Teleport = teleport as Teleport,
                DoRemove = false
            });

            base.OnTeleportActivatedByPlayer(teleport, player);
        }
    }

}
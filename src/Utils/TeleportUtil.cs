using System;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace TeleportationNetwork
{
    public static class TeleportUtil
    {
        // Constants.SealRadius, add .5 1 .5
        public static void AreaTeleportTo(IServerPlayer player, Vec3d startPoint, BlockPos targetPoint, float radius)
        {
            var sapi = (ICoreServerAPI)player.Entity.Api;
            var manager = sapi.ModLoader.GetModSystem<TeleportManager>();
            string? targetName = manager.Points[targetPoint]?.Name;

            var entities = MathUtil.GetInCircleEntities(player.Entity.Api, radius, startPoint);
            foreach (var entity in entities)
            {
                Vec3d point = targetPoint.ToVec3d();
                point += entity.Pos.XYZ - startPoint;
                point += new Vec3d(0.5, 2, 0.5);

                if (entity is EntityPlayer entityPlayer)
                {
                    StabilityRelatedTeleportTo(entityPlayer, point);
                }
                else
                {
                    entity.TeleportTo(point);
                }

                Core.ModLogger.Notification($"{entity?.GetName()} teleported to {point} ({targetName})");
            }
        }

        public static void AreaTeleportTo(IServerPlayer player, BlockPos targetPoint, float radius)
        {
            AreaTeleportTo(player, player.Entity.Pos.XYZ, targetPoint, radius);
        }

        public static void StabilityRelatedTeleportTo(this EntityPlayer entity, Vec3d pos)
        {
            if (entity.Api is not ICoreServerAPI sapi)
            {
                return;
            }

            var stabilitySystem = sapi.ModLoader.GetModSystem<SystemTemporalStability>();
            bool stabilityEnabled = sapi.World.Config.GetBool("temporalStability", true);
            entity.SetActivityRunning(Core.ModId + "_teleportCooldown", Core.Config.TeleportCooldown);

            bool unstableTeleport = false;

            if (stabilityEnabled && Core.Config.StabilityTeleportMode != "off")
            {
                if (Core.Config.StabilityTeleportMode == "always")
                {
                    unstableTeleport = true;
                }
                else
                {
                    double currStability = entity.WatchedAttributes.GetDouble("temporalStability");
                    double nextStability = currStability - Core.Config.StabilityConsumable;

                    if (nextStability < 0 || stabilitySystem.StormData.nowStormActive)
                    {
                        entity.WatchedAttributes.SetDouble("temporalStability", Math.Max(0, nextStability));
                        unstableTeleport = true;
                    }
                    else if (0 < nextStability && nextStability < currStability)
                    {
                        entity.WatchedAttributes.SetDouble("temporalStability", nextStability);
                    }
                }
            }

            if (Core.Config.StabilityTeleportMode != "off" && unstableTeleport)
            {
                RandomTeleport((IServerPlayer)entity.Player, Core.Config.UnstableTeleportRange, pos.AsBlockPos.ToVec3i());
            }
            else
            {
                entity.TeleportTo(pos);
            }
        }

        public static void RandomTeleport(IServerPlayer player, int range = -1, Vec3i? pos = null)
        {
            try
            {
                var sapi = (ICoreServerAPI)player.Entity.Api;

                int x, z;
                if (range != -1)
                {
                    if (pos == null) pos = player.Entity.Pos.XYZInt;

                    x = sapi.World.Rand.Next(range * 2) - range + pos.X;
                    z = sapi.World.Rand.Next(range * 2) - range + pos.Z;
                }
                else
                {
                    x = sapi.World.Rand.Next(sapi.WorldManager.MapSizeX);
                    z = sapi.World.Rand.Next(sapi.WorldManager.MapSizeZ);
                }

                int chunkSize = sapi.WorldManager.ChunkSize;
                player.Entity.TeleportToDouble(x + 0.5f, sapi.WorldManager.MapSizeY + 2, z + 0.5f);
                sapi.WorldManager.LoadChunkColumnPriority(x / chunkSize, z / chunkSize, new ChunkLoadOptions()
                {
                    OnLoaded = () =>
                    {
                        int y = (int)sapi.WorldManager.GetSurfacePosY(x, z)!;
                        player.Entity.TeleportToDouble(x + 0.5f, y + 2, z + 0.5f);
                    }
                });
            }
            catch (Exception e)
            {
                Core.ModLogger.Error("Failed to teleport player to random location.");
                Core.ModLogger.Error(e.Message);
            }
        }
    }
}

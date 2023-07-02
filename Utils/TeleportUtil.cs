using System;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace TeleportationNetwork
{
    public static class TeleportUtil
    {
        public static void AreaTeleportToPoint(IServerPlayer player, Vec3d startPoint, BlockPos targetPoint,
            float radius, ILogger logger, Action<Entity>? onTeleported = null)
        {
            var sapi = (ICoreServerAPI)player.Entity.Api;
            var manager = sapi.ModLoader.GetModSystem<TeleportManager>();
            string? targetName = manager.Points[targetPoint]?.Name;

            var entities = MathUtil.GetInCircleEntities(player.Entity.Api, radius, startPoint);
            foreach (var entity in entities)
            {
                Vec3d point = targetPoint.ToVec3d();
                point += entity.Pos.XYZ - startPoint;
                point += new Vec3d(0.5, 1.5, 0.5);

                if (entity is EntityPlayer entityPlayer)
                {
                    StabilityRelatedTeleportTo(entityPlayer, point, logger, () => onTeleported?.Invoke(entity));
                }
                else
                {
                    entity.TeleportTo(point, () => onTeleported?.Invoke(entity));
                }

                logger.Notification($"{entity?.GetName()} teleported to {point} ({targetName})");
            }
        }

        public static void StabilityRelatedTeleportTo(this EntityPlayer entity, Vec3d pos,
            ILogger logger, Action? onTeleported = null)
        {
            if (entity.Api is not ICoreServerAPI sapi)
            {
                return;
            }

            var stabilitySystem = sapi.ModLoader.GetModSystem<SystemTemporalStability>();
            bool stabilityEnabled = sapi.World.Config.GetBool("temporalStability", true);
            entity.SetActivityRunning($"{Constants.ModId}_teleportCooldown", Core.Config.TeleportCooldown);

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
                CommonLib.Utils.TeleportUtil.RandomTeleport((IServerPlayer)entity.Player, new()
                {
                    Range = Core.Config.UnstableTeleportRange,
                    CenterPos = pos.AsBlockPos.ToVec3i()
                }, logger);
            }
            else
            {
                entity.TeleportTo(pos, onTeleported);
            }
        }

        public static void TeleportTo(this Entity entity, Vec3d pos, Action? onTeleported = null)
        {
            var entityPos = entity.Pos.Copy();
            entityPos.SetPos(pos);
            entity.TeleportTo(entityPos, onTeleported);
        }
    }
}

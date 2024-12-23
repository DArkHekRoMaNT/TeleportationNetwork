using System;
using System.Diagnostics;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace TeleportationNetwork
{
    public static class TeleportUtil
    {
        public static float GetThick(this Teleport teleport)
        {
            return 0.25f;
        }

        public static Vec3d GetCenter(this Teleport teleport)
        {
            if (teleport.Orientation == null || teleport.Size == 0)
            {
                Debug.WriteLine($"{teleport.OrientationIndex} {teleport.Size}");
                Debugger.Break();
                return teleport.Pos.ToVec3d().Add(0.5);
            }

            var thick = teleport.GetThick();
            var dir = teleport.Orientation.Normalf;
            return teleport.Pos.ToVec3d().Add(0.5).Add(dir * thick);
        }

        public static Vec3d GetTeleportPoint(this Teleport target)
        {
            var targetPos = GetCenter(target);
            targetPos -= target.Orientation.Normald.Clone().Mul(0.5); // Forward offset
            return targetPos;
        }

        public static Vec3d GetTeleportPoint(this Teleport from, Entity entity, Teleport to)
        {
            if (from.Orientation == null || to.Orientation == null ||  from.Size == 0 || to.Size == 0)
                return GetTeleportPoint(to);

            var fromCenterPos = GetCenter(from);
            var entityRelativePos = fromCenterPos - entity.Pos.XYZ;

            var axis = new Vec3d();
            axis.Cross(to.Orientation.Normald, from.Orientation.Normald);

            var targetPos = GetCenter(to);
            
            targetPos -= to.Orientation.Normalf.ToVec3d().Mul(0.5); // Forward offset
            return targetPos;
        }

        public static Entity[] GetEntityInPortal(this Teleport teleport, ICoreAPI api)
        {
            return MathUtil.GetInCyllinderEntities(api, teleport.Size, GetThick(teleport), GetCenter(teleport), teleport.Orientation);
        }

        public static void StabilityRelatedTeleportTo(this Entity entity, Vec3d pos, ILogger logger, Action? onTeleported = null)
        {
            var entityPos = entity.Pos.Copy();
            entityPos.SetPos(pos);
            StabilityRelatedTeleportTo(entity, entityPos, logger, onTeleported);
        }

        public static void StabilityRelatedTeleportTo(this Entity entity, EntityPos pos, ILogger logger, Action? onTeleported = null)
        {
            if (entity.Api is not ICoreServerAPI sapi)
            {
                return;
            }

            var stabilitySystem = sapi.ModLoader.GetModSystem<SystemTemporalStability>();
            bool stabilityEnabled = sapi.World.Config.GetBool("temporalStability", true);
            entity.SetActivityRunning(Constants.TeleportCooldownActivityName, Core.Config.TeleportCooldown);

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

            if (Core.Config.StabilityTeleportMode != "off" && unstableTeleport && entity is EntityPlayer entityPlayer)
            {
                CommonLib.Utils.TeleportUtil.RandomTeleport((IServerPlayer)entityPlayer.Player, new()
                {
                    Range = Core.Config.UnstableTeleportRange,
                    CenterPos = pos.AsBlockPos.ToVec3i()
                }, logger);
            }
            else
            {
                entity.TeleportTo(pos, () =>
                {
                    var pos = entity.Pos.AsBlockPos;
                    for (; pos.Y < sapi.WorldManager.MapSizeY; pos.Y++)
                    {
                        var solidBlock = sapi.World.BlockAccessor.GetMostSolidBlock(pos);
                        if (!solidBlock.SideSolid.Any)
                        {
                            var entityPos = entity.SidedPos.Copy();
                            entityPos.Y = pos.Y;
                            entity.TeleportTo(entityPos, onTeleported);
                            break;
                        }
                    }
                    onTeleported?.Invoke();
                });
            }
        }
    }
}

using System;
using System.Diagnostics;
using System.Threading;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace TeleportationNetwork
{
    public static class TeleportUtil
    {

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
                            return;
                        }
                    }
                    onTeleported?.Invoke();
                });
            }
        }

        public static Vec3d GetTargetPos(this Teleport target)
        {
            if (target.OrientationIndex == -1 || target.Size == 0)
            {
                Debug.WriteLine($"{target.OrientationIndex} {target.Size}");
                return GetDefaultGateCenter(target);
            }

            var targetPos = GetGateCenter(target);
            targetPos -= BlockFacing.ALLNORMALI[target.OrientationIndex].AsBlockPos.ToVec3d().Mul(1f); // Forward offset
            return targetPos;
        }

        public static Vec3d GetGateCenter(this Teleport teleport)
        {
            if (teleport.OrientationIndex == -1 || teleport.Size == 0)
            {
                return GetDefaultGateCenter(teleport);
            }
            return GetGateCenter(teleport.Pos, BlockFacing.ALLFACES[teleport.OrientationIndex], teleport.Size);
        }

        public static Vec3d GetDefaultGateCenter(this Teleport teleport)
        {
            return teleport.Pos.ToVec3d().Add(0.5);
        }

        public static Vec3d GetGateCenter(BlockPos pos, BlockFacing facing, float size)
        {
            return pos.ToVec3d().Add(0.5) - facing.Normalf.ToVec3d().Mul(0.25 * (size / 5f));
        }
    }
}

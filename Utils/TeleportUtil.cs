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

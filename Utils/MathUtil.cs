using System;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;

namespace TeleportationNetwork
{
    public static class MathUtil
    {
        private static Random? _rand;

        public static Vec3d RandomPosOnCircleEdge(float radius, Vec3d center)
        {
            _rand ??= new Random();

            double angle = _rand.NextDouble() * Math.PI * 2;
            return new Vec3d(
                center.X + Math.Cos(angle) * (radius - 1 / 16),
                center.Y,
                center.Z + Math.Sin(angle) * (radius - 1 / 16));
        }

        public static Entity[] GetInCyllinderEntities(ICoreAPI api, float radius, float thick, Vec3d center, BlockFacing orientation)
        {
            if (orientation.IsVertical)
            {
                return api.World.GetEntitiesAround(center, radius, thick, (e) => e.Pos.HorDistanceTo(center) < radius);
            }

            return api.World.GetEntitiesAround(center, radius, radius, (e) =>
            {
                var dx = e.Pos.X - center.X;
                var dy = e.Pos.Y - center.Y;
                var dz = e.Pos.Z - center.Z;
                if (orientation.IsAxisNS)
                {
                    return Math.Abs(dz) < thick && Math.Sqrt(dy * dy + dx * dx) < radius;
                }
                else
                {
                    return Math.Abs(dx) < thick && Math.Sqrt(dy * dy + dz * dz) < radius;
                }
            });
        }

        public static Entity[] GetInSphereEntities(ICoreAPI api, float radius, Vec3d center)
        {
            return api.World.GetEntitiesAround(center, radius, radius, (e) => e.Pos.DistanceTo(center) < radius);
        }
    }
}

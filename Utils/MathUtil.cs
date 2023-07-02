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

        public static Entity[] GetInCircleEntities(ICoreAPI api, float radius, Vec3d center)
        {
            return api.World.GetEntitiesAround(center, radius, radius, (e) => e.Pos.DistanceTo(center) < radius);
        }
    }
}

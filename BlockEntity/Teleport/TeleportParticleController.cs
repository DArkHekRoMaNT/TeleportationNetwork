using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;

namespace TeleportationNetwork
{
    public class TeleportParticleController
    {
        private readonly ICoreClientAPI _api;
        private readonly SimpleParticleProperties _circleParticles;
        private readonly SimpleParticleProperties _teleportParticles;
        private readonly Item _temporalGear;

        public TeleportParticleController(ICoreClientAPI api)
        {
            _api = api;

            _circleParticles = new SimpleParticleProperties
            {
                MinQuantity = 1f,
                AddQuantity = 0f,
                MinPos = new Vec3d(),
                AddPos = new Vec3d(),
                MinVelocity = new Vec3f(),
                AddVelocity = new Vec3f(),
                LifeLength = 0.5f,
                GravityEffect = -0.05f,
                MinSize = 0.1f,
                MaxSize = 0.2f,
                ParticleModel = EnumParticleModel.Quad,
                SelfPropelled = true,
                SizeEvolve = EvolvingNatFloat.create(EnumTransformFunction.LINEARNULLIFY, -0.1f),
                VertexFlags = 20 & VertexFlags.GlowLevelBitMask
            };

            _teleportParticles = new SimpleParticleProperties
            {
                MinQuantity = 15,
                AddQuantity = 15,
                MinPos = new Vec3d(),
                AddPos = new Vec3d(),
                MinVelocity = new Vec3f(-1f, -1f, -1f),
                AddVelocity = new Vec3f(2, 2, 2),
                LifeLength = 0.5f,
                GravityEffect = 0,
                MinSize = 0.2f,
                MaxSize = 0.4f,
                ParticleModel = EnumParticleModel.Quad,
                RandomVelocityChange = true,
                VertexFlags = 20 & VertexFlags.GlowLevelBitMask,
                SizeEvolve = EvolvingNatFloat.create(EnumTransformFunction.LINEARNULLIFY, -0.5f)
            };

            _temporalGear = api.World.GetItem(new AssetLocation("gear-temporal"));
        }

        public void SpawnTeleportParticles(Entity entity)
        {
            float width = entity.CollisionBox.Width;
            _teleportParticles.AddPos.Set(width, entity.CollisionBox.Height, width);
            _teleportParticles.MinPos.Set(entity.SidedPos).Add(-width / 2, 0, -width / 2);
            _teleportParticles.Color = GetRandomColor();
            _api.World.SpawnParticles(_teleportParticles);
        }

        public int GetRandomColor()
        {
            return _temporalGear.GetRandomColor(_api, null);
        }
    }
}

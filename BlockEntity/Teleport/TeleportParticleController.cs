using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;

namespace TeleportationNetwork
{
    public class TeleportParticleController
    {
        private readonly ICoreClientAPI _api;
        private readonly SimpleParticleProperties _gateParticles;
        private readonly SimpleParticleProperties _entityTeleportedParticles;
        private readonly Item _temporalGear;

        public TeleportParticleController(ICoreClientAPI api)
        {
            _api = api;

            _gateParticles = new SimpleParticleProperties
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

            _entityTeleportedParticles = new SimpleParticleProperties
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
            _entityTeleportedParticles.AddPos.Set(width, entity.CollisionBox.Height, width);
            _entityTeleportedParticles.MinPos.Set(entity.SidedPos).Add(-width / 2, 0, -width / 2);
            _entityTeleportedParticles.Color = GetRandomColor();
            _api.World.SpawnParticles(_entityTeleportedParticles);
        }

        public void SpawnGateParticles(float radius, float thick, Vec3d center, BlockFacing orientation)
        {
            return;
            for (int i = 0; i < 50; i++)
            {
                var gateParticles = new SimpleParticleProperties
                {
                    MinQuantity = 1f,
                    AddQuantity = 0f,
                    MinPos = new Vec3d(),
                    AddPos = new Vec3d(),
                    MinVelocity = new Vec3f(),
                    AddVelocity = new Vec3f(),
                    LifeLength = 0.5f,
                    GravityEffect = 0,
                    MinSize = 0.2f,
                    MaxSize = 0.2f,
                    ParticleModel = EnumParticleModel.Quad
                };
                gateParticles.MinPos.Set(center + MathUtil.GetRandomPosInCyllinder(radius, thick, orientation));
                gateParticles.Color = GetRandomColor();
                _api.World.SpawnParticles(gateParticles);
            }
        }

        public int GetRandomColor()
        {
            return _temporalGear.GetRandomColor(_api, null);
        }
    }
}

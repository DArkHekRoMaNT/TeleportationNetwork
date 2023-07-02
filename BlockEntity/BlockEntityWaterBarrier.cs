using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace TeleportationNetwork
{
    public class BlockEntityWaterBarrier : BlockEntity
    {
        private long _listenerId;
        private SimpleParticleProperties _particles = null!;

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);

            _particles = new SimpleParticleProperties
            {
                MinQuantity = 1f,
                AddQuantity = 0,
                MinPos = Pos.ToVec3d(),
                AddPos = new Vec3d(),
                MinVelocity = new Vec3f(),
                AddVelocity = new Vec3f(),
                LifeLength = 0.367f,
                GravityEffect = 0,
                MinSize = .3f,
                MaxSize = .3f,
                ParticleModel = EnumParticleModel.Cube,
                ShouldDieInLiquid = false,
                VertexFlags = 200 & VertexFlags.GlowLevelBitMask,
                Color = ColorUtil.WhiteArgb,
                RandomVelocityChange = false,
                WindAffected = false,
                Async = true
            };

            _listenerId = RegisterGameTickListener(OnGameTick, 1000, Api.World.Rand.Next(1000));
        }

        private void OnGameTick(float dt)
        {
            _particles.MinPos = Pos.ToVec3d().Add(1, 1, 1);
            _particles.MinVelocity = new Vec3f(-1, 0, 0);
            Api.World.SpawnParticles(_particles);

            _particles.MinPos = Pos.ToVec3d().Add(1, 1, 1);
            _particles.MinVelocity = new Vec3f(0, -1, 0);
            Api.World.SpawnParticles(_particles);

            _particles.MinPos = Pos.ToVec3d().Add(1, 1, 1);
            _particles.MinVelocity = new Vec3f(0, 0, -1);
            Api.World.SpawnParticles(_particles);


            _particles.MinPos = Pos.ToVec3d().Add(0, 1, 0);
            _particles.MinVelocity = new Vec3f(1, 0, 0);
            Api.World.SpawnParticles(_particles);

            _particles.MinPos = Pos.ToVec3d().Add(0, 1, 0);
            _particles.MinVelocity = new Vec3f(0, -1, 0);
            Api.World.SpawnParticles(_particles);

            _particles.MinPos = Pos.ToVec3d().Add(0, 1, 0);
            _particles.MinVelocity = new Vec3f(0, 0, 1);
            Api.World.SpawnParticles(_particles);


            _particles.MinPos = Pos.ToVec3d().Add(0, 0, 1);
            _particles.MinVelocity = new Vec3f(1, 0, 0);
            Api.World.SpawnParticles(_particles);

            _particles.MinPos = Pos.ToVec3d().Add(0, 0, 1);
            _particles.MinVelocity = new Vec3f(0, 1, 0);
            Api.World.SpawnParticles(_particles);

            _particles.MinPos = Pos.ToVec3d().Add(0, 0, 1);
            _particles.MinVelocity = new Vec3f(0, 0, -1);
            Api.World.SpawnParticles(_particles);


            _particles.MinPos = Pos.ToVec3d().Add(1, 0, 0);
            _particles.MinVelocity = new Vec3f(-1, 0, 0);
            Api.World.SpawnParticles(_particles);

            _particles.MinPos = Pos.ToVec3d().Add(1, 0, 0);
            _particles.MinVelocity = new Vec3f(0, 1, 0);
            Api.World.SpawnParticles(_particles);

            _particles.MinPos = Pos.ToVec3d().Add(1, 0, 0);
            _particles.MinVelocity = new Vec3f(0, 0, 1);
            Api.World.SpawnParticles(_particles);
        }

        public override void OnBlockUnloaded()
        {
            base.OnBlockUnloaded();
            UnregisterGameTickListener(_listenerId);
        }
    }
}

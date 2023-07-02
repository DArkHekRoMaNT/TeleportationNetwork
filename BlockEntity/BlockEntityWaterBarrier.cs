using System;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace TeleportationNetwork
{
    public class BlockEntityWaterBarrier : BlockEntity
    {
        private long _listenerId;

        private SimpleParticleProperties particles;

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);
            _listenerId = RegisterGameTickListener(OnGameTick, 1000, Api.World.Rand.Next(1000));
            _listenerId = RegisterGameTickListener(OnGameTick, 1000, Api.World.Rand.Next(1000));
            _listenerId = RegisterGameTickListener(OnGameTick, 1000, Api.World.Rand.Next(1000));
            _listenerId = RegisterGameTickListener(OnGameTick, 1000, Api.World.Rand.Next(1000));
            _listenerId = RegisterGameTickListener(OnGameTick, 1000, Api.World.Rand.Next(1000));
            _listenerId = RegisterGameTickListener(OnGameTick, 1000, Api.World.Rand.Next(1000));
            _listenerId = RegisterGameTickListener(OnGameTick, 1000, Api.World.Rand.Next(1000));
            _listenerId = RegisterGameTickListener(OnGameTick, 1000, Api.World.Rand.Next(1000));
            _listenerId = RegisterGameTickListener(OnGameTick, 1000, Api.World.Rand.Next(1000));
            _listenerId = RegisterGameTickListener(OnGameTick, 1000, Api.World.Rand.Next(1000));
            _listenerId = RegisterGameTickListener(OnGameTick, 1000, Api.World.Rand.Next(1000));
            _listenerId = RegisterGameTickListener(OnGameTick, 1000, Api.World.Rand.Next(1000));
            _listenerId = RegisterGameTickListener(OnGameTick, 1000, Api.World.Rand.Next(1000));

            particles = new SimpleParticleProperties()
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
        }

        private void OnGameTick(float dt)
        {
            return;

            particles.MinPos = Pos.ToVec3d().Add(1, 1, 1);
            particles.MinVelocity = new Vec3f(-1, 0, 0);
            Api.World.SpawnParticles(particles);

            particles.MinPos = Pos.ToVec3d().Add(1, 1, 1);
            particles.MinVelocity = new Vec3f(0, -1, 0);
            Api.World.SpawnParticles(particles);

            particles.MinPos = Pos.ToVec3d().Add(1, 1, 1);
            particles.MinVelocity = new Vec3f(0, 0, -1);
            Api.World.SpawnParticles(particles);


            particles.MinPos = Pos.ToVec3d().Add(0, 1, 0);
            particles.MinVelocity = new Vec3f(1, 0, 0);
            Api.World.SpawnParticles(particles);

            particles.MinPos = Pos.ToVec3d().Add(0, 1, 0);
            particles.MinVelocity = new Vec3f(0, -1, 0);
            Api.World.SpawnParticles(particles);

            particles.MinPos = Pos.ToVec3d().Add(0, 1, 0);
            particles.MinVelocity = new Vec3f(0, 0, 1);
            Api.World.SpawnParticles(particles);


            particles.MinPos = Pos.ToVec3d().Add(0, 0, 1);
            particles.MinVelocity = new Vec3f(1, 0, 0);
            Api.World.SpawnParticles(particles);

            particles.MinPos = Pos.ToVec3d().Add(0, 0, 1);
            particles.MinVelocity = new Vec3f(0, 1, 0);
            Api.World.SpawnParticles(particles);

            particles.MinPos = Pos.ToVec3d().Add(0, 0, 1);
            particles.MinVelocity = new Vec3f(0, 0, -1);
            Api.World.SpawnParticles(particles);


            particles.MinPos = Pos.ToVec3d().Add(1, 0, 0);
            particles.MinVelocity = new Vec3f(-1, 0, 0);
            Api.World.SpawnParticles(particles);

            particles.MinPos = Pos.ToVec3d().Add(1, 0, 0);
            particles.MinVelocity = new Vec3f(0, 1, 0);
            Api.World.SpawnParticles(particles);

            particles.MinPos = Pos.ToVec3d().Add(1, 0, 0);
            particles.MinVelocity = new Vec3f(0, 0, 1);
            Api.World.SpawnParticles(particles);
        }

        public override void OnBlockUnloaded()
        {
            base.OnBlockUnloaded();
            UnregisterGameTickListener(_listenerId);
        }
    }
}

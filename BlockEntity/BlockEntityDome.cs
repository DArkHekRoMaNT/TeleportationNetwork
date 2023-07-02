using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace TeleportationNetwork
{
    public class BlockEntityDome : BlockEntity
    {
        private long _listenerId;
        private SimpleParticleProperties particles;
        private int radius => 3;

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);
            _listenerId = RegisterGameTickListener(OnGameTick, 1000);

            OnLongGameTick(0);
        }

        private void OnLongGameTick(float dt)
        {
            Api.World.BlockAccessor.SearchFluidBlocks(
                Pos.AddCopy(-radius, 0, -radius),
                Pos.AddCopy(radius, radius * 2, radius),
                (block, pos) =>
                {
                    Api.World.BlockAccessor.SetBlock(0, pos, BlockLayersAccess.Fluid);
                    return true;
                });
        }

        private void OnGameTick(float dt)
        {
            float speed = 0.1f;
            particles = new SimpleParticleProperties
            {
                MinQuantity = 10,
                MinPos = Pos.ToVec3d().AddCopy(-radius, 0, -radius),
                AddPos = new Vec3d(2 * radius, 2 * radius, 2 * radius),
                MinVelocity = new Vec3f(-speed, -speed, -speed),
                AddVelocity = new Vec3f(speed * 2, speed * 2, speed * 2),
                LifeLength = 1,
                GravityEffect = 0,
                MinSize = .01f,
                MaxSize = .1f,
                ParticleModel = EnumParticleModel.Quad,
                ShouldDieInLiquid = false,
                VertexFlags = 200 & VertexFlags.GlowLevelBitMask,
                Color = ColorUtil.WhiteArgb,
                Async = true
            };

            Api.World.SpawnParticles(particles);
        }

        public override void OnBlockUnloaded()
        {
            base.OnBlockUnloaded();
            UnregisterGameTickListener(_listenerId);
        }
    }
}

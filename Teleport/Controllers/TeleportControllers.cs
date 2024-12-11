using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;

namespace TeleportationNetwork
{
    public class TeleportControllers(ICoreClientAPI capi, BlockPos pos) : IDisposable
    {
        public TeleportParticleController Particles { get; } = new(capi);

        private readonly TeleportSoundController _sound = new(capi, pos);
        private readonly TeleportRiftRenderer _riftRenderer = new(capi, pos);
        private readonly TeleportShapeRenderer _shapeRenderer = new(capi, pos);

        public void UpdateTeleport(BlockEntityTeleport be)
        {
            var rotationDeg = (be.Block as BlockTeleport)?.RotationDeg ?? 0;
            _shapeRenderer.UpdateMesh(be.Block, rotationDeg, be.Size);
            _riftRenderer.UpdateTeleport(be.Size, rotationDeg, be.Status.IsBroken);
        }

        public void Update(TeleportStatus status)
        {
            _riftRenderer?.Update(status);
            _shapeRenderer?.Update(status);
            _sound.Update(status);
        }

        public void Dispose()
        {
            _riftRenderer?.Dispose();
            _shapeRenderer?.Dispose();
            _sound.Dispose();
        }
    }
}

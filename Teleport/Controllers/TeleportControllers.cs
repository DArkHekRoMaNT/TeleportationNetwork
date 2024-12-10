using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace TeleportationNetwork
{
    public class TeleportControllers(ICoreClientAPI capi, BlockPos pos, Block block, GateSettings settings) : IDisposable
    {
        public TeleportRiftRenderer RiftRenderer { get; } = new(capi, pos, settings.Rotation);
        public TeleportShapeRenderer ShapeRenderer { get; } = new(capi, pos, block, settings);
        public TeleportSoundController SoundController { get; } = new(capi, pos);

        public void UpdateTeleport(Teleport teleport)
        {
            RiftRenderer.UpdateTeleport(teleport);
        }

        public void Update(float dt, TeleportActivator status)
        {
            RiftRenderer.Update(status);
            ShapeRenderer.Update(status);
            SoundController.Update(dt, status);
        }

        public void Dispose()
        {
            RiftRenderer.Dispose();
            ShapeRenderer.Dispose();
            SoundController.Dispose();
        }

        public void FastForward(TeleportActivator status)
        {
            SoundController.Update(1000, status);
        }
    }
}

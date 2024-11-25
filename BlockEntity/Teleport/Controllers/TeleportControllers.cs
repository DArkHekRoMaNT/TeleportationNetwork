using System;
using Vintagestory.API.Client;
using Vintagestory.API.MathTools;

namespace TeleportationNetwork
{
    public class TeleportControllers(ICoreClientAPI capi, BlockPos pos, GateSettings settings) : IDisposable
    {
        public TeleportRiftRenderer RiftRenderer { get; } = new(capi, pos, settings.Rotation);
        public TeleportShapeRenderer ShapeRenderer { get; } = new(capi, pos, settings);
        public TeleportSoundController SoundController { get; } = new(capi, pos);

        public void UpdateTeleport(Teleport teleport)
        {
            RiftRenderer.UpdateTeleport(teleport);
        }

        public void Update(float dt, TeleportActivator status)
        {
            RiftRenderer.Update(dt, status);
            ShapeRenderer.Update(dt, status);
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

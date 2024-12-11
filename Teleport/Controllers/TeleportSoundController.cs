using Cairo;
using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace TeleportationNetwork
{
    public class TeleportSoundController : IDisposable
    {
        private readonly ILoadedSound _sound;

        public TeleportSoundController(ICoreClientAPI capi, BlockPos pos)
        {
            _sound = capi.World.LoadSound(new SoundParams
            {
                Location = new AssetLocation("sounds/effect/translocate-idle.ogg"), //TODO: Change sound
                ShouldLoop = true,
                Position = pos.ToVec3f().AddCopy(.5f, 1, .5f),
                RelativePosition = false,
                DisposeOnFinish = false,
                Volume = 0
            });
        }

        public void Update(TeleportStatus status)
        {
            if (status.IsBroken && _sound.IsPlaying)
            {
                _sound.Stop();
                return;
            }
            else if (status.IsRepaired && !_sound.IsPlaying)
            {
                _sound.Start();
            }

            if (status.State == TeleportStatus.FSMState.Activating ||
                status.State == TeleportStatus.FSMState.Activated)
            {
                _sound.SetVolume(0.5f + status.Progress);
                _sound.SetPitch(0.5f + status.Progress);
            }
            else
            {
                _sound.SetVolume(0.5f + status.Progress);
                _sound.SetPitch(0.5f + status.Progress);
            }
        }

        public void Dispose()
        {
            _sound.Dispose();
        }
    }
}

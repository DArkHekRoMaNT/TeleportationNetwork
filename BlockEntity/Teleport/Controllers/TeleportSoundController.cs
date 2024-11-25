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

        private float _soundVolume;
        private float _soundPith;

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

        public void Update(float dt, TeleportActivator status)
        {
            if (_sound.IsPlaying == false)
            {
                _sound.Start();
            }

            if (status.State == TeleportActivator.FSMState.Activating ||
                status.State == TeleportActivator.FSMState.Activated)
            {
                _soundVolume = Math.Min(1f, _soundVolume + dt / 3);
                _soundPith = Math.Min(1.5f, _soundPith + dt / 3);
            }
            else
            {
                _soundVolume = Math.Max(0.5f, _soundVolume - dt);
                _soundPith = Math.Max(0.5f, _soundPith - dt);
            }

            _sound.SetVolume(_soundVolume);
            _sound.SetPitch(_soundPith);
        }

        public void Dispose()
        {
            _sound.Dispose();
        }
    }
}

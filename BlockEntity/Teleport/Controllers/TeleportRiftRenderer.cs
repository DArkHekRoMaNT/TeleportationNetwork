using CommonLib.Utils;
using Vintagestory.API.Client;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace TeleportationNetwork
{
    public sealed class TeleportRiftRenderer : IRenderer
    {
        public double RenderOrder => 0.05;
        public int RenderRange => 100;

        private IShaderProgram Prog => _renderSystem.Prog;

        private readonly BlockPos _pos;
        private readonly ICoreClientAPI _api;
        private readonly float _rotation;
        private readonly TeleportRenderSystem _renderSystem;
        private readonly MeshRef _meshref;
        private readonly Matrixf _matrixf;

        private float _counter;
        private float _activationProgress;
        private float _size;
        private bool _broken;

        public TeleportRiftRenderer(ICoreClientAPI api, BlockPos pos, float rotation)
        {
            _pos = pos;
            _api = api;
            _rotation = rotation;

            var mesh = DarkMeshUtil.GetRectangle(1, 100);
            _meshref = _api.Render.UploadMesh(mesh);
            _matrixf = new Matrixf();
            _size = 1;

            _api.Event.RegisterRenderer(this, EnumRenderStage.AfterBlit, $"{Constants.ModId}-teleport-rift");
            _renderSystem = _api.ModLoader.GetModSystem<TeleportRenderSystem>();
        }

        public void UpdateTeleport(Teleport teleport)
        {
            _size = teleport.Size;
            _broken = !teleport.Enabled;
        }

        public void Update(TeleportActivator status)
        {
            _activationProgress = GameMath.Clamp(status.Progress, 0, 1);
        }

        public void OnRenderFrame(float deltaTime, EnumRenderStage stage)
        {
            var camPos = _api.World.Player.Entity.CameraPos;

            float viewDistance = _api.World.Player.WorldData.LastApprovedViewDistance;
            if (_api.IsSinglePlayer)
            {
                viewDistance = _api.World.Player.WorldData.DesiredViewDistance;
            }
            viewDistance *= 0.85f;
            if (_pos.DistanceSqTo(camPos.X, camPos.Y, camPos.Z) > viewDistance * viewDistance)
            {
                return;
            }

            var glichEffectStrength = 0.0f;
            var temporalBehavior = _api.World.Player.Entity.GetBehavior<EntityBehaviorTemporalStabilityAffected>();
            if (temporalBehavior != null)
            {
                glichEffectStrength = (float)temporalBehavior.GlichEffectStrength;
            }

            _counter += deltaTime;
            if (glichEffectStrength > 0.1)
            {
                if (_api.World.Rand.NextDouble() < 0.012)
                {
                    _counter += 20 * (float)_api.World.Rand.NextDouble();
                }
            }

            _api.Render.GLDepthMask(false);
            _api.Render.GlEnableCullFace();

            Prog.Use();
            Prog.Uniform("rgbaFogIn", _api.Render.FogColor);
            Prog.Uniform("fogMinIn", _api.Render.FogMin);
            Prog.Uniform("fogDensityIn", _api.Render.FogDensity);
            Prog.Uniform("rgbaAmbientIn", _api.Render.AmbientColor);
            Prog.Uniform("rgbaLightIn", _api.World.BlockAccessor.GetLightRGBs(_pos));

            Prog.BindTexture2D("primaryFb", _api.Render.FrameBuffers[(int)EnumFrameBuffer.Primary].ColorTextureIds[0], 0);
            Prog.BindTexture2D("depthTex", _api.Render.FrameBuffers[(int)EnumFrameBuffer.Primary].DepthTextureId, 1);
            Prog.UniformMatrix("projectionMatrix", _api.Render.CurrentProjectionMatrix);

            int width = _api.Render.FrameWidth;
            int height = _api.Render.FrameHeight;
            Prog.Uniform("time", _counter);
            Prog.Uniform("invFrameSize", new Vec2f(1f / width, 1f / height));
            Prog.Uniform("glich", GameMath.Min(glichEffectStrength * 2, 1));
            Prog.Uniform("glich", GameMath.Min(glichEffectStrength * 2, 1));
            Prog.Uniform("stage", 0.2f + _activationProgress * 0.8f);
            Prog.Uniform("broken", _broken ? 1 : 0);
            Prog.Uniform("direction", (int)(_rotation / 90));

            _matrixf.Identity();

            float dx = (float)(_pos.X - camPos.X + 0.5f);
            float dy = (float)(_pos.Y - camPos.Y + 0.5f);
            float dz = (float)(_pos.Z - camPos.Z + 0.5f);

            _matrixf.Translate(dx, dy, dz);
            _matrixf.RotateYDeg(_rotation + 180);
            if (_size == 5)
            {
                _matrixf.Translate(0, 0, 0.25);
            }
            _matrixf.Scale(_size, _size, _size);

            Prog.UniformMatrix("modelMatrix", _matrixf.Values);
            Prog.UniformMatrix("viewMatrix", _api.Render.CameraMatrixOriginf);

            _api.Render.RenderMesh(_meshref);

            Prog.Stop();

            _api.Render.GlDisableCullFace();
            _api.Render.GLDepthMask(true);
        }

        public void Dispose()
        {
            _api.Event.UnregisterRenderer(this, EnumRenderStage.AfterBlit);
            _meshref?.Dispose();
        }
    }
}

using System.Diagnostics.CodeAnalysis;
using Vintagestory.API.Client;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace TeleportationNetwork
{
    public class TeleportRiftRenderer : IRenderer
    {
        public double RenderOrder => 0.05;
        public int RenderRange => 100;

        private IShaderProgram Prog => _renderSystem.Prog;

        private readonly ICoreClientAPI _api;
        private readonly BlockPos _pos;
        private readonly MeshRef _meshref;
        private readonly Matrixf _matrixf;
        private readonly float _rotation;

        private TeleportRenderSystem _renderSystem;
        private float _counter;
        private int _cnt;
        private float _activationProgress;
        private float _size;

        public TeleportRiftRenderer(BlockPos pos, ICoreClientAPI api, float rotation)
        {
            _api = api;
            _pos = pos;
            _rotation = rotation;

            _meshref = _api.Render.UploadMesh(QuadMeshUtil.GetQuad());
            _matrixf = new Matrixf();
            _size = 1;

            _api.Event.RegisterRenderer(this, EnumRenderStage.AfterBlit, $"{Constants.ModId}-teleport-rift");
            _renderSystem = _api.ModLoader.GetModSystem<TeleportRenderSystem>();
        }

        public void Update(Teleport teleport)
        {
            _size = teleport.Size;
        }

        public void SetActivationProgress(float stage)
        {
            _activationProgress = GameMath.Clamp(stage, 0, 1);
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

            //if (modsys.nearestRifts.Length > 0)
            //{
            //    Rift rift = modsys.nearestRifts[0];

            //    float dist = Math.Max(0, GameMath.Sqrt(plrPos.SquareDistanceTo(rift.Position)) - 1 - rift.Size / 2f);
            //    float f = Math.Max(0, 1 - dist / 3f);
            //    float jitter = capi.World.Rand.NextDouble() < 0.25 ? f * ((float)capi.World.Rand.NextDouble() - 0.5f) / 1f : 0;

            //    GlobalConstants.GuiGearRotJitter = jitter;

            //    capi.ModLoader.GetModSystem<SystemTemporalStability>().modGlitchStrength = Math.Min(1, f * 1.3f);

            //    if (temporalBehavior != null)
            //    {
            //        temporalBehavior.stabilityOffset = -Math.Pow(Math.Max(0, 1 - dist / 3), 2) * 20;
            //    }
            //}
            //else
            //{
            //    capi.ModLoader.GetModSystem<SystemTemporalStability>().modGlitchStrength = 0;
            //}

            _counter += deltaTime;
            if (glichEffectStrength > 0.1)
            {
                if (_api.World.Rand.NextDouble() < 0.012)
                {
                    _counter += 20 * (float)_api.World.Rand.NextDouble();
                }
            }

            _api.Render.GLDepthMask(false);

            Prog.Use();
            Prog.Uniform("rgbaFogIn", _api.Render.FogColor);
            Prog.Uniform("fogMinIn", _api.Render.FogMin);
            Prog.Uniform("fogDensityIn", _api.Render.FogDensity);
            Prog.Uniform("rgbaAmbientIn", _api.Render.AmbientColor);
            Prog.Uniform("rgbaLightIn", new Vec4f(1, 1, 1, 1));


            Prog.BindTexture2D("primaryFb", _api.Render.FrameBuffers[(int)EnumFrameBuffer.Primary].ColorTextureIds[0], 0);
            Prog.BindTexture2D("depthTex", _api.Render.FrameBuffers[(int)EnumFrameBuffer.Primary].DepthTextureId, 1);
            Prog.UniformMatrix("projectionMatrix", _api.Render.CurrentProjectionMatrix);


            int width = _api.Render.FrameWidth;
            int height = _api.Render.FrameHeight;
            Prog.Uniform("time", _counter);
            Prog.Uniform("invFrameSize", new Vec2f(1f / width, 1f / height));
            Prog.Uniform("glich", GameMath.Min(glichEffectStrength * 2, 1));
            Prog.Uniform("glich", GameMath.Min(glichEffectStrength * 2, 1));
            Prog.Uniform("stage", 0.1f + _activationProgress * 0.9f);
            int riftIndex = 0;

            _cnt = (_cnt + 1) % 3;

            //foreach (var rift in rifts.Values)
            //{
            //if (_cnt == 0)
            //{
            //    rift.Visible = _api.World.BlockAccessor.GetChunkAtBlockPos((int)rift.Position.X, (int)rift.Position.Y, (int)rift.Position.Z) != null;
            //}

            riftIndex++;
            _matrixf.Identity();

            //float dx = (float)(rift.Position.X - playerPos.X);
            //float dy = (float)(rift.Position.Y - playerPos.Y);
            //float dz = (float)(rift.Position.Z - playerPos.Z);

            float dx = (float)(_pos.X - camPos.X + 0.5f);
            float dy = (float)(_pos.Y - camPos.Y + 0.5f);
            float dz = (float)(_pos.Z - camPos.Z + 0.5f);

            var playerPos = _api.World.Player.Entity.Pos;
            _matrixf.Translate(dx, dy, dz);
            _matrixf.RotateYDeg(_rotation);
            if (_size == 5)
                _matrixf.Translate(0, 0, 0.25);
            //_matrixf.Rotate(GameMath.PIHALF, 0, -playerPos.Yaw);
            //_matrixf.Rotate(GameMath.PIHALF, 0, 0);
            //_matrixf.Rotate(0, playerPos.Yaw, 0);

            //_matrixf.Values[0] = 1f;
            //_matrixf.Values[1] = 0f;
            //_matrixf.Values[2] = 0f;
            //
            ////_matrixf.Values[4] = 0f;
            ////_matrixf.Values[5] = 1f;
            ////_matrixf.Values[6] = 0f;
            //
            //_matrixf.Values[8] = 0f;
            //_matrixf.Values[9] = 0f;
            //_matrixf.Values[10] = 1f;

            //float size = rift.GetNowSize(_api);
            //_matrixf.Scale(size, size, size);
            float wMod = 0.85f + 1 * 0.15f;
            float size = .54f * _size;
            _matrixf.Scale(size * wMod, size, size * wMod);

            Prog.UniformMatrix("modelMatrix", _matrixf.Values);
            Prog.UniformMatrix("viewMatrix", _api.Render.CameraMatrixOriginf);
            //_prog.Uniform("worldPos", new Vec4f(dx, dy, dz, 0));
            //_prog.Uniform("riftIndex", riftIndex);

            _api.Render.RenderMesh(_meshref);

            //if (dx * dx + dy * dy + dz * dz < 40 * 40)
            //{
            //    Vec3d ppos = rift.Position;
            //    _api.World.SpawnParticles(0.1f, ColorUtil.ColorFromRgba(21 / 2, 70 / 2, 116 / 2, 128), ppos, ppos, new Vec3f(-0.125f, -0.125f, -0.125f), new Vec3f(0.125f, 0.125f, 0.125f), 5, 0, (0.125f / 2 + (float)capi.World.Rand.NextDouble() * 0.25f) / 2);
            //}
            //}

            _counter = GameMath.Mod(_counter + deltaTime, GameMath.TWOPI * 100f);

            Prog.Stop();

            _api.Render.GLDepthMask(true);
        }

        public void Dispose()
        {
            _api.Event.UnregisterRenderer(this, EnumRenderStage.AfterBlit);
            _meshref?.Dispose();
        }
    }
}
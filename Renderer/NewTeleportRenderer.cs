using System.Diagnostics.CodeAnalysis;
using Vintagestory.API.Client;
using Vintagestory.API.MathTools;

namespace TeleportationNetwork
{
    public class NewTeleportRenderer : IRenderer
    {
        public double RenderOrder => 0.05;
        public int RenderRange => 100;

        private readonly ICoreClientAPI _api;
        private readonly BlockPos _pos;
        private readonly MeshRef _meshref;
        private readonly Matrixf _matrixf;

        private IShaderProgram _prog;
        private float _counter;
        private int _cnt;

        public NewTeleportRenderer(BlockPos pos, ICoreClientAPI api)
        {
            _api = api;
            _pos = pos;

            api.Event.RegisterRenderer(this, EnumRenderStage.AfterBlit, $"{Constants.ModId}-teleport-rift");
            MeshData mesh = QuadMeshUtil.GetQuad();
            _meshref = _api.Render.UploadMesh(mesh);
            _matrixf = new Matrixf();

            _api.Event.ReloadShader += LoadShader;
            LoadShader();
        }

        public void OnRenderFrame(float deltaTime, EnumRenderStage stage)
        {
            var playerPos = _api.World.Player.Entity.Pos;

            //var temporalBehavior = _api.World.Player.Entity.GetBehavior<EntityBehaviorTemporalStabilityAffected>();
            //if (temporalBehavior != null)
            //{
            //    temporalBehavior.stabilityOffset = 0;
            //}

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
            if (_api.World.Rand.NextDouble() < 0.012)
            {
                _counter += 20 * (float)_api.World.Rand.NextDouble();
            }

            _api.Render.GLDepthMask(false);

            _prog.Use();
            _prog.Uniform("rgbaFogIn", _api.Render.FogColor);
            _prog.Uniform("fogMinIn", _api.Render.FogMin);
            _prog.Uniform("fogDensityIn", _api.Render.FogDensity);
            _prog.Uniform("rgbaAmbientIn", _api.Render.AmbientColor);
            _prog.Uniform("rgbaLightIn", new Vec4f(1, 1, 1, 1));


            _prog.BindTexture2D("primaryFb", _api.Render.FrameBuffers[(int)EnumFrameBuffer.Primary].ColorTextureIds[0], 0);
            _prog.BindTexture2D("depthTex", _api.Render.FrameBuffers[(int)EnumFrameBuffer.Primary].DepthTextureId, 1);
            _prog.UniformMatrix("projectionMatrix", _api.Render.CurrentProjectionMatrix);


            int width = _api.Render.FrameWidth;
            int height = _api.Render.FrameHeight;
            _prog.Uniform("counter", _counter);
            float bf = 200 + (float)GameMath.Sin(_api.InWorldEllapsedMilliseconds / 24000.0) * 100;

            _prog.Uniform("counterSmooth", bf);
            _prog.Uniform("invFrameSize", new Vec2f(1f / width, 1f / height));
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

            float dx = (float)(_pos.X - playerPos.X + 0.5f);
            float dy = (float)(_pos.Y - playerPos.Y + 3);
            float dz = (float)(_pos.Z - playerPos.Z + 0.5f);

            _matrixf.Translate(dx, dy, dz);
            _matrixf.ReverseMul(_api.Render.CameraMatrixOriginf);

            _matrixf.Values[0] = 1f;
            _matrixf.Values[1] = 0f;
            _matrixf.Values[2] = 0f;

            //matrixf.Values[4] = 0f;
            //matrixf.Values[5] = 1f;
            //matrixf.Values[6] = 0f;

            _matrixf.Values[8] = 0f;
            _matrixf.Values[9] = 0f;
            _matrixf.Values[10] = 1f;

            //float size = rift.GetNowSize(_api);
            //_matrixf.Scale(size, size, size);

            _prog.UniformMatrix("modelViewMatrix", _matrixf.Values);
            _prog.Uniform("worldPos", new Vec4f(dx, dy, dz, 0));
            _prog.Uniform("riftIndex", riftIndex);

            _api.Render.RenderMesh(_meshref);

            //if (dx * dx + dy * dy + dz * dz < 40 * 40)
            //{
            //    Vec3d ppos = rift.Position;
            //    _api.World.SpawnParticles(0.1f, ColorUtil.ColorFromRgba(21 / 2, 70 / 2, 116 / 2, 128), ppos, ppos, new Vec3f(-0.125f, -0.125f, -0.125f), new Vec3f(0.125f, 0.125f, 0.125f), 5, 0, (0.125f / 2 + (float)capi.World.Rand.NextDouble() * 0.25f) / 2);
            //}
            //}

            _counter = GameMath.Mod(_counter + deltaTime, GameMath.TWOPI * 100f);

            _prog.Stop();

            _api.Render.GLDepthMask(true);
        }

        [MemberNotNull(nameof(_prog))]
        public bool LoadShader()
        {
            _prog = _api.Shader.NewShaderProgram();
            _prog.VertexShader = _api.Shader.NewShader(EnumShaderType.VertexShader);
            _prog.FragmentShader = _api.Shader.NewShader(EnumShaderType.FragmentShader);

            _api.Shader.RegisterFileShaderProgram("teleport", _prog);

            return _prog.Compile();
        }

        public void Dispose()
        {
            _api.Event.UnregisterRenderer(this, EnumRenderStage.AfterBlit);
            _meshref?.Dispose();
        }
    }
}

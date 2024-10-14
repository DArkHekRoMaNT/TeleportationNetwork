using System.Collections.Generic;
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

            var mesh = DarkMeshUtil.GetRectangle(1, 100);
            _meshref = _api.Render.UploadMesh(mesh);
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
                _matrixf.Translate(0, 0, -0.25);
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
            _matrixf.Scale(_size, _size, _size);

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

    public static class DarkMeshUtil
    {
        private static int[] _quadVertices = [-1, -1, 0, 1, -1, 0, 1, 1, 0, -1, 1, 0];

        private static int[] _quadTextureCoords = [0, 0, 1, 0, 1, 1, 0, 1];

        private static int[] _quadVertexIndices = [0, 1, 2, 0, 2, 3];

        public static MeshData GetRectangle(float totalSize, int gridSize)
        {
            var quadSize = totalSize / gridSize;
            var halfSize = totalSize / 2.0f;

            var vertices = new List<float>();
            var uvs = new List<float>();
            var indices = new List<int>();

            // Vertices and uvs
            for (int y = 0; y <= gridSize; y++)
            {
                for (int x = 0; x <= gridSize; x++)
                {
                    vertices.Add(x * quadSize - halfSize);
                    vertices.Add(y * quadSize - halfSize);
                    vertices.Add(0);
                    uvs.Add((float)x / gridSize);
                    uvs.Add((float)y / gridSize);
                }
            }

            // Indices
            for (int y = 0; y < gridSize; y++)
            {
                for (int x = 0; x < gridSize; x++)
                {
                    int topLeft = y * (gridSize + 1) + x;
                    int topRight = topLeft + 1;
                    int bottomLeft = topLeft + (gridSize + 1);
                    int bottomRight = bottomLeft + 1;

                    // First triangle
                    indices.Add(topLeft);
                    indices.Add(bottomLeft);
                    indices.Add(topRight);

                    // Second triangle
                    indices.Add(topRight);
                    indices.Add(bottomLeft);
                    indices.Add(bottomRight);
                }
            }

            var meshData = new MeshData();
            meshData.SetXyz(vertices.ToArray());
            meshData.SetUv(uvs.ToArray());
            meshData.SetVerticesCount(vertices.Count / 3);
            meshData.SetIndices(indices.ToArray());
            meshData.SetIndicesCount(indices.Count);

            return meshData;



            //var meshData = new MeshData();
            //float[] array = new float[12];
            //for (int i = 0; i < 12; i++)
            //{
            //    array[i] = _quadVertices[i];
            //}

            //meshData.SetXyz(array);
            //float[] array2 = new float[8];
            //for (int j = 0; j < array2.Length; j++)
            //{
            //    array2[j] = _quadTextureCoords[j];
            //}

            //meshData.SetUv(array2);
            //meshData.SetVerticesCount(4);
            //meshData.SetIndices(_quadVertexIndices);
            //meshData.SetIndicesCount(6);
            //return meshData;
        }
    }
}

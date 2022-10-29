using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace TeleportationNetwork
{
    public class SealRenderer : IRenderer
    {
        public bool Enabled { get; set; }
        public float Speed { get; set; }

        private float _timePassed;

        private readonly ICoreClientAPI _api;
        private readonly BlockPos _pos;

        private readonly int[] _sealTextureId;
        private readonly Matrixf _modelMatrix;
        private readonly MeshRef _sealModelRef;

        public SealRenderer(BlockPos pos, ICoreClientAPI api)
        {
            _api = api;
            _pos = pos;

            _timePassed = 0;
            _modelMatrix = new Matrixf();

            Speed = 1;

            _sealTextureId = new int[25];
            for (int i = 0; i < 5; i++)
            {
                for (int j = 0; j < 5; j++)
                {
                    var loc = new AssetLocation(Core.ModId, $"textures/block/teleport/seal-{i}-{j}.png");
                    _sealTextureId[i * 5 + j] = api.Render.GetOrLoadTexture(loc);
                }
            }
            MeshData modelData = QuadMeshUtil.GetCustomQuadHorizontal(0, 0, 0, 1, 1, 255, 255, 255, 255);
            _sealModelRef = api.Render.UploadMesh(modelData);

            api.Event.RegisterRenderer(this, EnumRenderStage.Opaque, Core.ModId + "-teleport");
        }

        public void OnRenderFrame(float deltaTime, EnumRenderStage stage)
        {
            if (!Enabled)
            {
                return;
            }

            _timePassed += deltaTime * 0.5f * Speed;

            IRenderAPI rpi = _api.Render;
            Vec3d camPos = _api.World.Player.Entity.CameraPos;

            rpi.GlDisableCullFace();
            rpi.GlToggleBlend(true);

            IStandardShaderProgram prog = rpi.PreparedStandardShader(_pos.X, _pos.Y, _pos.Z);
            prog.ExtraGlow = 10 + (int)((1 + Math.Sin(_timePassed * .5)) * 40);

            prog.RgbaAmbientIn = rpi.AmbientColor;
            prog.RgbaFogIn = rpi.FogColor;
            prog.FogMinIn = rpi.FogMin;
            prog.FogDensityIn = rpi.FogDensity;
            prog.RgbaTint = ColorUtil.WhiteArgbVec;

            double cx = _pos.X - camPos.X + 0.5;
            double cy = _pos.Y - camPos.Y + 1;
            double cz = _pos.Z - camPos.Z + 0.5;


            // Seal render

            for (int i = 0; i < 5; i++)
            {
                for (int j = 0; j < 5; j++)
                {
                    rpi.BindTexture2d(_sealTextureId[i * 5 + j]);

                    prog.ModelMatrix = _modelMatrix
                        .Identity()
                        .Translate(cx, cy + 0.01f, cz)
                        .Translate(-Constants.SealRadius + i, 0, -Constants.SealRadius + j)
                        .Values;

                    prog.ViewMatrix = rpi.CameraMatrixOriginf;
                    prog.ProjectionMatrix = rpi.CurrentProjectionMatrix;

                    rpi.RenderMesh(_sealModelRef);
                }
            }
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
            _api.Event.UnregisterRenderer(this, EnumRenderStage.Opaque);
            _sealModelRef.Dispose();
        }

        public double RenderOrder => 0.4;
        public int RenderRange => 100;
    }
}

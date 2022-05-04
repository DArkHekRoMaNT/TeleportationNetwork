using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace TeleportationNetwork
{
    public class TeleportRenderer : IRenderer
    {
        public float Speed { get; set; }
        public float Progress { get; set; }

        private readonly ICoreClientAPI api;
        private readonly BlockPos pos;

        private float timePassed;

        private int sealTextureId;
        private int tgearTextureId;
        private readonly Matrixf modelMatrix;

        private MeshRef sealModelRef;
        private MeshRef progressCircleModelRef;

        public TeleportRenderer(BlockPos pos, ICoreClientAPI api)
        {
            this.api = api;
            this.pos = pos;

            timePassed = 0;
            modelMatrix = new Matrixf();

            Speed = 1;
            Progress = 0;

            sealTextureId = api.Render.GetOrLoadTexture(new AssetLocation(Core.ModId, "textures/block/teleport/seal2.png"));
            MeshData modelData = QuadMeshUtil.GetCustomQuadHorizontal(0, 0, 0, 1, 1, 255, 255, 255, 255);
            sealModelRef = api.Render.UploadMesh(modelData);

            tgearTextureId = api.Render.GetOrLoadTexture(new AssetLocation("game", "textures/item/resource/temporalgear.png"));
            UpdateCirceMesh(Progress);

            api.Event.RegisterRenderer(this, EnumRenderStage.Opaque, Core.ModId + "-teleport");
        }

        private void UpdateCirceMesh(float progress)
        {
            int maxSteps = 48;

            var ringSize = .9f;
            var stepSize = 1.0F / maxSteps;

            var steps = 1 + (int)Math.Ceiling(maxSteps * progress);
            var data = new MeshData(steps * 2, steps * 6, false, false, true, false);

            float[] uvpart = new float[] { 0, 0, 1/32f, 0, 1 / 32f, 1 / 32f, 0, 1 / 32f };
            float[] uv = new float[8 * steps];

            for (var i = 0; i < steps; i++)
            {
                var p = Math.Min(progress, i * stepSize) * Math.PI * 2;
                var x = (float)Math.Sin(p);
                var y = -(float)Math.Cos(p);

                data.AddVertex(x, 0, y, ColorUtil.WhiteArgb);
                data.AddVertex(x * ringSize, 0, y * ringSize, ColorUtil.WhiteArgb);

                uvpart.CopyTo(uv, i * 8);

                if (i > 0)
                {
                    data.AddIndices(new[] { i * 2 - 2, i * 2 - 1, i * 2 + 0 });
                    data.AddIndices(new[] { i * 2 + 0, i * 2 - 1, i * 2 + 1 });
                }
            }

            data.SetUv(uv);

            //if (progressCircleModelRef != null) api.Render.UpdateMesh(progressCircleModelRef, data);
            //else progressCircleModelRef = api.Render.UploadMesh(data);
            progressCircleModelRef = api.Render.UploadMesh(data);
        }

        public void OnRenderFrame(float deltaTime, EnumRenderStage stage)
        {
            timePassed += deltaTime * 0.5f * Speed;
            UpdateCirceMesh(Progress);

            IRenderAPI rpi = api.Render;
            Vec3d camPos = api.World.Player.Entity.CameraPos;

            rpi.GlDisableCullFace();
            rpi.GlToggleBlend(true);

            IStandardShaderProgram prog = rpi.PreparedStandardShader(pos.X, pos.Y, pos.Z);
            prog.ExtraGlow = 10 + (int)((1 + Math.Sin(timePassed * .5)) * 40);

            prog.RgbaAmbientIn = rpi.AmbientColor;
            prog.RgbaFogIn = rpi.FogColor;
            prog.FogMinIn = rpi.FogMin;
            prog.FogDensityIn = rpi.FogDensity;
            prog.RgbaTint = ColorUtil.WhiteArgbVec;

            double cx = pos.X - camPos.X + 0.5;
            double cy = pos.Y - camPos.Y + 1;
            double cz = pos.Z - camPos.Z + 0.5;


            // Seal render
            rpi.BindTexture2d(sealTextureId);

            prog.ModelMatrix = modelMatrix
                .Identity()
                .Translate(cx, cy + 0.01f, cz)
                .Translate(-Constants.SealRadius, 0, -Constants.SealRadius)
                .Scale(5, 0, 5)
                .Values;

            prog.ViewMatrix = rpi.CameraMatrixOriginf;
            prog.ProjectionMatrix = rpi.CurrentProjectionMatrix;

            rpi.RenderMesh(sealModelRef);

            // Progress circle render
            rpi.BindTexture2d(tgearTextureId);

            prog.ModelMatrix = modelMatrix
                .Identity()
                .Translate(cx, cy + 0.02f, cz)
                .Scale(0.5f, 0, 0.5f)
                .Values;

            prog.ViewMatrix = rpi.CameraMatrixOriginf;
            prog.ProjectionMatrix = rpi.CurrentProjectionMatrix;

            rpi.RenderMesh(progressCircleModelRef);

            prog.Stop();
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
            api.Event.UnregisterRenderer(this, EnumRenderStage.Opaque);
            sealModelRef?.Dispose();
            progressCircleModelRef?.Dispose();
        }

        public double RenderOrder => 0.4;
        public int RenderRange => 100;
    }
}

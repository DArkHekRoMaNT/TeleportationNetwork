/*
 *  Based on https://github.com/copygirl/CarryCapacity/blob/master/src/Client/HudOverlayRenderer.cs
 */

using System;
using Vintagestory.API.Client;
using Vintagestory.API.MathTools;

namespace TeleportationNetwork
{
    public class HudCircleRenderer : IRenderer
    {
        private HudCircleSettings Settings { get; }
        private ICoreClientAPI Api { get; }

        private MeshRef? _circleMesh;

        private float _circleAlpha;
        private float _circleProgress;

        public bool CircleVisible { get; set; }

        /// <summary>
        /// Circle filling percentage
        /// </summary>
        public float CircleProgress
        {
            get => _circleProgress;
            set
            {
                _circleProgress = GameMath.Clamp(value, 0, 1);
                CircleVisible = true;
            }
        }

        public HudCircleRenderer(ICoreClientAPI api, HudCircleSettings settings)
        {
            Api = api;
            Settings = settings;
            Api.Event.RegisterRenderer(this, EnumRenderStage.Ortho);
            UpdateCirceMesh(1);
        }

        private void UpdateCirceMesh(float progress)
        {
            var ringSize = Settings.InnerRadius / Settings.OuterRadius;
            var stepSize = 1f / Settings.MaxSteps;

            var steps = 1 + (int)Math.Ceiling(Settings.MaxSteps * progress);
            var data = new MeshData(steps * 2, steps * 6, false, false, true, false);

            for (var i = 0; i < steps; i++)
            {
                var p = Math.Min(progress, i * stepSize) * Math.PI * 2;
                var x = (float)Math.Sin(p);
                var y = -(float)Math.Cos(p);

                data.AddVertex(x, y, 0, ColorUtil.WhiteArgb);
                data.AddVertex(x * ringSize, y * ringSize, 0, ColorUtil.WhiteArgb);

                if (i > 0)
                {
                    data.AddIndices(new[] { i * 2 - 2, i * 2 - 1, i * 2 + 0 });
                    data.AddIndices(new[] { i * 2 + 0, i * 2 - 1, i * 2 + 1 });
                }
            }

            if (_circleMesh != null)
            {
                Api.Render.UpdateMesh(_circleMesh, data);
            }
            else
            {
                _circleMesh = Api.Render.UploadMesh(data);
            }
        }

        // IRenderer implementation

        public double RenderOrder => 0;
        public int RenderRange => 10;

        public void OnRenderFrame(float deltaTime, EnumRenderStage stage)
        {
            var rend = Api.Render;
            var shader = rend.CurrentActiveShader;

            _circleAlpha = Math.Max(0f, Math.Min(1f, _circleAlpha
                + deltaTime / (CircleVisible ? Settings.AlphaIn : -Settings.AlphaOut)));

            // TODO: Do some smoothing between frames?
            if (CircleProgress <= 0 || _circleAlpha <= 0)
            {
                return;
            }

            UpdateCirceMesh(CircleProgress);

            var r = (Settings.Color >> 16 & 0xFF) / 255f;
            var g = (Settings.Color >> 8 & 0xFF) / 255f;
            var b = (Settings.Color & 0xFF) / 255f;
            var color = new Vec4f(r, g, b, _circleAlpha);

            shader.Uniform("rgbaIn", color);
            shader.Uniform("extraGlow", 0);
            shader.Uniform("applyColor", 0);
            shader.Uniform("tex2d", 0);
            shader.Uniform("noTexture", 1f);
            shader.UniformMatrix("projectionMatrix", rend.CurrentProjectionMatrix);

            int x, y;
            if (Api.Input.MouseGrabbed)
            {
                x = Api.Render.FrameWidth / 2;
                y = Api.Render.FrameHeight / 2;
            }
            else
            {
                x = Api.Input.MouseX;
                y = Api.Input.MouseY;
            }

            rend.GlPushMatrix();
            rend.GlTranslate(x, y, 0);
            rend.GlScale(Settings.OuterRadius, Settings.OuterRadius, 0);
            shader.UniformMatrix("modelViewMatrix", rend.CurrentModelviewMatrix);
            rend.GlPopMatrix();

            rend.RenderMesh(_circleMesh);
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
            if (_circleMesh != null)
            {
                Api.Render.DeleteMesh(_circleMesh);
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace TeleportationNetwork
{
    public class TeleportShapeRenderer : IRenderer
    {
        public double RenderOrder => 0.37;
        public int RenderRange => 100;

        private readonly ICoreClientAPI _api;
        private readonly BlockPos _pos;
        private readonly float _rotationDeg;
        private readonly float _size;
        private readonly Matrixf _modelMatrix;

        private readonly MeshRef _staticMesh;
        private readonly MeshRef _dynamicMesh;
        private readonly MeshRef[] _rodMesh;

        private float _progress;
        private float _ringRotation;

        public TeleportShapeRenderer(ICoreClientAPI api, BlockPos pos, Block block, GateSettings settings)
        {
            _api = api;
            _pos = pos;

            _rotationDeg = settings.Rotation;
            _size = settings.Size;
            _modelMatrix = new Matrixf();

            Shape GetShape(AssetLocation loc) => api.Assets.Get<Shape>(loc.CopyWithPathPrefixAndAppendixOnce("shapes/", ".json"));

            MeshRef UploadMesh(Shape shape)
            {
                api.Tesselator.TesselateShape(block, shape, out var mesh);
                return api.Render.UploadMesh(mesh);
            }

            _staticMesh = UploadMesh(GetShape(settings.Shapes.Static));
            _dynamicMesh = UploadMesh(GetShape(settings.Shapes.Dynamic));

            static IEnumerable<string> GetLast(ShapeElement element)
            {
                if (element.Children == null || element.Children.Length == 0)
                {
                    yield return element.Name;
                }
                else
                {
                    foreach (var child in element.Children)
                    {
                        foreach (var last in GetLast(child))
                        {
                            yield return last;
                        }
                    }
                }
            }

            var rodShape = GetShape(settings.Shapes.Rod);
            var rodMeshes = new List<MeshRef>();
            while (rodShape.Elements.Length == 1 && // Origin
                rodShape.Elements[0].Children?.Length == 1) // Hub
            {
                rodMeshes.Add(UploadMesh(rodShape));
                rodShape = rodShape.Clone();

                var forRemove = GetLast(rodShape.Elements[0]);
                rodShape.RemoveElements(forRemove.ToArray());
            }
            _rodMesh = rodMeshes.ToArray();

            _api.Event.RegisterRenderer(this, EnumRenderStage.Opaque, $"{Constants.ModId}-teleport-shape");
        }

        public void Update(TeleportActivator status)
        {
            _progress = status.Progress;
        }

        public void OnRenderFrame(float deltaTime, EnumRenderStage stage)
        {
            var rpi = _api.Render;
            var camPos = _api.World.Player.Entity.CameraPos;

            rpi.GlToggleBlend(true);
            rpi.GlDisableCullFace();

            var prog = rpi.PreparedStandardShader(_pos.X, _pos.Y, _pos.Z);

            prog.Tex2D = _api.BlockTextureAtlas.AtlasTextures[0].TextureId;

            var cx = _pos.X - camPos.X;
            var cy = _pos.Y - camPos.Y;
            var cz = _pos.Z - camPos.Z;

            var dir = _rotationDeg / 90 % 2;

            // Static render
            prog.ModelMatrix = _modelMatrix
                    .Identity()
                    .Translate(cx + 0.5, cy + 0.5, cz + 0.5)
                    .RotateYDeg(_rotationDeg)
                    .Translate(-0.5, -0.5, -0.5)
                    .Values;

            prog.ViewMatrix = rpi.CameraMatrixOriginf;
            prog.ProjectionMatrix = rpi.CurrentProjectionMatrix;

            rpi.RenderMesh(_staticMesh);

            // Dynamic ring render
            _ringRotation += _progress * deltaTime / 2.5f;
            prog.ModelMatrix = _modelMatrix
                    .Identity()
                    .Translate(cx + 0.5, cy + 0.5, cz + 0.5)
                    .RotateYDeg(_rotationDeg)
                    .RotateZ(_ringRotation)
                    .Translate(-0.5, -0.5, -0.5)
                    .Values;

            prog.ViewMatrix = rpi.CameraMatrixOriginf;
            prog.ProjectionMatrix = rpi.CurrentProjectionMatrix;

            rpi.RenderMesh(_dynamicMesh);

            // Rods render
            for (int i = 0; i < 8; i++)
            {
                var rodRotation = GameMath.TWOPI * (i / 8f);
                var step = _progress * 4f;
                var xStep = -Math.Sin(rodRotation) * step;
                var yStep = Math.Cos(rodRotation) * step;

                var mesh = _rodMesh[(int)Math.Clamp(_progress * _rodMesh.Length, 0, _rodMesh.Length - 1)];

                prog.ModelMatrix = _modelMatrix
                    .Identity()
                    .Translate(cx + 0.5, cy + 0.5, cz + 0.5)
                    .RotateYDeg(_rotationDeg)
                    .Translate(xStep, yStep, 0)
                    .RotateZ(rodRotation)
                    .Translate(-0.5, -0.5, -0.5)
                    .RotateYDeg(0.1f)
                    .Values;

                prog.ViewMatrix = rpi.CameraMatrixOriginf;
                prog.ProjectionMatrix = rpi.CurrentProjectionMatrix;

                rpi.RenderMesh(mesh);
            }

            prog.Stop();
        }

        public void Dispose()
        {
            _api.Event.UnregisterRenderer(this, EnumRenderStage.Opaque);
            _staticMesh?.Dispose();
            _dynamicMesh?.Dispose();

            if (_rodMesh != null)
                foreach (var mesh in _rodMesh)
                    mesh.Dispose();
        }
    }
}

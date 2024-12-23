using MonoMod.Core.Platforms;
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
        private readonly Matrixf _modelMatrix;

        private float _rotationDeg;
        private float _size;
        private MeshRef? _staticMesh;
        private MeshRef? _dynamicMesh;
        private MeshRef[]? _rodMesh;

        private TeleportStatus _status;
        private float _ringRotation;

        private readonly (float rotX, float rotY, float rotZ, float xOffset, float yOffset)[] _animPreset =
        [
            (+0.1f, +0.0f, +0.1f, -0.1f, +1.0f),
            (+0.0f, +2.0f, +0.1f, +0.1f, +1.0f),
            (+0.0f, +0.0f, -0.1f, +0.2f, +0.0f),
            (+0.2f, +1.2f, -0.2f, +0.0f, +0.0f),
            (-0.3f, +0.0f, +0.2f, +0.3f, +0.0f),
            (+0.0f, +0.2f, +0.0f, +0.0f, +0.0f),
            (-0.2f, +1.2f, +0.1f, -0.3f, +1.0f),
            (+0.2f, +0.0f, +0.3f, +0.1f, +1.0f)
        ];

        public TeleportShapeRenderer(ICoreClientAPI api, BlockPos pos)
        {
            _api = api;
            _pos = pos;

            _status = new TeleportStatus();
            _rotationDeg = 0;
            _size = 0;
            _modelMatrix = new Matrixf();

            _api.Event.RegisterRenderer(this, EnumRenderStage.Opaque, $"{Constants.ModId}-teleport-shape");
        }

        public void UpdateMesh(Block block, float rotationDeg, int size)
        {
            if (rotationDeg == _rotationDeg && size == _size)
            {
                return;
            }

            _rotationDeg = rotationDeg;
            _size = size;

            Shape? GetShape(AssetLocation loc)
            {
                return loc == null ? null : _api.Assets.Get<Shape>(loc.CopyWithPathPrefixAndAppendixOnce("shapes/", ".json"));
            }

            MeshRef? UploadMesh(Shape? shape)
            {
                if (shape == null) return null;
                _api.Tesselator.TesselateShape(block, shape, out var mesh);
                return _api.Render.UploadMesh(mesh);
            }

            CleanMeshes();

            _staticMesh = UploadMesh(GetShape($"{Constants.ModId}:block/gate/static"));
            _dynamicMesh = UploadMesh(GetShape($"{Constants.ModId}:block/gate/dynamic"));

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

            var rodShape = GetShape($"{Constants.ModId}:block/gate/rod");
            if (rodShape != null)
            {
                var rodMeshes = new List<MeshRef>();
                while (rodShape.Elements.Length == 1 && // Origin
                    rodShape.Elements[0].Children?.Length == 1) // Hub
                {
                    rodMeshes.Add(UploadMesh(rodShape)!);
                    rodShape = rodShape.Clone();

                    var forRemove = GetLast(rodShape.Elements[0]);
                    rodShape.RemoveElements(forRemove.ToArray());
                }
                _rodMesh = rodMeshes.ToArray();
            }
        }

        public void Update(TeleportStatus status)
        {
            _status = status;
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

            var repairingProgress = !_status.IsRepaired ? 1f - _status.Progress : 0f;
            var activatingProgress = _status.IsRepaired ? _status.Progress : 0f;

            var size = _size / 10f;
            var zOffset = _size == 10f ? 0 : -0.5f;

            // Static render
            if (_staticMesh != null)
            {
                prog.ModelMatrix = _modelMatrix
                        .Identity()
                        .Translate(cx + 0.5, cy + 0.5, cz + 0.5)
                        .RotateYDeg(_rotationDeg)
                        .Scale(size, size, size)
                        .Translate(-0.5, -0.5, -0.5 + zOffset)
                        .Values;

                prog.ViewMatrix = rpi.CameraMatrixOriginf;
                prog.ProjectionMatrix = rpi.CurrentProjectionMatrix;

                rpi.RenderMesh(_staticMesh);
            }

            // Dynamic ring render
            if (_dynamicMesh != null)
            {
                _ringRotation += activatingProgress * deltaTime / 5f;
                prog.ModelMatrix = _modelMatrix
                        .Identity()
                        .Translate(cx + 0.5, cy + 0.5, cz + 0.5)
                        .RotateYDeg(_rotationDeg)
                        .RotateZ(_ringRotation)
                        .Scale(size, size, size)
                        .Translate(-0.5, -0.5, -0.5 + zOffset)
                        .Values;

                prog.ViewMatrix = rpi.CameraMatrixOriginf;
                prog.ProjectionMatrix = rpi.CurrentProjectionMatrix;

                rpi.RenderMesh(_dynamicMesh);
            }

            // Rods render
            if (_rodMesh != null)
            {
                for (int i = 0; i < 8; i++)
                {
                    var (rotX, rotY, rotZ, xOffset, yOffset) = _animPreset[i];

                    var rodRotation = GameMath.TWOPI * (i / 8f);
                    var step = activatingProgress * 4f * size;
                    var xStep = -Math.Sin(rodRotation) * step * (1 + xOffset * repairingProgress);
                    var yStep = Math.Cos(rodRotation) * step * (1 + yOffset * repairingProgress);

                    var mesh = _rodMesh[(int)Math.Clamp(activatingProgress * _rodMesh.Length, 0, _rodMesh.Length - 1)];

                    prog.ModelMatrix = _modelMatrix
                        .Identity()
                        .Translate(cx + 0.5, cy + 0.5, cz + 0.5)
                        .RotateYDeg(_rotationDeg)
                        .Translate(xStep, yStep, 0)
                        .RotateZ(rodRotation)
                        .Rotate(rotX * repairingProgress, rotY * repairingProgress, rotZ * repairingProgress)
                        .Scale(size, size, size)
                        .Translate(-0.5, -0.5, -0.5 + zOffset)
                        .RotateYDeg(0.01f)
                        .Values;

                    prog.ViewMatrix = rpi.CameraMatrixOriginf;
                    prog.ProjectionMatrix = rpi.CurrentProjectionMatrix;

                    rpi.RenderMesh(mesh);
                }
            }

            prog.Stop();
        }

        public void CleanMeshes()
        {
            _staticMesh?.Dispose();
            _dynamicMesh?.Dispose();

            if (_rodMesh != null)
            {
                foreach (var mesh in _rodMesh)
                {
                    mesh.Dispose();
                }
            }
        }

        public void Dispose()
        {
            _api.Event.UnregisterRenderer(this, EnumRenderStage.Opaque);
            CleanMeshes();
        }
    }
}

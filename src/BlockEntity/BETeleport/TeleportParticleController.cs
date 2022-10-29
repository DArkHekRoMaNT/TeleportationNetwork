using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;

namespace TeleportationNetwork
{
    public class TeleportParticleController
    {
        private readonly ICoreAPI _api;
        private readonly SimpleParticleProperties _circleParticles;
        private readonly SimpleParticleProperties _teleportParticles;
        private readonly int[] _colors;

        public TeleportParticleController(ICoreAPI api)
        {
            _api = api;

            _circleParticles = new SimpleParticleProperties()
            {
                MinQuantity = 1f,
                AddQuantity = 0f,
                MinPos = new Vec3d(),
                AddPos = new Vec3d(),
                MinVelocity = new Vec3f(),
                AddVelocity = new Vec3f(),
                LifeLength = 0.5f,
                GravityEffect = -0.05f,
                MinSize = 0.1f,
                MaxSize = 0.2f,
                ParticleModel = EnumParticleModel.Quad,
                SelfPropelled = true,
                SizeEvolve = EvolvingNatFloat.create(EnumTransformFunction.LINEARNULLIFY, -0.1f),
                VertexFlags = 20 & VertexFlags.GlowLevelBitMask
            };

            _teleportParticles = new SimpleParticleProperties()
            {
                MinQuantity = 15,
                AddQuantity = 15,
                MinPos = new Vec3d(),
                AddPos = new Vec3d(),
                MinVelocity = new Vec3f(-1f, -1f, -1f),
                AddVelocity = new Vec3f(2, 2, 2),
                LifeLength = 0.5f,
                GravityEffect = 0,
                MinSize = 0.2f,
                MaxSize = 0.4f,
                ParticleModel = EnumParticleModel.Quad,
                RandomVelocityChange = true,
                VertexFlags = 20 & VertexFlags.GlowLevelBitMask,
                SizeEvolve = EvolvingNatFloat.create(EnumTransformFunction.LINEARNULLIFY, -0.5f)
            };

            _colors = GetColorsFromPNG(api, "game:textures/item/resource/temporalgear.png");
        }
        private void SpawnCircleEdgeParticle(float radius, Vec3d pos)
        {
            _circleParticles.MinPos.Set(MathUtil.RandomPosOnCircleEdge(radius, pos));
            _circleParticles.Color = GetRandomColor();
            _api.World.SpawnParticles(_circleParticles);
        }

        private void SpawnCircleEdgeParticle(float radius, Vec3d pos, int quantity)
        {
            for (int i = 0; i < quantity; i++)
            {
                SpawnCircleEdgeParticle(radius, pos);
            }
        }

        public void SpawnSealEdgeParticle(BlockPos pos)
        {
            SpawnCircleEdgeParticle(Constants.SealRadius, GetSealCenter(pos), 3);
        }

        public void SpawnActiveParticles(BlockPos pos, float time)
        {
            _circleParticles.LifeLength = 0.5f + (float)Math.Exp(time) * 0.1f;

            SpawnCircleEdgeParticle(Constants.SealRadius, GetSealCenter(pos), 3 + (int)Math.Exp(time));

            Entity[] entities = MathUtil.GetInCircleEntities(_api, Constants.SealRadius, GetSealCenter(pos));
            foreach (Entity entity in entities)
            {
                float radius = entity.CollisionBox.Width / 1.5f;
                Vec3d entityPos = entity.SidedPos.XYZ;

                int quantity = (int)(entity.CollisionBox.Width * entity.CollisionBox.Width * (5 + Math.Exp(time)));
                SpawnCircleEdgeParticle(radius, entityPos, quantity);
            }

            _circleParticles.LifeLength = 0.5f;
        }

        public void SpawnTeleportParticles(Entity entity)
        {
            float width = entity.CollisionBox.Width;
            _teleportParticles.AddPos.Set(width, entity.CollisionBox.Height, width);
            _teleportParticles.MinPos.Set(entity.SidedPos).Add(-width / 2, 0, -width / 2);
            _teleportParticles.Color = GetRandomColor();
            _api.World.SpawnParticles(_teleportParticles);
        }

        public int GetRandomColor()
        {
            int id = _api.World.Rand.Next(0, _colors.Length);
            return _colors[id];
        }

        private static int[] GetColorsFromPNG(ICoreAPI api, string path)
        {
            var colors = new List<int>();

            IAsset asset = api.Assets.Get(new AssetLocation(path));
            using var ms = new MemoryStream(asset.Data);
            using var bitmap = new Bitmap(ms);
            {
                for (int i = 0; i < bitmap.Width; i++)
                {
                    for (int j = 0; j < bitmap.Height; j++)
                    {
                        colors.Add(bitmap.GetPixel(i, j).ToArgb());
                    }
                }
            }

            return colors.ToArray();
        }

        private static Vec3d GetSealCenter(BlockPos pos) => pos.ToVec3d().Add(0.5, 1, 0.5);
    }
}

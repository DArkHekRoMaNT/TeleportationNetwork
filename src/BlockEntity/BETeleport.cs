using System;
using System.Collections.Generic;
using System.Text;
using SharedUtils;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace TeleportationNetwork
{
    public class BlockEntityTeleport : BlockEntity
    {

        public Core Core { get; private set; }
        public TPNetManager TPNetManager { get; private set; }

        GuiDialogTeleport teleportDlg;
        GuiDialogRenameTeleport renameDlg;

        Dictionary<string, TeleportingPlayer> tpingPlayers;

        long? listenerid;

        public string State
        {
            get
            {
                if (Block.Variant.TryGetValue("state", out string state)) return state;
                else
                {
                    State = TeleportState.Broken;
                    return TeleportState.Broken;
                }
            }
            set
            {
                string state = TeleportState.CheckValue(value, TeleportState.Broken);

                if (state == TeleportState.Broken) RemoveGameTickers();
                else if (state == TeleportState.Normal) SetupGameTickers();

                Block block = Api.World.GetBlock(Block.CodeWithVariant("state", state));
                Api.World.BlockAccessor.SetBlock(block.BlockId, Pos);
            }
        }

        public bool IsNormal => State == TeleportState.Normal;
        public bool Active { get; set; }


        public AssetLocation DefaultFrameCode => new AssetLocation(Block.Attributes?["frame"]?.AsString() ?? "game:stonebricks-granite");

        private MeshData _frameMesh;
        private ItemStack _frameStack;
        public ItemStack FrameStack
        {
            get => _frameStack;
            set
            {
                Api.World.SpawnItemEntity(_frameStack, Pos.ToVec3d().Add(Block.TopMiddlePos));
                _frameStack = value;

                UpdateFrameMesh();
                MarkDirty(true);
            }
        }

        private void UpdateFrameMesh()
        {
            if (Api is ICoreClientAPI capi)
            {
                AssetLocation shapeCode = new AssetLocation(ConstantsCore.ModId, "shapes/block/teleport/frame.json");
                Shape frameShape = Api.Assets.Get<Shape>(shapeCode);
                capi.Tesselator.TesselateShape(_frameStack.Collectible, frameShape, out _frameMesh);
            }
        }

        BlockEntityAnimationUtil AnimUtil => GetBehavior<BEBehaviorAnimatable>()?.animUtil;

        static readonly SimpleParticleProperties props = new SimpleParticleProperties(
            minQuantity: 0.3f,
            maxQuantity: 1.3f,
            color: ColorUtil.ColorFromRgba(255, 215, 1, 255),
            minPos: new Vec3d(), maxPos: new Vec3d(),
            minVelocity: new Vec3f(), maxVelocity: new Vec3f(),
            lifeLength: 1f,
            gravityEffect: -0.1f,
            minSize: 0.05f,
            maxSize: 0.2f,
            model: EnumParticleModel.Quad
        );

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);

            Core = api.ModLoader.GetModSystem<Core>();
            TPNetManager = api.ModLoader.GetModSystem<TPNetManager>();
            tpingPlayers = new Dictionary<string, TeleportingPlayer>();


            TPNetManager.TryCreateData(Pos, IsNormal);

            if (FrameStack == null)
            {
                FrameStack = new ItemStack(api.World.GetBlock(DefaultFrameCode));
            }
            else
            {
                FrameStack.ResolveBlockOrItem(api.World);
                UpdateFrameMesh();
            }

            if (api.Side == EnumAppSide.Client)
            {
                float rotY = Block.Shape.rotateY;
                AnimUtil?.InitializeAnimator(ConstantsCore.ModId + "-teleport", new Vec3f(0, rotY, 0));
            }

            if (IsNormal)
            {
                SetupGameTickers();
            }
        }

        public override void OnLoadCollectibleMappings(IWorldAccessor worldForNewMappings, Dictionary<int, AssetLocation> oldBlockIdMapping, Dictionary<int, AssetLocation> oldItemIdMapping, int schematicSeed)
        {
            if (FrameStack == null)
            {
                FrameStack = new ItemStack(worldForNewMappings.GetBlock(DefaultFrameCode));
            }
            else
            {
                FrameStack.FixMapping(oldBlockIdMapping, oldItemIdMapping, worldForNewMappings);
            }

            base.OnLoadCollectibleMappings(worldForNewMappings, oldBlockIdMapping, oldItemIdMapping, schematicSeed);
        }

        private void SetupGameTickers()
        {
            listenerid = RegisterGameTickListener(OnGameTick_Normal, 50);
        }
        private void RemoveGameTickers()
        {
            if (listenerid != null) UnregisterGameTickListener((long)listenerid);
        }

        private void OnGameTick_Normal(float dt)
        {
            // TODO: Move to Init and change State?
            if (Api.Side == EnumAppSide.Client && AnimUtil?.animator?.ActiveAnimationCount == 0)
            {
                AnimUtil.StartAnimation(new AnimationMetaData()
                {
                    Animation = "octagram",
                    Code = "octagram",
                    AnimationSpeed = 50f,
                    EaseInSpeed = float.MaxValue,
                    EaseOutSpeed = float.MaxValue
                });
                AnimUtil.StartAnimation(new AnimationMetaData()
                {
                    Animation = "largegears",
                    Code = "largegears",
                    AnimationSpeed = 25f,
                    EaseInSpeed = float.MaxValue,
                    EaseOutSpeed = float.MaxValue
                });
                AnimUtil.StartAnimation(new AnimationMetaData()
                {
                    Animation = "smallgears",
                    Code = "smallgears",
                    AnimationSpeed = 50f,
                    EaseInSpeed = float.MaxValue,
                    EaseOutSpeed = float.MaxValue
                });
            }

            if (!Active) return;

            List<string> toRemove = new List<string>();

            float bestSecondsPassed = 0;
            foreach (var val in tpingPlayers)
            {
                if (val.Value.State == EnumTeleportingEntityState.None)
                {
                    if (Api.Side == EnumAppSide.Server)
                    {
                        IServerPlayer player = Api.World.PlayerByUid(val.Key) as IServerPlayer;
                        TPNetManager.AddAvailableTeleport(player, Pos);
                    }
                    val.Value.State = EnumTeleportingEntityState.Teleporting;
                }

                val.Value.SecondsPassed += Math.Min(0.5f, dt);

                if (Api.World.ElapsedMilliseconds - val.Value.LastCollideMs > 300)
                {
                    toRemove.Add(val.Key);
                    continue;
                }

                if (val.Value.SecondsPassed > 3 && val.Value.State == EnumTeleportingEntityState.Teleporting)
                {
                    val.Value.State = EnumTeleportingEntityState.UI;

                    if (Api.Side == EnumAppSide.Client && teleportDlg?.IsOpened() != true)
                    {
                        if (teleportDlg != null) teleportDlg.Dispose();

                        teleportDlg = new GuiDialogTeleport(Api as ICoreClientAPI, Pos);
                        teleportDlg.TryOpen();
                        Core.HudCircleRenderer.CircleVisible = false;
                    }
                }

                bestSecondsPassed = Math.Max(val.Value.SecondsPassed, bestSecondsPassed);
            }

            foreach (var playerUID in toRemove)
            {
                tpingPlayers.Remove(playerUID);

                if (Api.Side == EnumAppSide.Client)
                {
                    teleportDlg?.TryClose();
                }
            }

            Active = tpingPlayers.Count > 0;

            if (Api.Side == EnumAppSide.Server)
            {
                for (int i = 0; i < 10; i++)
                {
                    props.MinPos.Set(RandomParticleInCirclePos());
                    Api.World.SpawnParticles(props);
                }
            }

            if (Api.Side == EnumAppSide.Client)
            {
                bestSecondsPassed = Math.Min(bestSecondsPassed, 3);
                AnimUtil.activeAnimationsByAnimCode["octagram"].AnimationSpeed = (float)(50f * (1 + Math.Exp(bestSecondsPassed) * 0.3f));
                Core.HudCircleRenderer.CircleProgress = bestSecondsPassed / 3f;
            }
        }

        public void OnEntityCollide(Entity entity)
        {
            if (!IsNormal || !(entity is EntityPlayer player)) return;

            if (player.IsActivityRunning("teleportCooldown")) //BUG ActivityRunning not working?
            {
                return;
            }

            if (!tpingPlayers.TryGetValue(player.PlayerUID, out TeleportingPlayer tpe))
            {
                tpingPlayers[player.PlayerUID] = tpe = new TeleportingPlayer()
                {
                    Player = player,
                    State = EnumTeleportingEntityState.None
                };
            }

            tpe.LastCollideMs = Api.World.ElapsedMilliseconds;
            Active = true;
        }

        public override void OnBlockRemoved()
        {
            base.OnBlockRemoved();

            if (Api.Side == EnumAppSide.Server)
            {
                TPNetManager.RemoveTeleport(Pos);
            }
        }

        public Entity[] GetInCircleEntities()
        {
            float r = 2.5f;
            Vec3d c = Pos.ToVec3d().Add(0.5, r, 0.5);

            return Api.World.GetEntitiesAround(c, r, r, (e) => e.Pos.DistanceTo(c) < r);
        }

        public void OpenRenameDlg()
        {
            if (Api.Side == EnumAppSide.Client)
            {
                if (renameDlg == null)
                {
                    renameDlg = new GuiDialogRenameTeleport(Pos, Api as ICoreClientAPI, CairoFont.WhiteSmallText());
                }

                if (!renameDlg.IsOpened()) renameDlg.TryOpen();
            }
        }

        public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
        {
            base.GetBlockInfo(forPlayer, dsc);

            if (IsNormal)
            {
                dsc.AppendLine(TPNetManager.GetTeleport(Pos)?.Name);
            }
        }

        public override void OnBlockUnloaded()
        {
            base.OnBlockUnloaded();

            RemoveGameTickers();
        }


        public Vec3d RandomParticleInCirclePos(float radius = 2.5f)
        {
            Random rand = Api.World.Rand;

            double angle = rand.NextDouble() * Math.PI * 2f;
            return new Vec3d(
                Pos.X + Math.Cos(angle) * (radius - 1 / 16f) + 0.5f,
                Pos.Y,
                Pos.Z + Math.Sin(angle) * (radius - 1 / 16f) + 0.5f
            );
        }

        public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tessThreadTesselator)
        {
            mesher.AddMeshData(_frameMesh);
            return base.OnTesselation(mesher, tessThreadTesselator);
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);

            tree.SetItemstack("frameStack", _frameStack);
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
        {
            base.FromTreeAttributes(tree, worldAccessForResolve);

            _frameStack = tree.GetItemstack("frameStack");
        }

        public override void OnBlockBroken()
        {
            if (Api.Side == EnumAppSide.Client)
            {
                Core.HudCircleRenderer.CircleVisible = false;
            }

            base.OnBlockBroken();
        }
    }

    public static class TeleportState
    {
        public const string Broken = "broken";
        public const string Normal = "normal";

        public static string CheckValue(string value, string def = Broken)
        {
            if (value == Broken || value == Normal) return value;
            else return def;
        }
    }

    public enum EnumTeleportingEntityState
    {
        None = 0,
        Teleporting = 1,
        UI = 2
    }

    public class TeleportingPlayer
    {
        public EntityPlayer Player;
        public long LastCollideMs;
        public float SecondsPassed;
        public EnumTeleportingEntityState State;
    }
}
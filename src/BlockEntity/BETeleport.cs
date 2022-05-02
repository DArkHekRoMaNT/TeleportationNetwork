using System;
using System.Collections.Generic;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace TeleportationNetwork
{
    public class BETeleport : BlockEntity
    {
        public static AssetLocation DefaultFrameCode => new AssetLocation("game:stonebricks-granite");

        static readonly SimpleParticleProperties props = new SimpleParticleProperties()
        {
            MinQuantity = 0.3f,
            AddQuantity = 1.0f,
            Color = ColorUtil.ColorFromRgba(255, 215, 1, 255),
            MinPos = new Vec3d(),
            AddPos = new Vec3d(),
            MinVelocity = new Vec3f(),
            AddVelocity = new Vec3f(),
            LifeLength = 1f,
            GravityEffect = -0.1f,
            MinSize = 0.05f,
            MaxSize = 0.2f,
            ParticleModel = EnumParticleModel.Quad
        };


        public Core Core { get; private set; }
        public ITeleportManager TeleportManager { get; private set; }

        GuiDialogTeleport teleportDlg;
        GuiDialogRenameTeleport renameDlg;

        Dictionary<string, TeleportingPlayer> tpingPlayers;

        public bool Active { get; set; }
        public bool Enabled => Block is BlockNormalTeleport;

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
                AssetLocation shapeCode = new AssetLocation(Core.ModId, "shapes/block/teleport/frame.json");
                Shape frameShape = Api.Assets.Get<Shape>(shapeCode);
                capi.Tesselator.TesselateShape(_frameStack.Collectible, frameShape, out _frameMesh);
            }
        }


        long? animListenerId;
        BlockEntityAnimationUtil AnimUtil => GetBehavior<BEBehaviorAnimatable>()?.animUtil;

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);

            Core = api.ModLoader.GetModSystem<Core>();
            TeleportManager = api.ModLoader.GetModSystem<TeleportSystem>().Manager;
            tpingPlayers = new Dictionary<string, TeleportingPlayer>();

            if (api.Side == EnumAppSide.Server)
            {
                TeleportManager.GetOrCreateTeleport(Pos, Enabled);
            }

            if (_frameStack == null)
            {
                _frameStack = new ItemStack(api.World.GetBlock(DefaultFrameCode));
            }
            else
            {
                _frameStack.ResolveBlockOrItem(api.World);
            }
            UpdateFrameMesh();


            if (api.Side == EnumAppSide.Client)
            {
                float rotY = Block.Shape.rotateY;
                AnimUtil?.InitializeAnimator(Core.ModId + "-teleport", new Vec3f(0, rotY, 0));
            }

            if (Enabled)
            {
                SetupGameTickers();
            }
        }

        public override void OnLoadCollectibleMappings(IWorldAccessor worldForNewMappings, Dictionary<int, AssetLocation> oldBlockIdMapping, Dictionary<int, AssetLocation> oldItemIdMapping, int schematicSeed)
        {
            if (_frameStack == null)
            {
                _frameStack = new ItemStack(worldForNewMappings.GetBlock(DefaultFrameCode));
            }
            else
            {
                _frameStack.FixMapping(oldBlockIdMapping, oldItemIdMapping, worldForNewMappings);
            }

            base.OnLoadCollectibleMappings(worldForNewMappings, oldBlockIdMapping, oldItemIdMapping, schematicSeed);
        }

        private void SetupGameTickers()
        {
            animListenerId = RegisterGameTickListener(OnGameTick_Normal, 50);
        }

        private void RemoveGameTickers()
        {
            if (animListenerId != null) UnregisterGameTickListener((long)animListenerId);
        }

        private void OnGameTick_Normal(float dt)
        {
            // TODO Move to Init and change State?
            if (Api.Side == EnumAppSide.Client &&
               (AnimUtil.activeAnimationsByAnimCode.Count == 0 || AnimUtil.animator.ActiveAnimationCount == 0))
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
                        var player = Api.World.PlayerByUid(val.Key) as IServerPlayer;
                        ITeleport teleport = TeleportManager.GetTeleport(Pos);
                        TeleportManager.ActivateTeleport(teleport, player);
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
                    if (playerUID == (Api as ICoreClientAPI).World.Player.PlayerUID)
                    {
                        Core.HudCircleRenderer.CircleVisible = false;
                    }
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

                string playerUID = (Api as ICoreClientAPI).World.Player.PlayerUID;
                if (tpingPlayers.TryGetValue(playerUID, out var val))
                {
                    if (val.State == EnumTeleportingEntityState.Teleporting)
                    {
                        Core.HudCircleRenderer.CircleProgress = bestSecondsPassed / 3f;
                    }
                    else
                    {
                        Core.HudCircleRenderer.CircleVisible = false;
                    }
                }
            }
        }

        public void OnEntityCollide(Entity entity)
        {
            if (!Enabled || !(entity is EntityPlayer player)) return;

            if (player.IsActivityRunning(Core.ModId + "_teleportCooldown"))
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
                ITeleport teleport = TeleportManager.GetTeleport(Pos);
                if (teleport != null)
                {
                    TeleportManager.RemoveTeleport(teleport);
                }
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
                    renameDlg = new GuiDialogRenameTeleport(Pos, Api as ICoreClientAPI);
                }

                if (!renameDlg.IsOpened()) renameDlg.TryOpen();
            }
        }

        public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
        {
            base.GetBlockInfo(forPlayer, dsc);

            if (Enabled)
            {
                ITeleport teleport = TeleportManager.GetTeleport(Pos);
                if (teleport != null)
                {
                    dsc.AppendLine(teleport.Name);

                    if (forPlayer.WorldData.CurrentGameMode == EnumGameMode.Creative)
                    {
                        dsc.AppendLine("Neighbours:");
                        foreach (BlockPos nodePos in teleport.Neighbours)
                        {
                            string name = "null";
                            if (nodePos != null)
                            {
                                ITeleport node = TeleportManager.GetTeleport(nodePos);
                                if (node != null)
                                {
                                    name = node.Name;
                                }
                            }
                            dsc.AppendLine("*** " + name);
                        }
                    }
                }
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
    }

    public enum EnumTeleportingEntityState
    {
        None = 0,
        Teleporting = 1,
        UI = 2
    }

    public class TeleportingPlayer
    {
        public EntityPlayer Player { get; set; }
        public long LastCollideMs { get; set; }
        public float SecondsPassed { get; set; }
        public EnumTeleportingEntityState State { get; set; }
    }
}
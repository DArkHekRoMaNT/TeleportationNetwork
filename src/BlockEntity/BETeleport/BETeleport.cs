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

        public bool Active { get; set; }
        public bool Enabled => Block is BlockNormalTeleport;
        public ITeleportManager TeleportManager { get; private set; }

        TeleportRenderer renderer;
        TeleportParticleManager particleManager;

        float activeStage;
        Dictionary<string, TeleportingPlayer> activePlayers;

        GuiDialogTeleport teleportDlg;
        GuiDialogRenameTeleport renameDlg;

        long? animListenerId;
        BlockEntityAnimationUtil AnimUtil => GetBehavior<BEBehaviorAnimatable>()?.animUtil;

        MeshData frameMesh;
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

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);

            activePlayers = new Dictionary<string, TeleportingPlayer>();
            TeleportManager = api.ModLoader.GetModSystem<TeleportSystem>().Manager;

            if (_frameStack == null)
            {
                _frameStack = new ItemStack(api.World.GetBlock(DefaultFrameCode));
            }

            if (api.Side == EnumAppSide.Server)
            {
                TeleportManager.GetOrCreateTeleport(Pos, Enabled);
            }
            else
            {
                particleManager = new TeleportParticleManager(api as ICoreClientAPI, Pos);
                renderer = new TeleportRenderer(Pos, api as ICoreClientAPI);
                UpdateFrameMesh();

                float rotY = Block.Shape.rotateY;
                AnimUtil?.InitializeAnimator(Core.ModId + "-teleport", new Vec3f(0, rotY, 0));

                if (AnimUtil.activeAnimationsByAnimCode.Count == 0 ||
                    AnimUtil.animator.ActiveAnimationCount == 0)
                {
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
            }

            if (Enabled)
            {
                animListenerId = RegisterGameTickListener(OnGameTick, 50);
            }
        }

        public void OnEntityCollide(Entity entity)
        {
            if (Enabled && entity is EntityPlayer player)
            {
                if (player.IsActivityRunning(Core.ModId + "_teleportCooldown"))
                {
                    return;
                }

                if (!activePlayers.TryGetValue(player.PlayerUID, out TeleportingPlayer tpe))
                {
                    activePlayers[player.PlayerUID] = tpe = new TeleportingPlayer()
                    {
                        Player = player,
                        State = EnumTeleportingEntityState.None
                    };
                }

                tpe.LastCollideMs = Api.World.ElapsedMilliseconds;
                Active = true;
            }
        }

        private void OnGameTick(float dt)
        {
            if (Api.Side == EnumAppSide.Client)
            {
                particleManager.SpawnSealEdgeParticle();
                renderer.Speed = (float)(1 + Math.Exp(activeStage) * 1f);
                renderer.Progress = activeStage;
            }

            if (Active)
            {
                CheckActivePlayers(dt);

                if (Api.Side == EnumAppSide.Client)
                {
                    particleManager.SpawnActiveParticles();
                }
            }
        }

        private void CheckActivePlayers(float dt)
        {
            List<string> toRemove = new List<string>();

            float maxSecondsPassed = 0;
            foreach (var val in activePlayers)
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

                if (val.Value.SecondsPassed > Constants.BeforeTeleportShowGUITime && val.Value.State == EnumTeleportingEntityState.Teleporting)
                {
                    val.Value.State = EnumTeleportingEntityState.UI;

                    if (Api.Side == EnumAppSide.Client && teleportDlg?.IsOpened() != true)
                    {
                        if (teleportDlg != null) teleportDlg.Dispose();

                        teleportDlg = new GuiDialogTeleport(Api as ICoreClientAPI, Pos);
                        teleportDlg.TryOpen();
                    }
                }

                maxSecondsPassed = Math.Max(val.Value.SecondsPassed, maxSecondsPassed);
            }

            foreach (var playerUID in toRemove)
            {
                activePlayers.Remove(playerUID);

                if (Api.Side == EnumAppSide.Client)
                {
                    teleportDlg?.TryClose();
                }
            }

            Active = activePlayers.Count > 0;
            activeStage = Math.Min(1, maxSecondsPassed / Constants.BeforeTeleportShowGUITime);
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

        private void UpdateFrameMesh()
        {
            if (Api is ICoreClientAPI capi)
            {
                AssetLocation shapeCode = new AssetLocation(Core.ModId, "shapes/block/teleport/frame.json");
                Shape frameShape = Api.Assets.Get<Shape>(shapeCode);
                capi.Tesselator.TesselateShape(_frameStack.Collectible, frameShape, out frameMesh);
            }
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
            renderer?.Dispose();
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

            renderer?.Dispose();
        }

        public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tessThreadTesselator)
        {
            mesher.AddMeshData(frameMesh);
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
            _frameStack.ResolveBlockOrItem(worldAccessForResolve);
        }
    }
}
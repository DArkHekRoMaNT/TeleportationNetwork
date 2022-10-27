using System;
using System.Collections.Generic;
using System.IO;
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
        public static AssetLocation DefaultFrameCode => new("game:stonebricks-granite");

        public bool Active { get; set; }
        private Dictionary<string, TeleportingPlayerData> ActivePlayers { get; } = new();
        private float _activeStage;

        public bool Repaired => (Block as BlockTeleport)?.IsNormal ?? false;

        private GuiDialogTeleportList? _teleportDlg;
        private GuiDialogRenameTeleport? _renameDlg;

        private BlockEntityAnimationUtil? AnimUtil
            => GetBehavior<BEBehaviorAnimatable>()?.animUtil;

        public TeleportManager TeleportManager { get; private set; } = null!;
        private SealRenderer SealRenderer { get; set; } = null!;
        private TeleportParticleController ParticleController { get; set; } = null!;


        private MeshData? _frameMesh;
        private ItemStack _frameStack = null!;
        public ItemStack FrameStack
        {
            get => _frameStack;
            set
            {
                _frameStack = value;
                UpdateFrameMesh();
                MarkDirty(true);
            }
        }

        public Teleport Teleport
        {
            get
            {
                Teleport? teleport = TeleportManager.Points[Pos];

                if (teleport == null)
                {
                    CreateTeleport();
                    return TeleportManager.Points[Pos]!;
                }

                return teleport;
            }
        }


        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);

            _frameStack ??= new ItemStack(api.World.GetBlock(DefaultFrameCode));
            TeleportManager = api.ModLoader.GetModSystem<TeleportManager>();

            if (api.Side == EnumAppSide.Server)
            {
                CreateTeleport();
            }

            if (api.Side == EnumAppSide.Client)
            {
                ParticleController = new TeleportParticleController((ICoreClientAPI)api, Pos);
                SealRenderer = new SealRenderer(Pos, (ICoreClientAPI)api);

                UpdateFrameMesh();
                InitAnimator();
            }

            RegisterGameTickListener(OnGameTick, 50);
        }

        public void OnEntityCollide(Entity entity)
        {
            if (Repaired && entity is EntityPlayer player)
            {
                if (player.IsActivityRunning(Core.ModId + "_teleportCooldown"))
                {
                    return;
                }

                if (!ActivePlayers.TryGetValue(player.PlayerUID, out TeleportingPlayerData tpe))
                {
                    ActivePlayers[player.PlayerUID] = tpe = new TeleportingPlayerData(player);
                }

                tpe.LastCollideMs = Api.World.ElapsedMilliseconds;
                Active = true;
            }
        }

        private void OnGameTick(float dt)
        {
            if (Repaired)
            {
                if (Api.Side == EnumAppSide.Client)
                {
                    ParticleController.SpawnSealEdgeParticle();
                    SealRenderer.Enabled = true;
                    SealRenderer.Speed = (float)(1 + Math.Exp(_activeStage) * 1f);
                    SealRenderer.Progress = _activeStage;
                }

                if (Active)
                {
                    CheckActivePlayers(dt);

                    if (Api.Side == EnumAppSide.Client)
                    {
                        ParticleController.SpawnActiveParticles();
                    }
                }
            }
            else
            {
                if (Api.Side == EnumAppSide.Client)
                {
                    SealRenderer.Enabled = false;
                }
            }
        }

        private void CheckActivePlayers(float dt)
        {
            var toRemove = new List<string>();

            float maxSecondsPassed = 0;
            foreach (var activePlayer in ActivePlayers)
            {
                if (activePlayer.Value.State == TeleportingPlayerData.EnumState.None)
                {
                    ActivateTeleportByPlayer(activePlayer.Key);
                    activePlayer.Value.State = TeleportingPlayerData.EnumState.Active;
                }

                activePlayer.Value.SecondsPassed += Math.Min(0.5f, dt);

                if (Api.World.ElapsedMilliseconds - activePlayer.Value.LastCollideMs > 300)
                {
                    toRemove.Add(activePlayer.Key);
                    continue;
                }

                if (activePlayer.Value.SecondsPassed > Constants.BeforeTeleportShowGUITime &&
                    activePlayer.Value.State == TeleportingPlayerData.EnumState.Active)
                {
                    activePlayer.Value.State = TeleportingPlayerData.EnumState.UI;

                    if (Api.Side == EnumAppSide.Client && _teleportDlg?.IsOpened() != true)
                    {
                        _teleportDlg?.Dispose();

                        _teleportDlg = new GuiDialogTeleportList((ICoreClientAPI)Api, Pos);
                        _teleportDlg.TryOpen();
                    }
                }

                maxSecondsPassed = Math.Max(activePlayer.Value.SecondsPassed, maxSecondsPassed);
            }

            foreach (var playerUID in toRemove)
            {
                ActivePlayers.Remove(playerUID);

                if (Api.Side == EnumAppSide.Client)
                {
                    _teleportDlg?.TryClose();
                }
            }

            Active = ActivePlayers.Count > 0;
            _activeStage = Math.Min(1, maxSecondsPassed / Constants.BeforeTeleportShowGUITime);
        }

        public void ActivateTeleportByPlayer(string playerUID)
        {
            if (!Teleport.ActivatedByPlayers.Contains(playerUID))
            {
                Teleport.ActivatedByPlayers.Add(playerUID);
                TeleportManager.Points.MarkDirty(Pos);
            }
        }

        public void CreateTeleport()
        {
            if (!TeleportManager.Points.Contains(Pos))
            {
                if (Api.Side == EnumAppSide.Client)
                {
                    Core.ModLogger.Error("Creating teleport on client side!");
                }

                string name = TeleportManager.GetRandomName();
                var teleport = new Teleport(Pos, name, Repaired);
                TeleportManager.Points.Set(teleport);
            }
        }

        public void OpenRenameDlg()
        {
            if (Api.Side == EnumAppSide.Client)
            {
                _renameDlg ??= new GuiDialogRenameTeleport(Pos, (ICoreClientAPI)Api);

                if (!_renameDlg.IsOpened())
                {
                    _renameDlg.TryOpen();
                }
            }
        }

        public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tessThreadTesselator)
        {
            mesher.AddMeshData(_frameMesh);
            return base.OnTesselation(mesher, tessThreadTesselator);
        }

        public override void OnReceivedClientPacket(IPlayer fromPlayer, int packetid, byte[] data)
        {
            base.OnReceivedClientPacket(fromPlayer, packetid, data);

            if (packetid == Constants.ChangeTeleportNamePacketId)
            {
                if (Api.World.Claims.TryAccess(fromPlayer, Pos, EnumBlockAccessFlags.Use))
                {
                    Teleport.Name = Encoding.UTF8.GetString(data);
                    TeleportManager.Points.MarkDirty(Pos);
                }
            }

            if (packetid == Constants.TeleportToPacketId)
            {
                using var ms = new MemoryStream(data);
                using var reader = new BinaryReader(ms);
                {
                    string playerUID = reader.ReadString();
                    BlockPos targetPoint = BlockPos.CreateFromBytes(reader);

                    if (Api.World.PlayerByUid(playerUID) is IServerPlayer player)
                    {
                        Vec3d startPoint = Pos.ToVec3d().AddCopy(.5, -1, .5);
                        TeleportUtil.AreaTeleportTo(player, startPoint, targetPoint, Constants.SealRadius);
                    }
                }
            }
        }

        public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
        {
            base.GetBlockInfo(forPlayer, dsc);

            if (Repaired)
            {
                dsc.AppendLine(Teleport.Name);

                if (forPlayer.WorldData.CurrentGameMode == EnumGameMode.Creative)
                {
                    dsc.AppendLine("Neighbours:");
                    foreach (Teleport node in Teleport.Neighbours)
                    {
                        dsc.AppendLine("*** " + node.Name);
                    }
                }
            }
        }

        public override void OnBlockUnloaded()
        {
            base.OnBlockUnloaded();
            SealRenderer?.Dispose();
        }

        public override void OnBlockRemoved()
        {
            base.OnBlockRemoved();

            if (Api.Side == EnumAppSide.Server)
            {
                TeleportManager.Points.Remove(Pos);
            }

            SealRenderer?.Dispose();
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
            UpdateFrameMesh();
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
                var shapeCode = new AssetLocation(Core.ModId, "shapes/block/teleport/frame.json");
                Shape frameShape = Api.Assets.Get<Shape>(shapeCode);
                capi.Tesselator.TesselateShape(_frameStack.Collectible, frameShape, out _frameMesh);
            }
        }

        private void InitAnimator()
        {
            if (AnimUtil != null && Api.Side == EnumAppSide.Client)
            {
                if (AnimUtil.animator != null)
                {
                    AnimUtil.Dispose();
                    GetBehavior<BEBehaviorAnimatable>().animUtil = new(Api, this);
                }

                float rotY = Block.Shape.rotateY;
                AnimUtil.InitializeAnimator(Core.ModId + "-teleport", new Vec3f(0, rotY, 0));

                if (AnimUtil.activeAnimationsByAnimCode.Count == 0 ||
                    AnimUtil.animator!.ActiveAnimationCount == 0)
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
        }

        // hack, not call then BlockEntity.MarkDirty calls!
        /// <inheritdoc cref="BlockEntity.MarkDirty(bool, IPlayer)"/>
        public new void MarkDirty(bool redrawOnClient = false, IPlayer? skipPlayer = null)
        {
            if (redrawOnClient)
            {
                UpdateFrameMesh();
                InitAnimator();
            }

            CreateTeleport();
            Teleport.Enabled = Repaired;
            TeleportManager.Points.MarkDirty(Pos);

            base.MarkDirty(redrawOnClient, skipPlayer);
        }
    }
}

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
    public class BlockEntityTeleport : BlockEntity
    {
        public static AssetLocation DefaultFrameCode => new("game:stonebricks-granite");

        private float _activeTime;
        private Dictionary<string, TeleportingPlayerData> ActivePlayers { get; } = [];

        private GuiDialogTeleportList? _teleportDlg;
        private GuiDialogEditTeleport? _editDlg;

        private BlockEntityAnimationUtil? AnimUtil => GetBehavior<BEBehaviorAnimatable>()?.animUtil;

        private SealRenderer? SealRenderer { get; set; }
        private NewTeleportRenderer? TeleportRiftRenderer { get; set; }

        private TeleportParticleController? ParticleController => (Block as BlockTeleport)?.ParticleController;

        private ILoadedSound? _sound;
        private float _soundVolume;
        private float _soundPith;

        private MeshData? _frameMesh;
        private ItemStack? _frameStack;

        private ILogger? _modLogger;

        public bool Active { get; set; }
        public bool Repaired => (Block as BlockTeleport)?.IsNormal ?? false;

        public TeleportManager TeleportManager { get; private set; } = null!;
        public ItemStack? FrameStack
        {
            get => _frameStack;
            set
            {
                _frameStack = value;
                Update();
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

        public ILogger ModLogger => _modLogger ?? Api.Logger;

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);

            // Legacy add side at 1.19.0-rc.5
            if (Block.Code == new AssetLocation("tpnet:teleport-normal") ||
                Block.Code == new AssetLocation("tpnet:teleport-broken"))
            {
                var sidedBlock = api.World.GetBlock(Block.Code.WithPathAppendix("-north"));
                api.World.BlockAccessor.ExchangeBlock(sidedBlock.Id, Pos);
                api.World.BlockAccessor.MarkBlockDirty(Pos);
            }

            _modLogger = api.ModLoader.GetModSystem<Core>().Mod.Logger;

            _frameStack ??= new ItemStack(api.World.GetBlock(DefaultFrameCode));
            TeleportManager = api.ModLoader.GetModSystem<TeleportManager>();

            if (api.Side == EnumAppSide.Server)
            {
                CreateTeleport();
            }

            if (api is ICoreClientAPI capi)
            {
                //SealRenderer = new SealRenderer(Pos, capi);
                TeleportRiftRenderer = new NewTeleportRenderer(Pos, capi);

                _sound = capi.World.LoadSound(new SoundParams
                {
                    Location = new AssetLocation("sounds/effect/translocate-idle.ogg"),
                    ShouldLoop = true,
                    Position = Pos.ToVec3f().AddCopy(.5f, 1, .5f),
                    RelativePosition = false,
                    DisposeOnFinish = false,
                    Volume = 0
                });

                UpdateFrameMesh();
                UpdateAnimator();
            }

            RegisterGameTickListener(OnGameTick, 50);
        }

        public void OnEntityCollide(Entity entity)
        {
            if (Repaired && entity is EntityPlayer player)
            {
                if (player.IsActivityRunning($"{Constants.ModId}_teleportCooldown"))
                {
                    return;
                }

                if (!ActivePlayers.TryGetValue(player.PlayerUID, out TeleportingPlayerData? tpe))
                {
                    ActivePlayers[player.PlayerUID] = tpe = new TeleportingPlayerData(player);
                }

                tpe.LastCollideMs = Api.World.ElapsedMilliseconds;
                Active = true;
            }
        }

        private void OnGameTick(float dt)
        {
            if (Repaired && Active)
            {
                CheckActivePlayers(dt);
            }

            if (Api.Side == EnumAppSide.Client)
            {
                if (Repaired)
                {
                    if (_sound?.IsPlaying == false)
                    {
                        _sound.Start();
                    }

                    ParticleController?.SpawnSealEdgeParticle(Pos);

                    if (SealRenderer != null)
                    {
                        SealRenderer.Enabled = true;
                        SealRenderer.Speed = (float)(1 + Math.Exp(_activeTime) * 1f);
                    }

                    if (Active)
                    {

                        if (TeleportRiftRenderer != null)
                        {
                            TeleportRiftRenderer.Enabled = true;
                            TeleportRiftRenderer.Speed = Math.Clamp(_activeTime, 0, 1);
                        }

                        ParticleController?.SpawnActiveParticles(Pos, _activeTime);

                        _soundVolume = Math.Min(1f, _soundVolume + dt / 3);
                        _soundPith = Math.Min(1.5f, _soundPith + dt / 3);
                    }
                    else
                    {

                        if (TeleportRiftRenderer != null)
                        {
                            TeleportRiftRenderer.Enabled = false;
                            TeleportRiftRenderer.Speed = 0;
                        }

                        _soundVolume = Math.Max(0.5f, _soundVolume - dt);
                        _soundPith = Math.Max(0.5f, _soundPith - dt);
                    }

                    _sound?.SetVolume(_soundVolume);
                    _sound?.SetPitch(_soundPith);
                }
                else
                {
                    if (SealRenderer != null)
                    {
                        SealRenderer.Enabled = false;
                    }
                    if (TeleportRiftRenderer != null)
                    {
                        TeleportRiftRenderer.Enabled = false;
                    }
                    _sound?.Stop();
                }
            }
        }

        private void CheckActivePlayers(float dt)
        {
            var toRemove = new List<string>();

            float maxSecondsPassed = 0;
            foreach (KeyValuePair<string, TeleportingPlayerData> activePlayer in ActivePlayers)
            {
                if (activePlayer.Value.State == TeleportingPlayerData.EnumState.None)
                {
                    ActivateTeleportByPlayer(activePlayer.Key);
                    activePlayer.Value.State = TeleportingPlayerData.EnumState.Active;
                }

                activePlayer.Value.SecondsPassed += Math.Min(0.5f, dt);

                if (Api.World.ElapsedMilliseconds - activePlayer.Value.LastCollideMs > 300)
                {
                    // Make sure its not just server lag (from BlockEntity/BETeleporter.cs)
                    Block block = Api.World.CollisionTester.GetCollidingBlock(Api.World.BlockAccessor,
                        activePlayer.Value.Player.SelectionBox, activePlayer.Value.Player.Pos.XYZ, true);

                    // Check what is not other teleport
                    bool otherTp = activePlayer.Value.Player.Pos.AsBlockPos.DistanceTo(Pos) > 2;

                    if (block is not BlockTeleport || otherTp)
                    {
                        toRemove.Add(activePlayer.Key);
                        continue;
                    }
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

            foreach (string playerUID in toRemove)
            {
                ActivePlayers.Remove(playerUID);

                if (Api.Side == EnumAppSide.Client)
                {
                    _teleportDlg?.TryClose();
                }
            }

            Active = ActivePlayers.Count > 0;
            _activeTime = Math.Min(1, maxSecondsPassed / Constants.BeforeTeleportShowGUITime);
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
                    ModLogger.Error("Creating teleport on client side!");
                }

                string name = TeleportManager.NameGenerator.Next();
                var teleport = new Teleport(Pos, name, Repaired);
                TeleportManager.Points.Set(teleport);
            }
        }

        public void OpenEditDialog()
        {
            if (Api is ICoreClientAPI capi)
            {
                _editDlg ??= new GuiDialogEditTeleport(capi, Pos);

                if (!_editDlg.IsOpened())
                {
                    _editDlg.TryOpen();
                }
            }
        }

        public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tessThreadTesselator)
        {
            if (_frameMesh != null)
            {
                mesher.AddMeshData(_frameMesh);
            }

            return base.OnTesselation(mesher, tessThreadTesselator);
        }

        public override void OnReceivedClientPacket(IPlayer fromPlayer, int packetid, byte[] data)
        {
            base.OnReceivedClientPacket(fromPlayer, packetid, data);

            if (packetid == Constants.TeleportPlayerPacketId)
            {
                using var ms = new MemoryStream(data);
                using var reader = new BinaryReader(ms);
                {
                    BlockPos targetPoint = BlockPos.CreateFromBytes(reader);
                    if (fromPlayer is IServerPlayer player)
                    {
                        Vec3d startPoint = Pos.ToVec3d().AddCopy(.5, 1, .5);
                        TeleportUtil.AreaTeleportToPoint(player, startPoint, targetPoint, Constants.SealRadius,
                        ModLogger, (entity) =>
                        {
                            // one per tp
                            if (entity is EntityPlayer entityPlayer && entityPlayer.PlayerUID == player.PlayerUID)
                            {
                                var soundLoc = new AssetLocation("sounds/effect/translocate-breakdimension.ogg");
                                entity.World.PlaySoundAt(soundLoc, entity, null, true, 32, .5f);
                            }
                           ((ICoreServerAPI)Api).Network.BroadcastBlockEntityPacket(
                               targetPoint,
                               Constants.EntityTeleportedPacketId,
                               BitConverter.GetBytes(entity.EntityId));
                        });
                    }
                }
            }
        }

        public override void OnReceivedServerPacket(int packetid, byte[] data)
        {
            base.OnReceivedServerPacket(packetid, data);

            if (packetid == Constants.EntityTeleportedPacketId)
            {
                Entity entity = Api.World.GetEntityById(BitConverter.ToInt64(data, 0));
                if (entity != null)
                {
                    ParticleController?.SpawnTeleportParticles(entity);
                }
            }
        }

        public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
        {
            base.GetBlockInfo(forPlayer, dsc);

            if (Repaired)
            {
                dsc.AppendLine(Teleport.Name);
            }

            if (forPlayer.WorldData.CurrentGameMode == EnumGameMode.Creative)
            {
                dsc.AppendLine($"Frame code: {_frameStack?.Collectible?.Code}");
                dsc.AppendLine("Neighbours:");
                foreach (Teleport node in Teleport.Neighbours)
                {
                    dsc.AppendLine($"*** {node.Name}");
                }
            }
        }

        public override void OnBlockUnloaded()
        {
            base.OnBlockUnloaded();
            SealRenderer?.Dispose();
            TeleportRiftRenderer?.Dispose();
            _sound?.Dispose();
        }

        public override void OnBlockRemoved()
        {
            base.OnBlockRemoved();

            if (Api.Side == EnumAppSide.Server)
            {
                TeleportManager.Points.Remove(Pos);
            }

            SealRenderer?.Dispose();
            TeleportRiftRenderer?.Dispose();
            _sound?.Dispose();
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);
            tree.SetString("frameCode", _frameStack?.Collectible.Code.ToString());
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
        {
            base.FromTreeAttributes(tree, worldAccessForResolve);

            string? frame = tree.GetString("frameCode");
            if (frame != null)
            {
                Block? block = worldAccessForResolve.GetBlock(new AssetLocation(frame));
                if (block != null)
                {
                    _frameStack = new ItemStack(block);
                }
            }
            else // Legacy 1.12
            {
                _frameStack = tree.GetItemstack("frameStack");
                _frameStack?.ResolveBlockOrItem(worldAccessForResolve);
            }

            UpdateFrameMesh();
        }

        private void UpdateFrameMesh()
        {
            if (Api is ICoreClientAPI capi && _frameStack != null)
            {
                var facing = BlockFacing.FromCode(Block.LastCodePart()) ?? BlockFacing.NORTH;
                float rotation = facing.HorizontalAngleIndex * 90 - 90;
                var shapeCode = new AssetLocation(Constants.ModId, "shapes/block/teleport/frame.json");
                Shape frameShape = Api.Assets.Get<Shape>(shapeCode);
                capi.Tesselator.TesselateShape(_frameStack.Collectible, frameShape, out _frameMesh, new Vec3f(0, rotation, 0));
            }
        }

        private void UpdateAnimator()
        {
            if (AnimUtil != null && Api.Side == EnumAppSide.Client)
            {
                if (AnimUtil.animator != null)
                {
                    AnimUtil.Dispose();
                    GetBehavior<BEBehaviorAnimatable>().animUtil = new(Api, this);
                }

                if (Repaired)
                {
                    float rotY = Block.Shape.rotateY;
                    AnimUtil.InitializeAnimator($"{Constants.ModId}-teleport", null, null, new Vec3f(0, rotY, 0));

                    if (AnimUtil.activeAnimationsByAnimCode.Count == 0 ||
                        AnimUtil.animator!.ActiveAnimationCount == 0)
                    {
                        AnimUtil.StartAnimation(new AnimationMetaData
                        {
                            Animation = "largegears",
                            Code = "largegears",
                            AnimationSpeed = 25f,
                            EaseInSpeed = float.MaxValue,
                            EaseOutSpeed = float.MaxValue
                        });

                        AnimUtil.StartAnimation(new AnimationMetaData
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
        }

        public void Update()
        {
            if (Api != null)
            {
                if (Api.Side == EnumAppSide.Client)
                {
                    UpdateFrameMesh();
                    UpdateAnimator();
                }

                CreateTeleport();
                Teleport.Enabled = Repaired;
                TeleportManager.Points.MarkDirty(Pos);

                MarkDirty(true);
            }
        }
    }
}

using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;
using static Microsoft.WindowsAPICodePack.Shell.PropertySystem.SystemProperties.System;

namespace TeleportationNetwork
{
    public class BlockEntityTeleport : BlockEntity
    {
        [MemberNotNullWhen(true, nameof(_currentTargetTeleportPos))]
        public bool Active => _currentTargetTeleportPos != null && _activationTime > Constants.TeleportActivationTime;
        public bool Repaired => (Block as BlockTeleport)?.IsNormal ?? false;
        public float Size => (Block as BlockTeleport)?.Variant["type"] == "smallgate" ? 5f : 10f; //TODO: Move to attributes

        private GuiDialogTeleportList? _teleportDlg;
        private GuiDialogEditTeleport? _editDlg;

        private BlockEntityAnimationUtil? AnimUtil => GetBehavior<BEBehaviorAnimatable>()?.animUtil;

        private NewTeleportRenderer? TeleportRiftRenderer { get; set; }

        private TeleportParticleController? ParticleController => (Block as BlockTeleport)?.ParticleController;
        private TeleportManager TeleportManager { get; set; } = null!;

        private ILogger? _modLogger;
        private ILoadedSound? _sound;
        private float _soundVolume;
        private float _soundPith;

        private float _activationTime;
        private BlockPos? _currentTargetTeleportPos;

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

            _modLogger = api.ModLoader.GetModSystem<Core>().Mod.Logger;
            TeleportManager = api.ModLoader.GetModSystem<TeleportManager>();

            if (api.Side == EnumAppSide.Server)
            {
                CreateTeleport();
            }

            if (api is ICoreClientAPI capi)
            {
                TeleportRiftRenderer = new NewTeleportRenderer(Pos, capi, Block.LastCodePart() switch
                {
                    "north" => 0,
                    "west" => 90,
                    "south" => 180,
                    "east" => 270,
                    _ => 0
                }, Size);

                _sound = capi.World.LoadSound(new SoundParams
                {
                    Location = new AssetLocation("sounds/effect/translocate-idle.ogg"),
                    ShouldLoop = true,
                    Position = Pos.ToVec3f().AddCopy(.5f, 1, .5f),
                    RelativePosition = false,
                    DisposeOnFinish = false,
                    Volume = 0
                });

                UpdateAnimator();
            }

            RegisterGameTickListener(OnGameTick, 50);
        }

        private void OnGameTick(float dt)
        {
            if (!Repaired) return;

            if (_currentTargetTeleportPos != null)
                _activationTime += dt;

            TeleportRiftRenderer?.SetActivationProgress(_activationTime / Constants.TeleportActivationTime);

            UpdateSound(dt);

            if (!Active) return;

            var target = TeleportManager.Points[_currentTargetTeleportPos];
            if (target == null)
            {
                _currentTargetTeleportPos = null;
                return;
            }

            if (Api is ICoreServerAPI sapi)
            {
                static Vec3d ToGateCenter(BlockPos pos, BlockFacing facing, float size)
                {
                    return pos.ToVec3d().Add(0.5) - facing.Normalf.ToVec3d().Mul(0.25 * (size / 5f));
                }

                var orientation = BlockFacing.FromCode(Block.LastCodePart());
                var center = ToGateCenter(Pos, orientation, Size);
                var entities = MathUtil.GetInCyllinderEntities(Api, Size / 2f, 0.5f, center, orientation);

                if (entities.Length > 0)
                {
                    ModLogger.Notification($"Entities {string.Join(", ", entities.Select(x => x.GetName()))} at {Api.Side}");
                    if (Api.World.BlockAccessor.GetBlock(target.Pos) is not BlockTeleport targetBlock ||
                        Api.World.BlockAccessor.GetBlockEntity(target.Pos) is not BlockEntityTeleport targetBE)
                    {
                        _currentTargetTeleportPos = null;
                        return;
                    }

                    var targetOrientation = BlockFacing.FromCode(targetBlock.LastCodePart());
                    var targetCenter = ToGateCenter(target.Pos, targetOrientation, targetBE.Size);
                    foreach (var entity in entities)
                    {
                        if (entity.IsActivityRunning(Constants.TeleportCooldownActivityName))
                            continue;

                        var point = targetCenter;
                        point += entity.Pos.XYZ - center;
                        point -= targetOrientation.Normalf.ToVec3d();

                        var entityPos = entity.Pos.Copy();
                        entityPos.SetPos(point);
                        if (orientation.IsHorizontal && targetOrientation.IsHorizontal)
                        {
                            var diff = orientation.Index - targetOrientation.Index;
                            entityPos.Yaw += diff * GameMath.PIHALF;
                            if (entity is EntityPlayer playerEntity && playerEntity.Player is IServerPlayer serverPlayer)
                            {
                                sapi.Network.SendBlockEntityPacket(serverPlayer, Pos, Constants.PlayerTeleportedPacketId, [(byte)diff]);
                            }
                        }

                        TeleportUtil.StabilityRelatedTeleportTo(entity, entityPos, ModLogger, () => AfterTeleportEntity(entity));
                        ModLogger.Notification($"{entity?.GetName()} teleported to {point} ({target.Name})");
                    }
                }
            }
        }

        private void AfterTeleportEntity(Entity entity)
        {
            var soundLoc = new AssetLocation("sounds/effect/translocate-breakdimension.ogg");
            entity.World.PlaySoundAt(soundLoc, entity, null, true, 32, .5f);
            ((ICoreServerAPI)Api).Network.BroadcastBlockEntityPacket(
                _currentTargetTeleportPos,
                Constants.EntityTeleportedPacketId,
                BitConverter.GetBytes(entity.EntityId));
        }

        private void UpdateSound(float dt)
        {
            if (Api.Side == EnumAppSide.Client)
            {
                if (_sound?.IsPlaying == false)
                {
                    _sound.Start();
                }

                if (Active)
                {
                    _soundVolume = Math.Min(1f, _soundVolume + dt / 3);
                    _soundPith = Math.Min(1.5f, _soundPith + dt / 3);
                }
                else
                {
                    _soundVolume = Math.Max(0.5f, _soundVolume - dt);
                    _soundPith = Math.Max(0.5f, _soundPith - dt);
                }

                _sound?.SetVolume(_soundVolume);
                _sound?.SetPitch(_soundPith);
            }
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

        public void OpenTeleportDialog()
        {
            if (Api is ICoreClientAPI capi)
            {
                _teleportDlg ??= new GuiDialogTeleportList(capi, Pos);
                if (!_teleportDlg.IsOpened())
                {
                    _teleportDlg.TryOpen();
                }
            }
        }

        public override void OnReceivedClientPacket(IPlayer fromPlayer, int packetid, byte[] data)
        {
            base.OnReceivedClientPacket(fromPlayer, packetid, data);

            if (packetid == Constants.OpenTeleportPacketId)
            {
                using var ms = new MemoryStream(data);
                using var reader = new BinaryReader(ms);
                _currentTargetTeleportPos = BlockPos.CreateFromBytes(reader);
                _activationTime = 0;
                (Api as ICoreServerAPI)?.Network.BroadcastBlockEntityPacket(Pos, Constants.OpenTeleportPacketId, data);
            }
        }

        public override void OnReceivedServerPacket(int packetid, byte[] data)
        {
            base.OnReceivedServerPacket(packetid, data);

            if (packetid == Constants.EntityTeleportedPacketId)
            {
                var entity = Api.World.GetEntityById(BitConverter.ToInt64(data, 0));
                if (entity != null)
                {
                    ParticleController?.SpawnTeleportParticles(entity);
                }
            }

            else if (packetid == Constants.PlayerTeleportedPacketId)
            {
                if (Api is ICoreClientAPI capi)
                {
                    var addYaw = (2f + data[0]) * GameMath.PIHALF;
                    capi.Input.MouseYaw += addYaw;
                    capi.World.Player.Entity.BodyYaw += addYaw * 4f;
                    capi.World.Player.Entity.WalkYaw += addYaw * 4f;
                    capi.World.Player.Entity.Pos.Yaw += addYaw * 4f;
                }
            }

            else if (packetid == Constants.OpenTeleportPacketId)
            {
                using var ms = new MemoryStream(data);
                using var reader = new BinaryReader(ms);
                _currentTargetTeleportPos = BlockPos.CreateFromBytes(reader);
                _activationTime = 0;
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

            TeleportRiftRenderer?.Dispose();
            _sound?.Dispose();
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);
            if (_currentTargetTeleportPos != null)
                tree.SetBlockPos("activeTeleport", _currentTargetTeleportPos);
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
        {
            base.FromTreeAttributes(tree, worldAccessForResolve);
            _currentTargetTeleportPos = tree.GetBlockPos("activeTeleport", null);
        }

        private void UpdateAnimator()
        {
            return;

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

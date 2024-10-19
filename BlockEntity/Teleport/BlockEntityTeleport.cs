using System;
using System.IO;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.Client.NoObf;
using Vintagestory.GameContent;

namespace TeleportationNetwork
{
    public class BlockEntityTeleport : BlockEntity
    {
        private ILogger Logger => _modLogger ?? Api.Logger;
        private TeleportParticleController? ParticleController => (Block as BlockTeleport)?.ParticleController;
        private TeleportActivator Status { get; } = new();
        private BlockEntityAnimationUtil? AnimUtil => GetBehavior<BEBehaviorAnimatable>()?.animUtil;

        private TeleportManager _manager = null!;
        private TeleportRiftRenderer? _teleportRiftRenderer;
        private ILogger? _modLogger;
        private ILoadedSound? _sound;
        private float _soundVolume;
        private float _soundPith;
        private BlockPos? _lastTargetPos;
        private GuiDialogTeleportList? _teleportDialog;
        private GuiDialogEditTeleport? _editDialog;

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);

            _modLogger = api.ModLoader.GetModSystem<Core>().Mod.Logger;
            _manager = api.ModLoader.GetModSystem<TeleportManager>();

            Status.StateChanged += (_, next) => UpdateState(next);

            if (api is ICoreClientAPI capi)
            {
                _teleportRiftRenderer = new TeleportRiftRenderer(Pos, capi, Block.Shape.rotateY);

                _sound = capi.World.LoadSound(new SoundParams
                {
                    Location = new AssetLocation("sounds/effect/translocate-idle.ogg"), //TODO: Change sound
                    ShouldLoop = true,
                    Position = Pos.ToVec3f().AddCopy(.5f, 1, .5f),
                    RelativePosition = false,
                    DisposeOnFinish = false,
                    Volume = 0
                });

                UpdateAnimator();
                UpdateState(Status.State);
            }

            var teleport = GetOrCreateTeleport();
            _teleportRiftRenderer?.Update(teleport);

            if (teleport.Target != null) // Fix reactivating on other side (onloaded chunks)
            {
                _lastTargetPos = teleport.Target;
                Status.State = TeleportActivator.FSMState.Activated;
            }
            if (Status.State == TeleportActivator.FSMState.Activated)
            {
                // Fast forward
                AnimUtil?.AnimationTickServer(1000);
                UpdateSound(1000);
            }

            RegisterGameTickListener(OnGameTick, 50);
            RegisterGameTickListener(OnGameRenderTick, 10); // For prevent shader lags on open/close
        }

        private void UpdateState(TeleportActivator.FSMState state)
        {
            if (state == TeleportActivator.FSMState.Deactivated || state == TeleportActivator.FSMState.Deactivating)
            {
                AnimUtil?.StopAnimation("activation");
                AnimUtil?.StopAnimation("loop");
                (Block as BlockTeleport)?.SetActive(false, Pos);
            }
            else if (state == TeleportActivator.FSMState.Activating || state == TeleportActivator.FSMState.Activated)
            {
                AnimUtil?.StartAnimation(new AnimationMetaData
                {
                    Animation = "loop",
                    Code = "loop",
                    AnimationSpeed = 0.5f,
                    EaseInSpeed = 0.5f,
                    EaseOutSpeed = 2
                });

                AnimUtil?.StartAnimation(new AnimationMetaData
                {
                    Animation = "activation",
                    Code = "activation",
                    AnimationSpeed = 0.5f,
                });

                (Block as BlockTeleport)?.SetActive(true, Pos);
            }
        }

        private void OnGameRenderTick(float dt)
        {
            Status.OnTick(dt);
            _teleportRiftRenderer?.SetActivationProgress(Status.Progress);
        }

        private void OnGameTick(float dt)
        {
            var teleport = GetOrCreateTeleport();

            UpdateSound(dt);

            if (!teleport.Enabled || teleport.Target == null || teleport.Target != _lastTargetPos)
            {
                _lastTargetPos = teleport.Target;
                Status.Stop();
                return;
            }

            Status.Start();

            if (Status.State != TeleportActivator.FSMState.Activated) return;

            var center = teleport.GetGateCenter();
            var orientation = teleport.Orientation;

            var radius = teleport.Size / 2f;
            var thick = 0.5f;
            ParticleController?.SpawnGateParticles(radius, thick, center, orientation);

            if (Api is ICoreServerAPI)
            {
                var entities = MathUtil.GetInCyllinderEntities(Api, radius, thick, center, orientation);
                if (entities.Length > 0)
                {
                    if (!_manager.Points.TryGetValue(teleport.Target, out var targetTeleport) || targetTeleport.Target != Pos)
                    {
                        _manager.Points.Unlink(Pos);
                        return;
                    }
                    foreach (var entity in entities)
                    {
                        if (entity.Teleporting) continue;
                        TeleportEntity(entity, teleport, targetTeleport);
                    }
                }
            }
        }

        /// <returns>True for skip base</returns>
        public bool OnEntityCollide(Entity entity)
        {
            if (Status.State != TeleportActivator.FSMState.Activated) return false;

            var teleport = GetOrCreateTeleport();
            if (teleport.Target == null) return false;

            if (!_manager.Points.TryGetValue(teleport.Target, out var targetTeleport) || targetTeleport.Target != Pos)
            {
                _manager.Points.Unlink(Pos);
                return false;
            }

            TeleportEntity(entity, teleport, targetTeleport);
            return true;
        }

        private void TeleportEntity(Entity entity, Teleport teleport, Teleport targetTeleport)
        {
            if (Api is not ICoreServerAPI sapi)
                return;

            if (entity.IsActivityRunning(Constants.TeleportCooldownActivityName))
                return;

            var center = teleport.GetGateCenter();
            var orientation = teleport.Orientation;
            var targetCenter = targetTeleport.GetGateCenter();
            var targetOrientation = targetTeleport.Orientation;

            var entityPos = entity.Pos.Copy();
            var forwardOffset = targetTeleport.Orientation.Normalf.ToVec3d().Mul(-1); // Forward offset
            entityPos.SetPos(targetCenter + forwardOffset);
            if (orientation.IsHorizontal && targetOrientation.IsHorizontal)
            {
                var diff = orientation.Index - targetOrientation.Index;
                var yawDiff = diff * GameMath.PIHALF;
                var centerOffset = ((entity.Pos.XYZ - center) * (targetTeleport.Size / teleport.Size)).RotatedCopy(yawDiff); // Center offset
                entityPos.Add(centerOffset.X, centerOffset.Y, centerOffset.Z);
                entityPos.Yaw += yawDiff;
                entityPos.Motion = entityPos.Motion.RotatedCopy((float)(Math.PI + yawDiff));
                if (entity is EntityPlayer playerEntity && playerEntity.Player is IServerPlayer serverPlayer)
                {
                    sapi.Network.SendBlockEntityPacket(serverPlayer, Pos, Constants.PlayerTeleportedPacketId, [(byte)diff]);
                }
                else
                {
                    entity.ServerPos.Yaw = entityPos.Yaw;
                }
            }

            var motion = entityPos.Motion.Clone();
            TeleportUtil.StabilityRelatedTeleportTo(entity, entityPos, Logger, () =>
            {
                entity.ServerPos.Motion = motion;
                var soundLoc = new AssetLocation("sounds/effect/translocate-breakdimension.ogg");
                entity.World.PlaySoundAt(soundLoc, entity, null, true, 32, .5f);
                ((ICoreServerAPI)Api).Network.BroadcastBlockEntityPacket(
                    targetTeleport.Pos,
                    Constants.EntityTeleportedPacketId,
                    BitConverter.GetBytes(entity.EntityId));
            });
            Logger.Audit($"{entity?.GetName()} teleported from {teleport.Pos} ({teleport.Name}) to {targetTeleport.Pos} ({targetTeleport.Name})");
        }

        private void UpdateSound(float dt)
        {
            if (Api.Side == EnumAppSide.Client)
            {
                if (_sound?.IsPlaying == false)
                {
                    _sound.Start();
                }

                if (Status.State == TeleportActivator.FSMState.Activating ||
                    Status.State == TeleportActivator.FSMState.Activated)
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
            var teleport = GetOrCreateTeleport();
            if (!teleport.ActivatedByPlayers.Contains(playerUID))
            {
                teleport.ActivatedByPlayers.Add(playerUID);
                _manager.Points.MarkDirty(Pos);
            }
        }

        private Teleport GetOrCreateTeleport()
        {
            if (_manager.Points.TryGetValue(Pos, out var teleport))
            {
                return teleport;
            }
            else
            {
                var name = _manager.NameGenerator.Next();
                var repaired = (Block as BlockTeleport)?.IsNormal ?? false;
                teleport = new Teleport(Pos, name, repaired, Block);
                _manager.Points.AddOrUpdate(teleport);
                return teleport;
            }
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);
            Status.ToTreeAttributes(tree);
            if (_lastTargetPos != null)
                tree.SetBlockPos("lastTargetPos", _lastTargetPos);
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
        {
            base.FromTreeAttributes(tree, worldAccessForResolve);
            Status.FromTreeAttributes(tree);
            _lastTargetPos = tree.GetBlockPos("lastTargetPos");
        }

        public override void OnReceivedClientPacket(IPlayer fromPlayer, int packetid, byte[] data)
        {
            base.OnReceivedClientPacket(fromPlayer, packetid, data);

            if (packetid == Constants.OpenTeleportPacketId)
            {
                using var ms = new MemoryStream(data);
                using var reader = new BinaryReader(ms);
                var pos = BlockPos.CreateFromBytes(reader);
                _manager.Points.Link(Pos, pos);
            }

            if (packetid == Constants.CloseTeleportPacketId)
            {
                _manager.Points.Unlink(Pos);
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
        }

        public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
        {
            base.GetBlockInfo(forPlayer, dsc);

            var teleport = GetOrCreateTeleport();
            if (teleport.Enabled)
            {
                if (ClientSettings.ExtendedDebugInfo)
                {
                    dsc.AppendLine($"Status: {Status.State} {Status.Progress:0%}");
                }

                if (Status.State == TeleportActivator.FSMState.Activated && teleport.Target != null)
                {
                    _manager.Points.TryGetValue(teleport.Target, out var targetTeleport);
                    dsc.AppendLine($"{teleport.Name} &gt;&gt;&gt; {targetTeleport?.Name}");
                }
                else
                {
                    dsc.AppendLine(teleport.Name);
                }
            }
        }

        public override void OnBlockUnloaded()
        {
            base.OnBlockUnloaded();
            _teleportRiftRenderer?.Dispose();
            _sound?.Dispose();
            _teleportDialog?.TryClose();
            _editDialog?.TryClose();
        }

        public override void OnBlockRemoved()
        {
            base.OnBlockRemoved();

            if (Api.Side == EnumAppSide.Server)
            {
                _manager.Points.Remove(Pos);
            }

            _teleportRiftRenderer?.Dispose();
            _sound?.Dispose();
            _teleportDialog?.TryClose();
            _editDialog?.TryClose();
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

                float rotY = Block.Shape.rotateY;
                AnimUtil.InitializeAnimator($"{Constants.ModId}-teleport-" + Block.Variant["type"], null, null, new Vec3f(0, rotY, 0));
            }
        }

        public void UpdateBlock()
        {
            var teleport = GetOrCreateTeleport();
            teleport.Enabled = (Block as BlockTeleport)?.IsNormal ?? false;
            teleport.UpdateBlockInfo(Block);
            _teleportRiftRenderer?.Update(teleport);
            _manager.Points.MarkDirty(Pos);

            UpdateAnimator();
            MarkDirty(true);
        }

        public void OpenTeleportDialog()
        {
            if (Api is ICoreClientAPI capi)
            {
                _teleportDialog ??= new GuiDialogTeleportList(capi, Pos);
                _teleportDialog.TryOpen();
            }
        }

        public void OpenEditDialog()
        {
            if (Api is ICoreClientAPI capi)
            {
                _editDialog ??= new GuiDialogEditTeleport(capi, Pos);
                _editDialog.TryOpen();
            }
        }
    }
}

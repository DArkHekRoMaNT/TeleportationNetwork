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

namespace TeleportationNetwork
{
    public class BlockEntityTeleport : BlockEntity, IDisposable
    {
        private ILogger Logger => _modLogger ?? Api.Logger;
        public TeleportStatus Status { get; } = new();
        public int Size { get; set; }

        private TeleportManager _manager = null!;
        private TeleportControllers? _controllers;
        private ILogger? _modLogger;
        private BlockPos? _lastTargetPos;
        private GuiDialogTeleportList? _teleportDialog;
        private GuiDialogEditTeleport? _editDialog;

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);

            _modLogger = api.ModLoader.GetModSystem<Core>().Mod.Logger;
            _manager = api.ModLoader.GetModSystem<TeleportManager>();

            if (api is ICoreClientAPI capi)
            {
                _controllers = new TeleportControllers(capi, Pos);
            }

            var teleport = GetOrCreateTeleport();

            if (teleport.Target != null) // Fix reactivating on other side (unloaded chunk)
            {
                _lastTargetPos = teleport.Target;
                Status.State = TeleportStatus.FSMState.Activated;
            }

            _controllers?.UpdateTeleport(this);

            RegisterGameTickListener(OnGameTick, 50);
            RegisterGameTickListener(OnGameRenderTick, 10); // For prevent shader lags on open/close
        }

        public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tessThreadTesselator)
        {
            return true;
        }

        private void OnGameRenderTick(float dt)
        {
            Status.OnTick(dt);
            _controllers?.Update(Status);
        }

        private void OnGameTick(float dt)
        {
            var teleport = GetOrCreateTeleport();

            if (!teleport.Enabled || teleport.Target == null || teleport.Target != _lastTargetPos)
            {
                _lastTargetPos = teleport.Target;
                Status.Stop();
                return;
            }

            Status.Start();

            if (Status.State != TeleportStatus.FSMState.Activated) return;

            var center = teleport.GetGateCenter();
            var orientation = teleport.Orientation;

            var radius = teleport.Size / 2f;
            var thick = 0.5f;
            _controllers?.Particles.SpawnGateParticles(radius, thick, center, orientation);

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
            TeleportUtil.StabilityRelatedTeleportTo(entity, entityPos, Logger, () => //TODO: Random teleport sound + text?
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
                teleport = new Teleport(Pos, name, Status.IsRepaired, this);
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
            tree.SetInt("size", Size);
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
        {
            base.FromTreeAttributes(tree, worldAccessForResolve);
            Status.FromTreeAttributes(tree);
            _lastTargetPos = tree.GetBlockPos("lastTargetPos");
            Size = tree.GetInt("size");
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
                    _controllers?.Particles.SpawnTeleportParticles(entity);
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

                if (Status.State == TeleportStatus.FSMState.Activated && teleport.Target != null)
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

        public void UpdateBlock()
        {
            var teleport = GetOrCreateTeleport();
            teleport.Enabled = !Status.IsBroken;
            teleport.UpdateBlockInfo(this);
            _controllers?.UpdateTeleport(this);
            _manager.Points.MarkDirty(Pos);
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

        public override void OnBlockUnloaded()
        {
            base.OnBlockUnloaded();
            Dispose();
        }

        public override void OnBlockRemoved()
        {
            base.OnBlockRemoved();

            if (Api.Side == EnumAppSide.Server)
            {
                _manager.Points.Remove(Pos);
            }

            Dispose();
        }

        public void Dispose()
        {
            _controllers?.Dispose();
            _teleportDialog?.TryClose();
            _editDialog?.TryClose();
        }
    }
}
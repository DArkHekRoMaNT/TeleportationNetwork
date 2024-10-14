using System;
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
        public ILogger ModLogger => _modLogger ?? Api.Logger;
        public bool Active => Teleport.Target != null && _activationTime >= Constants.TeleportActivationTime;
        public bool Repaired => (Block as BlockTeleport)?.IsNormal ?? false;
        public Teleport Teleport => _teleportCached ??= GetOrCreateTeleport();
        public Teleport? TargetTeleport => Teleport.Target == null ? null : _targetTeleportCached ??= TeleportManager.Points[Teleport.Target];

        private TeleportManager TeleportManager { get; set; } = null!;
        private TeleportParticleController? ParticleController => (Block as BlockTeleport)?.ParticleController;
        private BlockEntityAnimationUtil? AnimUtil => GetBehavior<BEBehaviorAnimatable>()?.animUtil;

        // Client side only
        private TeleportRiftRenderer? _teleportRiftRenderer;
        private GuiDialogTeleportList? _teleportDlg;
        private GuiDialogEditTeleport? _editDlg;

        private ILogger? _modLogger;
        private ILoadedSound? _sound;
        private float _soundVolume;
        private float _soundPith;
        private float _activationTime;
        private Teleport? _teleportCached = null;
        private Teleport? _targetTeleportCached = null;
        private BlockPos? _lastTargetTeleport = null;
        private bool _lastActive = false;

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);

            _modLogger = api.ModLoader.GetModSystem<Core>().Mod.Logger;
            TeleportManager = api.ModLoader.GetModSystem<TeleportManager>();

            SetBlockActive(false);
            if (api.Side == EnumAppSide.Server)
            {
                CreateTeleport();
            }

            if (api is ICoreClientAPI capi)
            {
                _teleportRiftRenderer = new TeleportRiftRenderer(Pos, capi, Block.Shape.rotateY);

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

            TeleportManager.Points.ValueChanged += (teleport) =>
            {
                if (teleport.Pos == Pos)
                {
                    MarkDirty(true);
                }
            };

            RegisterGameTickListener(OnGameTick, 50);
            RegisterGameTickListener(OnGameRenderTick, 10); // For prevent shader lags on open/close
        }

        private void SetBlockActive(bool active)
        {
            if (active == _lastActive)
                return;
            (Block as BlockTeleport)?.SetActive(active, Pos);
            _lastActive = active;
        }

        private void OnGameRenderTick(float dt)
        {
            if (!Repaired) return;

            _teleportRiftRenderer?.SetActivationProgress(_activationTime / Constants.TeleportActivationTime);

            if (Teleport.Target != null && (_lastTargetTeleport == Teleport.Target || _activationTime == 0))
            {
                _activationTime = Math.Min(_activationTime + dt, Constants.TeleportActivationTime);
                SetBlockActive(true);
            }
            else
            {
                _activationTime = Math.Max(_activationTime - dt * 2, 0);
                SetBlockActive(false);
                return;
            }
            _lastTargetTeleport = Teleport.Target;
        }

        private void OnGameTick(float dt)
        {
            if (!Repaired) return;

            UpdateSound(dt);

            if (!Active || Teleport.Target == null) return;
            if (!TeleportManager.CheckTeleportLink(Teleport)) return;

            if (TargetTeleport == null) return;

            var orientation = Teleport.Orientation;
            var targetOrientation = TargetTeleport.Orientation;
            if (Api is ICoreServerAPI sapi && orientation != null && targetOrientation != null)
            {
                var center = Teleport.GetGateCenter();
                var entities = MathUtil.GetInCyllinderEntities(Api, Teleport.Size / 2f, 0.5f, center, orientation);

                if (entities.Length > 0)
                {
                    var targetCenter = TargetTeleport.GetGateCenter();
                    foreach (var entity in entities)
                    {
                        if (entity.IsActivityRunning(Constants.TeleportCooldownActivityName))
                            continue;

                        var point = TargetTeleport.GetTargetPos();
                        point += (entity.Pos.XYZ - center) * (TargetTeleport.Size / Teleport.Size); // Offset

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

                        var localPoint = point.AsBlockPos.ToLocalPosition(Api);
                        TeleportUtil.StabilityRelatedTeleportTo(entity, entityPos, ModLogger, () => AfterTeleportEntity(entity));
                        ModLogger.Audit($"{entity?.GetName()} teleported to {localPoint} ({TargetTeleport.Name})");
                    }
                }
            }
        }

        private void AfterTeleportEntity(Entity entity)
        {
            var soundLoc = new AssetLocation("sounds/effect/translocate-breakdimension.ogg");
            entity.World.PlaySoundAt(soundLoc, entity, null, true, 32, .5f);
            ((ICoreServerAPI)Api).Network.BroadcastBlockEntityPacket(
                Teleport.Target,
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

        private Teleport GetOrCreateTeleport()
        {
            var teleport = TeleportManager.Points[Pos];
            teleport ??= CreateTeleport();

            _teleportRiftRenderer?.Update(teleport);
            _targetTeleportCached = null;

            return teleport;
        }

        private Teleport CreateTeleport()
        {
            if (!TeleportManager.Points.Contains(Pos))
            {
                var name = TeleportManager.NameGenerator.Next();
                var teleport = new Teleport(Pos, name, Repaired, Block);
                TeleportManager.Points.Set(teleport);
            }
            return TeleportManager.Points[Pos]!;
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

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);
            tree.SetFloat("activationTime", _activationTime);
            if (_lastTargetTeleport != null)
                tree.SetBlockPos("lastTargetTeleport", _lastTargetTeleport);
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
        {
            base.FromTreeAttributes(tree, worldAccessForResolve);
            _activationTime = tree.GetFloat("activationTime");
            _lastTargetTeleport = tree.GetBlockPos("lastTargetTeleport");
        }

        public override void OnReceivedClientPacket(IPlayer fromPlayer, int packetid, byte[] data)
        {
            base.OnReceivedClientPacket(fromPlayer, packetid, data);

            if (packetid == Constants.OpenTeleportPacketId)
            {
                using var ms = new MemoryStream(data);
                using var reader = new BinaryReader(ms);
                var pos = BlockPos.CreateFromBytes(reader);
                TeleportManager.LinkTeleport(Teleport, pos);
            }

            if (packetid == Constants.CloseTeleportPacketId)
            {
                TeleportManager.UnlinkTeleport(Teleport);
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

            if (Repaired)
            {
                if (Active)
                {
                    var extra = TargetTeleport?.Enabled == true ? "" : " (Broken)";
                    dsc.AppendLine($"{Teleport.Name} &gt;&gt;&gt; {TargetTeleport?.Name}{extra}");
                }
                else
                {
                    dsc.AppendLine(Teleport.Name);
                }
            }
        }

        public override void OnBlockUnloaded()
        {
            base.OnBlockUnloaded();
            _teleportRiftRenderer?.Dispose();
            _sound?.Dispose();
        }

        public override void OnBlockRemoved()
        {
            base.OnBlockRemoved();

            if (Api.Side == EnumAppSide.Server)
            {
                TeleportManager.Points.Remove(Pos);
            }

            _teleportRiftRenderer?.Dispose();
            _sound?.Dispose();
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

        public override void MarkDirty(bool redrawOnClient = false, IPlayer skipPlayer = null)
        {
            base.MarkDirty(redrawOnClient, skipPlayer);
            _targetTeleportCached = null;
            _teleportCached = null;
        }

        public void UpdateBlock()
        {
            if (Api != null)
            {
                if (Api.Side == EnumAppSide.Client)
                {
                    UpdateAnimator();
                }

                CreateTeleport();
                Teleport.Enabled = Repaired;
                Teleport.UpdateBlockInfo(Block);
                TeleportManager.Points.MarkDirty(Pos);

                MarkDirty(true);
            }
        }
    }
}

using Vintagestory.GameContent;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Server;
using System.Text;

namespace TeleportationNetwork
{
    //TODO Enum to static class
    public enum EnumTeleportState
    {
        Broken = 0,
        Normal = 1
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

    public class BlockEntityTeleport : BlockEntity
    {
        #region render

        BlockEntityAnimationUtil animUtil { get { return GetBehavior<BEBehaviorAnimatable>()?.animUtil; } }
        static SimpleParticleProperties props = new SimpleParticleProperties(
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

        /*public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tessThreadTesselator)
        {
            if (animUtil.activeAnimationsByAnimCode.Count > 0) return false;
            if (!Repaired) return false;

            MeshData mesh = ObjectCacheUtil.GetOrCreate(Api, Constants.MOD_ID + "-teleport-" + State + "-" + Type, () =>
            {
                ICoreClientAPI capi = Api as ICoreClientAPI;

                string shapeCode = "seal";
                // string shapeCode = "normal";
                // switch (State)
                // {
                //     case EnumTeleportState.Broken: shapeCode = "broken"; break;
                //     case EnumTeleportState.Normal: shapeCode = "normal"; break;
                // }

                MeshData meshdata;
                IAsset asset = Api.Assets.TryGet(new AssetLocation(Constants.MOD_ID, "shapes/block/teleport/" + shapeCode + ".json"));
                Shape shape = asset.ToObject<Shape>();

                tessThreadTesselator.TesselateShape(ownBlock, shape, out meshdata, new Vec3f(0, 0, 0));

                return meshdata;
            });

            mesher.AddMeshData(mesh);
            return false;
        }*/

        #endregion

        #region common

        BlockTeleport ownBlock;
        long listenerid;
        GuiDialogTeleport teleportDlg;
        GuiDialogRenameTeleport renameDlg;

        TeleportData tpData;
        Dictionary<string, TeleportingPlayer> tpingPlayers = new Dictionary<string, TeleportingPlayer>();


        public EnumTeleportState State
        {
            get
            {
                string state = null;
                Block.Variant.TryGetValue("state", out state);

                if (state == "broken") return EnumTeleportState.Broken;
                if (state == "normal") return EnumTeleportState.Normal;

                Api.Logger.ModWarning("Unknown teleport state " + state + ", will be replaced to default.");
                Block def = Api.World.GetBlock(new AssetLocation(Constants.MOD_ID, "teleport-broken"));
                if (def != null) Block = def;

                State = EnumTeleportState.Broken;
                return EnumTeleportState.Broken;
            }
            set
            {
                string state = null;

                if (value == EnumTeleportState.Broken)
                {
                    state = "broken";
                    RemoveGameTickers();
                }
                else if (value == EnumTeleportState.Normal)
                {
                    state = "normal";
                    SetupGameTickers();
                }

                if (state != null)
                {
                    Block block = Api.World.GetBlock(Block.CodeWithVariant("state", state));
                    Api.World.BlockAccessor.SetBlock(block.BlockId, Pos);
                }
            }
        }
        public bool Repaired
        {
            get { return State == EnumTeleportState.Normal; }
            set { State = value ? EnumTeleportState.Normal : EnumTeleportState.Broken; }
        }
        public bool Active { get; set; }
        public string Type => Block?.LastCodePart();

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);

            if (api.Side == EnumAppSide.Server)
            {
                tpData = TPNetManager.GetOrCreateData(Pos, Repaired);
            }

            if (api.Side == EnumAppSide.Client)
            {
                float rotY = Block.Shape.rotateY;
                animUtil?.InitializeAnimator(Constants.MOD_ID + "-teleport", new Vec3f(0, rotY, 0));
            }

            ownBlock = Block as BlockTeleport;
            if (Repaired)
            {

                SetupGameTickers();
            }
        }

        private void SetupGameTickers()
        {
            listenerid = RegisterGameTickListener(OnGameTick, 50);
        }
        private void RemoveGameTickers()
        {
            UnregisterGameTickListener(listenerid);
        }

        List<string> toremove = new List<string>();
        private void OnGameTick(float dt)
        {
            if (animUtil?.animator?.ActiveAnimationCount == 0)
            {
                animUtil.StartAnimation(new AnimationMetaData()
                {
                    Animation = "octagram",
                    Code = "octagram",
                    AnimationSpeed = 0.5f,
                    EaseInSpeed = 1f,
                    EaseOutSpeed = 1f
                });
                animUtil.StartAnimation(new AnimationMetaData()
                {
                    Animation = "gear",
                    Code = "gear",
                    AnimationSpeed = 0.25f,
                    EaseInSpeed = 1f,
                    EaseOutSpeed = 1f
                });
            }

            if (!Active) return;

            toremove.Clear();

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
                    toremove.Add(val.Key);
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
                    }
                }

                bestSecondsPassed = Math.Max(val.Value.SecondsPassed, bestSecondsPassed);
            }

            foreach (var playerUID in toremove)
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
                animUtil.activeAnimationsByAnimCode["octagram"].AnimationSpeed = (float)(0.5f * (1 + Math.Exp(bestSecondsPassed) * 0.3f));
            }
        }

        public void OnEntityCollide(Entity entity)
        {
            if (!Repaired) return;

            EntityPlayer player = entity as EntityPlayer;
            if (player == null) return;

            if (player.IsActivityRunning("teleportCooldown")) //REVIEW ActivityRunning not working?
            {
                return;
            }

            TeleportingPlayer tpe;
            if (!tpingPlayers.TryGetValue(player.PlayerUID, out tpe))
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

        public void OnShiftRightClick()
        {
            if (Api.Side == EnumAppSide.Client)
            {
                if (renameDlg == null)
                {
                    renameDlg = new GuiDialogRenameTeleport("Rename", Pos, Api as ICoreClientAPI, CairoFont.WhiteSmallText());
                }

                if (!renameDlg.IsOpened()) renameDlg.TryOpen();
            }
        }

        public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
        {
            base.GetBlockInfo(forPlayer, dsc);

            if (Repaired)
            {
                dsc.AppendLine(TPNetManager.GetTeleport(Pos).Name);
            }
        }

        #endregion
    }
}
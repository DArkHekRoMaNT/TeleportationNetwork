using Vintagestory.GameContent;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Client;
using System.Text;
using Vintagestory.API.Config;
using System.Collections.Generic;
using System;

namespace TeleportationNetwork
{
    public enum EnumTeleportState
    {
        Broken = 0,
        Normal = 1
    }

    public class BlockEntityTeleport : BlockEntity
    {
        public EnumTeleportState State
        {
            get
            {
                string state = null;
                Block?.Variant.TryGetValue("state", out state);

                if (state == "broken") return EnumTeleportState.Broken;
                if (state == "normal") return EnumTeleportState.Normal;

                Api?.Logger?.Warning("[" + Constants.MOD_ID + "] unknown teleport state " + state + ", will be replaced to default.");
                Block def = Api?.World?.GetBlock(new AssetLocation(Constants.MOD_ID, "teleport-broken"));
                if (def != null) Block = def;

                State = EnumTeleportState.Broken;
                return EnumTeleportState.Broken;
            }
            set
            {
                string state = null;

                if (value == EnumTeleportState.Broken) state = "broken";
                else if (value == EnumTeleportState.Normal) state = "normal";

                if (state != null)
                {
                    Block block = Api?.World?.GetBlock(Block?.CodeWithVariant("state", state));
                    Api?.World?.BlockAccessor?.SetBlock(block.BlockId, Pos);
                }
            }
        }

        List<string> activatedBy; // TODO: activatedBy list

        BlockEntityAnimationUtil animUtil { get { return GetBehavior<BEBehaviorAnimatable>()?.animUtil; } }

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);
            if (api.Side == EnumAppSide.Client)
            {
                animUtil?.InitializeAnimator(Constants.MOD_ID + "-teleport", null, (Block as BlockTeleport)?.GetShape(State));
            }

            RegisterGameTickListener(OnGameLongTick, 2000);
        }

        private void OnGameLongTick(float dt)
        {
            if (State == EnumTeleportState.Normal && animUtil?.animator?.GetAnimationState("octagram")?.Running == false)
            {
                animUtil.StartAnimation(new AnimationMetaData()
                {
                    Animation = "octagram",
                    Code = "octagram",
                    AnimationSpeed = 0.025f
                });
                animUtil.StartAnimation(new AnimationMetaData()
                {
                    Animation = "gear",
                    Code = "gear",
                    AnimationSpeed = 0.5f
                });
            }
        }

        public override void OnBlockRemoved()
        {
            base.OnBlockRemoved();
            animUtil?.StopAnimation("octagram");
            animUtil?.StopAnimation("gear");
        }

        public void OnEntityCollide(Entity entity)
        {
            if (entity is EntityPlayer player && State == EnumTeleportState.Normal &&
                animUtil?.animator?.GetAnimationState("octagram").CurrentFrame == 29)
            {
                animUtil.StopAnimation("octagram");
                animUtil.StopAnimation("gear");
                Api.SendMessageAll("OpenGUI");
            }
        }

        public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tessThreadTesselator)
        {
            bool skipMesh = base.OnTesselation(mesher, tessThreadTesselator);

            if (!skipMesh)
            {
                mesher.AddMeshData((Block as BlockTeleport).GetMesh(State));
            }

            return true;
        }
        public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
        {
            base.GetBlockInfo(forPlayer, dsc);
            dsc.AppendLine(Lang.Get("State: {0}", State));
        }
    }
}
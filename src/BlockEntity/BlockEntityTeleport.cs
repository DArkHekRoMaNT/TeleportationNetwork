using Vintagestory.GameContent;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Client;
using System.Text;
using Vintagestory.API.Config;

namespace TeleportationNetwork
{
    public enum EnumTeleportState
    {
        Broken = 0,
        Ready = 1
    }

    public class BlockEntityTeleport : BlockEntity
    {
        EnumTeleportState _state = EnumTeleportState.Broken;
        public EnumTeleportState State
        {
            get { return _state; }
            set
            {
                _state = value;
                MarkDirty(true);
            }
        }

        BlockEntityAnimationUtil animUtil { get { return GetBehavior<BEBehaviorAnimatable>()?.animUtil; } }

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);

            if (api.Side == EnumAppSide.Client)
            {
                animUtil?.InitializeAnimator(Constants.MOD_ID + "-teleport", null, (Block as BlockTeleport)?.GetShape(_state));
            }

            RegisterGameTickListener(OnGameLongTick, 2000);
        }

        private void OnGameLongTick(float dt)
        {
            if (_state == EnumTeleportState.Ready && animUtil?.animator?.GetAnimationState("octagram")?.Running == false)
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
            if (entity is EntityPlayer player && _state == EnumTeleportState.Ready &&
                animUtil?.animator?.GetAnimationState("octagram").CurrentFrame == 29)
            {
                animUtil.StopAnimation("octagram");
                animUtil.StopAnimation("gear");
                Api.SendMessageAll("OpenGUI");
            }
        }
        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);

            tree.SetInt("state", (int)_state);
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
        {
            base.FromTreeAttributes(tree, worldAccessForResolve);

            _state = (EnumTeleportState)tree.GetInt("state");
        }
        public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tessThreadTesselator)
        {
            bool skipMesh = base.OnTesselation(mesher, tessThreadTesselator);

            if (!skipMesh)
            {
                mesher.AddMeshData((Block as BlockTeleport).GetMesh(_state));
            }

            return true;
        }
        public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
        {
            base.GetBlockInfo(forPlayer, dsc);
            dsc.AppendLine(Lang.Get("State: {0}", _state.ToString()));
        }
    }
}
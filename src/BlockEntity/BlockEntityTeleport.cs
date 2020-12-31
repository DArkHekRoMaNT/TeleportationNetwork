using Vintagestory.GameContent;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;

namespace TeleportationNetwork
{
    public class BlockEntityTeleport : BlockEntity
    {
        public bool IsBroken { get { return Type == null || Type == "brokencore"; } }
        public string Type { get { return Block?.LastCodePart(); } }

        BlockEntityAnimationUtil animUtil { get { return GetBehavior<BEBehaviorAnimatable>()?.animUtil; } }

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);

            if (api.Side == EnumAppSide.Client)
            {
                animUtil?.InitializeAnimator(Constants.MOD_ID + "-teleport-" + Type, null, (Block as BlockTeleport).GetShape(Type));
                if (Type == "core")
                {

                }
            }

            RegisterGameTickListener(OnGameLongTick, 2000);
        }

        private void OnGameLongTick(float dt)
        {
            if (!IsBroken && animUtil?.animator?.GetAnimationState("octagram")?.Running == false)
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
            if (entity is EntityPlayer player && !IsBroken &&
                animUtil?.animator?.GetAnimationState("octagram").CurrentFrame == 29)
            {
                animUtil.StopAnimation("octagram");
                animUtil.StopAnimation("gear");
            }
        }
    }
}
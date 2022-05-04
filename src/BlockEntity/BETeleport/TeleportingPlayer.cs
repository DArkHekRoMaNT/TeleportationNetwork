using Vintagestory.API.Common;

namespace TeleportationNetwork
{
    public class TeleportingPlayer
    {
        public EntityPlayer Player { get; set; }
        public long LastCollideMs { get; set; }
        public float SecondsPassed { get; set; }
        public EnumTeleportingEntityState State { get; set; }
    }
}
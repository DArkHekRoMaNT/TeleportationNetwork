using Vintagestory.API.Common;

namespace TeleportationNetwork
{
    public class TeleportingPlayerData(EntityPlayer player)
    {
        public EntityPlayer Player { get; } = player;
        public long LastCollideMs { get; set; } = 0;
        public float SecondsPassed { get; set; } = 0;
        public EnumState State { get; set; } = EnumState.None;

        public enum EnumState
        {
            None,
            Active,
            UI
        }
    }
}

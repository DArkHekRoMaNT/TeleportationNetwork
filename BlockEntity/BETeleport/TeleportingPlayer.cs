using Vintagestory.API.Common;

namespace TeleportationNetwork
{
    public class TeleportingPlayerData
    {
        public EntityPlayer Player { get; }
        public long LastCollideMs { get; set; }
        public float SecondsPassed { get; set; }
        public EnumState State { get; set; }

        public TeleportingPlayerData(EntityPlayer player)
        {
            Player = player;
            LastCollideMs = 0;
            SecondsPassed = 0;
            State = EnumState.None;
        }

        public enum EnumState
        {
            None,
            Active,
            UI
        }
    }
}

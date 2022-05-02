namespace TeleportationNetwork
{
    public class Config : ITeleportManagerConfig
    {
        public static Config Current { get; set; }
        public class Part<T>
        {
            public readonly string Comment;
            public readonly T Default;
            private T val;
            public T Val
            {
                get => val != null ? val : val = Default;
                set => val = (value != null ? value : Default);
            }
            public Part(T Default, string Comment = null)
            {
                this.Default = Default;
                this.Val = Default;
                this.Comment = Comment;
            }
            public Part(T Default, string prefix, string[] allowed, string postfix = null)
            {
                this.Default = Default;
                this.Val = Default;
                this.Comment = prefix;

                this.Comment += "[" + allowed[0];
                for (int i = 1; i < allowed.Length; i++)
                {
                    this.Comment += ", " + allowed[i];
                }
                this.Comment += "]" + postfix;
            }
        }

        public Part<bool> LegacySharedTeleports { get; set; } = new Part<bool>(false, "All activated teleport to all players");
        //public Part<bool> CreateClaim { get; set; } = new Part<bool>(false);
        public Part<bool> Unbreakable { get; set; } = new Part<bool>(true);
        public Part<string> StabilityTeleportMode { get; set; } = new Part<string>("on", "Enabling the teleport stability mode. If on, teleport will be unstable at low temporal stability (lower than StabilityConsumable) and during storms. If always, then everytime will be unstable.", new string[] { "on", "off", "always" });
        public Part<double> StabilityConsumable { get; set; } = new Part<double>(0.2, "Consumption of stability for teleport if stability is not enough <data deleted>. Percentage values from 0 to 1");
        public Part<int> UnstableTeleportRange { get; set; } = new Part<int>(500, "Range for unstable teleport <data deleted>");

        public bool SubNetworks { get; set; }
        public int MaxNetworkDistance { get; set; }
        public bool SharedTeleports { get; set; }
        public int TeleportCooldown { get; set; }

        public Config()
        {
            SubNetworks = true;
            MaxNetworkDistance = 1000;
            SharedTeleports = false;
            TeleportCooldown = 5000;
        }
    }

    public interface ITeleportManagerConfig
    {
        /// <summary>
        /// Creates teleports subnetworks. You will not be able to teleport 
        /// if you activate the teleport too far from the desired one and 
        /// do not activate teleports in between.
        /// </summary>
        bool SubNetworks { get; }

        /// <summary>
        /// Maximum distance between two teleports to create a network.
        /// Only works if SubNetworks=true
        /// </summary>
        int MaxNetworkDistance { get; }

        /// <summary>
        /// Teleports are immediately available to everyone if repaired
        /// </summary>
        bool SharedTeleports { get; }

        /// <summary>
        /// Cooldown between teleports in milliseconds
        /// </summary>
        int TeleportCooldown { get; }
    }
}
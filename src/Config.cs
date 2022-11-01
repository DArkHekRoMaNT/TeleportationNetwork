using Vintagestory.API.Client;

namespace TeleportationNetwork
{
    [Config("tpnet.json")]
    public class Config
    {
        [ConfigItem(typeof(bool),
            true,
            Description = "Teleport blocks are unbreakable in survival")]
        public bool Unbreakable { get; set; }

        [ConfigItem(typeof(string),
            "on",
            Values = new string[] { "on", "off", "always" },
            Description = "Enabling the teleport stability mode." +
            " If on, teleport will be unstable at low temporal stability" +
            " (lower than StabilityConsumable) and during storms." +
            " If always, then everytime will be unstable.")]
        public string StabilityTeleportMode { get; set; } = null!;

        [ConfigItem(typeof(double),
            0.2,
            MinValue = 0,
            MaxValue = 1,
            Description = "Consumption of stability for teleport if stability" +
            " is not enough <data deleted>. Percentage values from 0 to 1")]
        public double StabilityConsumable { get; set; }


        [ConfigItem(typeof(int),
            500,
            MinValue = 0,
            Description = "Range for unstable teleport <data deleted>")]
        public int UnstableTeleportRange { get; set; }

        [ConfigItem(typeof(bool),
            false,
            Description = "Creates teleports subnetworks. You will not be able to teleport" +
            " if you activate the teleport too far from the desired one and" +
            " do not activate teleports in between.")]
        public bool SubNetworks { get; set; }

        [ConfigItem(typeof(int),
            10000,
            MinValue = 0,
            Description = "Maximum distance between two teleports to create a network." +
            " Only works if SubNetworks is true")]
        public int MaxNetworkDistance { get; set; }

        [ConfigItem(typeof(bool),
            false,
            Description = "Teleports are immediately available to everyone if repaired")]
        public bool SharedTeleports { get; set; }

        [ConfigItem(typeof(int),
            5000,
            MinValue = 0,
            Description = "Cooldown between teleports in milliseconds")]
        public int TeleportCooldown { get; set; }

        [ConfigItem(typeof(int),
            4096,
            MinValue = 0,
            Description = "Minimal distance between teleport structures")]
        public int MinTeleportDistance { get; set; }

        [ConfigItem(typeof(bool),
            false,
            Description = "Remove all paper and metal lanterns from teleport structures if True")]
        public bool DarknessMode { get; set; }

        [ConfigItem(typeof(bool),
            false,
            Description = "Create only standard teleports with granite and aged wood")]
        public bool BiomlessTeleports { get; set; }
    }
}

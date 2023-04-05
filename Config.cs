using CommonLib.Config;

namespace TeleportationNetwork
{
    [Config("tpnet.json")]
    public class Config
    {
        [Description("Teleport block are unbreakable in survival")]
        public bool Unbreakable { get; set; } = true;

        [Strings("on", "off", "always")]
        [Description("Enabling the teleport stability mode." +
            " If on, teleport will be unstable at low temporal stability" +
            " (lower than StabilityConsumable) and during storms." +
            " If always, then everytime will be unstable")]
        public string StabilityTeleportMode { get; set; } = "on";

        [Range(0, 1.0)]
        [Description("Consumption of stability for teleport if stability" +
            " is not enough <data deleted>. Percentage values from 0 to 1")]
        public double StabilityConsumable { get; set; } = 0.2;

        [Range(0, int.MaxValue)]
        [Description("Range for unstable teleport <data deleted> behavior")]
        public int UnstableTeleportRange { get; set; } = 500;

        [Description("Teleports are immediately available to everyone if repaired")]
        public bool SharedTeleports { get; set; } = false;

        [Range(0, int.MaxValue)]
        [Description("Cooldown between teleports in milliseconds")]
        public int TeleportCooldown { get; set; } = 5000;

        [Range(0, int.MaxValue)]
        [Description("Minimal distance between teleport structures")]
        public int MinTeleportDistance { get; set; } = 4096;

        [Description("Remove all paper and metal lanterns from teleport structures if True")]
        public bool DarknessMode { get; set; } = false;

        [Description("Create only standard teleports with granite and aged wood")]
        public bool BiomlessTeleports { get; set; } = false;

        [Strings("on", "off", "trader-only")]
        [Description("Create claim for teleport structure (does not affect generated structures)")]
        public string TeleportBuildProtected { get; set; } = "trader-only";

        [ClientOnly]
        [Description("Show teleport points on map")]
        public bool ShowTeleportOnMap { get; set; } = true;

        [ClientOnly]
        [Description("Default repaired teleport icon on the map")]
        [Strings("circle", "bee", "cave", "home", "ladder", "pick", "rocks", "ruins", "spiral", "star1", "star2", "trader", "vessel")]
        public string DefaultTeleportIcon { get; set; } = "spiral";

        [ClientOnly]
        [Description("Default repaired teleport icon color")]
        public string DefaultTeleportColor { get; set; } = "#23cca2";

        [ClientOnly]
        [Description("Broken teleport icon color")]
        public string BrokenTeleportColor { get; set; } = "#104430";

        [Description("Disable trader and locust teleport structures")]
        public bool NoSpecialTeleports { get; set; } = false;
    }
}

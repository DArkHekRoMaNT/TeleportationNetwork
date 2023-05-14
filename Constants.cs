namespace TeleportationNetwork
{
    public static class Constants
    {
        public static string ModId => "tpnet";
        public static string TeleportSchematicPath => "worldgen/schematics/tpnet/teleport";
        public static float BeforeTeleportShowGUITime => 3f;
        public static float SealRadius => 2.5f;
        public static int TeleportPlayerPacketId => 13514;
        public static int EntityTeleportedPacketId => 13516;
        public static string TeleportStructureGroup => ModId + "-teleport";
        public static int MaxPillarHeight => int.MaxValue;
    }
}

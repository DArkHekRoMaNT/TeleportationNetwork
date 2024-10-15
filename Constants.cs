namespace TeleportationNetwork
{
    //TODO: Rename?
    public static class Constants
    {
        public static string ModId => "tpnet";
        public static string TeleportSchematicPath => "worldgen/schematics/tpnet/teleport";
        public static float TeleportActivationTime => 3f;
        public static float SealRadius => 2.5f;
        public static int OpenTeleportPacketId => 13514;
        public static int CloseTeleportPacketId => 13515;
        public static int EntityTeleportedPacketId => 13516;
        public static int PlayerTeleportedPacketId => 13517;
        public static string TeleportStructureGroup => $"{ModId}-teleport";
        public static int MaxPillarHeight => int.MaxValue;
        public static int TeleportTriesPerChunk => 10;
        public static string TeleportCooldownActivityName => $"{ModId}_teleportCooldown";
        public static string TeleportManagerChannelName => $"{ModId}-teleport-manager";
        public static string TeleportSyncChannelName => $"{ModId}-teleport-sync";
    }
}

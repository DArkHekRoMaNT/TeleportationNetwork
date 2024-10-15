using ProtoBuf;
using Vintagestory.API.MathTools;

namespace TeleportationNetwork.Packets
{
    [ProtoContract(SkipConstructor = true, ImplicitFields = ImplicitFields.AllPublic)]
    public record struct RemoveTeleportMessage(BlockPos Pos);

    [ProtoContract(SkipConstructor = true, ImplicitFields = ImplicitFields.AllPublic)]
    public record struct SyncTeleportListMessage(Teleport[] Points);

    [ProtoContract(SkipConstructor = true, ImplicitFields = ImplicitFields.AllPublic)]
    public record struct SyncTeleportMessage(Teleport Teleport);

    [ProtoContract(SkipConstructor = true, ImplicitFields = ImplicitFields.AllPublic)]
    public record struct TeleportPlayerMessage(BlockPos Pos);
}

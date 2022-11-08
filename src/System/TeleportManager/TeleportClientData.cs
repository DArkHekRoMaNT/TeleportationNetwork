using ProtoBuf;
using System.IO;
using Vintagestory.API.MathTools;

namespace TeleportationNetwork
{
    [ProtoContract(ImplicitFields = ImplicitFields.AllFields)]
    public class TeleportClientData
    {
        public static string DefaultIcon => Core.Config.DefaultTeleportIcon;
        public static int DefaultColor => ColorUtil.Hex2Int(Core.Config.DefaultTeleportColor);

        public bool Pinned { get; set; } = false;

        public int SortOrder { get; set; } = 0;

        public string Note { get; set; } = "";

        public string Icon
        {
            get => _icon ?? DefaultIcon;
            set => _icon = value != DefaultIcon ? value : null;
        }
        private string? _icon;

        public int Color
        {
            get => _color ?? DefaultColor;
            set => _color = value != DefaultColor ? value : null;
        }
        private int? _color;

        public void ToBytes(BinaryWriter writer)
        {
            writer.Write(Pinned);
            writer.Write(Note);
            writer.Write(Icon);
            writer.Write(Color);
            writer.Write(SortOrder);
        }

        public void FromBytes(BinaryReader reader)
        {
            try
            {
                Pinned = reader.ReadBoolean();
                Note = reader.ReadString();
                Icon = reader.ReadString();
                Color = reader.ReadInt32();
                SortOrder = reader.ReadInt32();
            }
            catch (EndOfStreamException) { } // legacy data check
        }

        public TeleportClientData Clone()
        {
            return (TeleportClientData)MemberwiseClone();
        }
    }
}

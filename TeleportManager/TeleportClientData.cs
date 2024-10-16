using CommonLib.Utils;
using ProtoBuf;

namespace TeleportationNetwork
{
    [ProtoContract]
    public class TeleportClientData
    {
        public static string DefaultIcon => Core.Config.DefaultTeleportIcon;
        public static DarkColor DefaultColor => DarkColor.FromHex(Core.Config.DefaultTeleportColor);

        [ProtoMember(1)] public bool Pinned { get; set; } = false;
        [ProtoMember(2)] public int SortOrder { get; set; } = 0;
        [ProtoMember(3)] public string Note { get; set; } = string.Empty;

        public string Icon
        {
            get => _icon ?? DefaultIcon;
            set => _icon = value != DefaultIcon ? value : null;
        }

        public DarkColor Color
        {
            get => _color ?? DefaultColor;
            set => _color = value != DefaultColor ? value : null;
        }

        [ProtoMember(4)] private string? _icon;
        [ProtoMember(5)] private DarkColor? _color;

        public TeleportClientData Clone()
        {
            return new TeleportClientData
            {
                Pinned = Pinned,
                SortOrder = SortOrder,
                Note = Note,
                Icon = Icon,
                Color = Color
            };
        }
    }
}

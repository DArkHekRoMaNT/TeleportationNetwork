using ProtoBuf;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Vintagestory.API.MathTools;

//TODO: Move to CommonLib
namespace CommonLib.Utils
{
    [ProtoContract]
    public readonly struct DarkColor
    {
        [ProtoMember(1)]
        private readonly int _argbValue;

        private DarkColor(int argbValue)
        {
            _argbValue = argbValue;
        }

        public int A => _argbValue >> 24 & 0xFF;
        public int R => _argbValue >> 16 & 0xFF;
        public int G => _argbValue >> 8 & 0xFF;
        public int B => _argbValue & 0xFF;
        public int RGB => _argbValue & 0xFFFFFF;
        public int ARGB => _argbValue;
        public int RGBA => (R << 24) | (G << 16) | (B << 8) | A;
        public string Hex => ColorUtil.Int2Hex(_argbValue);

        public override string ToString() => Hex;

        public static DarkColor FromHex(string value)
        {
            if (value.StartsWith('#'))
                value = value.Substring(1);

            var intValue = int.Parse(value, NumberStyles.HexNumber);
            if (value.Length == 6)
            {
                return FromRGB(intValue);
            }
            return FromARGB(intValue);
        }

        public static DarkColor FromARGB(byte a, byte r, byte g, byte b)
        {
            return new DarkColor(a << 24 | r << 16 | g << 8 | b);
        }

        public static DarkColor FromRGB(int rgb)
        {
            return new DarkColor(255 << 24 | rgb);
        }

        public static DarkColor FromARGB(int argb)
        {
            return new DarkColor(argb);
        }

        public static DarkColor FromHSL(int hsl)
        {
            return new DarkColor(ColorUtil.Hsv2Rgb(hsl));
        }

        public override bool Equals([NotNullWhen(true)] object? obj)
        {
            return obj is DarkColor color && color._argbValue == _argbValue;
        }

        public override int GetHashCode()
        {
            return _argbValue.GetHashCode();
        }

        public static bool operator ==(DarkColor a, DarkColor b)
        {
            return a._argbValue == b._argbValue;
        }

        public static bool operator !=(DarkColor a, DarkColor b)
        {
            return a._argbValue != b._argbValue;
        }
    }
}

using System.IO;
using System.Text;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace TeleportationNetwork
{
    public class BlockPosArrayAttribute : ArrayAttribute<BlockPos>, IAttribute
    {
        public BlockPosArrayAttribute()
        {

        }

        public BlockPosArrayAttribute(BlockPos[] value)
        {
            this.value = value;
        }

        public void ToBytes(BinaryWriter stream)
        {
            stream.Write(value.Length);
            for (int i = 0; i < value.Length; i++)
            {
                stream.Write(value[i] == null);
                if (value != null)
                {
                    value[i].ToBytes(stream);
                }
            }

        }

        public void FromBytes(BinaryReader stream)
        {
            int quantity = stream.ReadInt32();
            value = new BlockPos[quantity];
            for (int i = 0; i < quantity; i++)
            {
                bool isNull = stream.ReadBoolean();
                if (!isNull)
                {
                    value[i] = BlockPos.CreateFromBytes(stream);
                }
            }
        }

        public int GetAttributeId()
        {
            return Constants.ATTRIBUTES_ID + 1;
        }

        public override string ToJsonToken()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("[");

            for (int i = 0; i < value.Length; i++)
            {
                if (i > 0) sb.Append(", ");

                sb.Append("{");
                sb.Append("x: " + value[i].X.ToString(GlobalConstants.DefaultCultureInfo) + ", ");
                sb.Append("y: " + value[i].Y.ToString(GlobalConstants.DefaultCultureInfo) + ", ");
                sb.Append("z: " + value[i].Z.ToString(GlobalConstants.DefaultCultureInfo));
                sb.Append("}");
            }
            sb.Append("]");

            return sb.ToString();
        }


    }
}
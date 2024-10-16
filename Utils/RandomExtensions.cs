using System.Collections.Generic;
using Vintagestory.API.MathTools;

//TODO: Move to CommonLib
namespace CommonLib.Extensions
{
    public static class RandomExtensions
    {
        public static T GetItem<T>(this LCGRandom random, IList<T> values)
        {
            return values[random.NextInt(values.Count)];
        }
    }
}

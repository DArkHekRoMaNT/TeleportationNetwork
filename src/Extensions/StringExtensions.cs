using System;

namespace TeleportationNetwork
{
    public static class StringExtensions
    {
        public static string Shuffle(this string str, Random rnd)
        {
            char[] array = str.ToCharArray();
            int n = array.Length;
            while (n > 1)
            {
                n--;
                int k = rnd.Next(n + 1);

                if (array[k] == ' ' || array[n] == ' ') continue;

                var value = array[k];

                array[k] = char.IsUpper(array[k]) ? char.ToUpper(array[n]) : char.ToLower(array[n]);
                array[n] = char.IsUpper(array[n]) ? char.ToUpper(value) : char.ToLower(value);
            }
            return new string(array);
        }
    }
}
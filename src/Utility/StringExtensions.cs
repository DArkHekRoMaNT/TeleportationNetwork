using System;

namespace TeleportationNetwork
{
    public static class StringExtensions
    {
        public static string Shuffle(this string str)
        {
            char[] array = str.ToCharArray();
            Random rng = new Random();
            int n = array.Length;
            while (n > 1)
            {
                n--;
                int k = rng.Next(n + 1);

                if (array[k] == ' ' || array[n] == ' ') continue;

                var value = array[k];

                array[k] = Char.IsUpper(array[k]) ? Char.ToUpper(array[n]) : Char.ToLower(array[n]);
                array[n] = Char.IsUpper(array[n]) ? Char.ToUpper(array[k]) : Char.ToLower(array[k]);
            }
            return new string(array);
        }
    }
}
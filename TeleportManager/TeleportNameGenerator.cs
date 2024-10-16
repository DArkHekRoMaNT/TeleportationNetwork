using System;
using Vintagestory.API.Common;

namespace TeleportationNetwork
{
    public class TeleportNameGenerator
    {
        private string[]? _adjectives;
        private string[]? _nouns;

        private readonly Random _random = new();

        public void Init(ICoreAPI api)
        {
            _adjectives = api.Assets.Get(new AssetLocation(Constants.ModId, "config/adjectives.json")).ToObject<string[]>();
            _nouns = api.Assets.Get(new AssetLocation(Constants.ModId, "config/nouns.json")).ToObject<string[]>();
        }

        public string Next()
        {
            if (_adjectives == null || _nouns == null || _adjectives.Length == 0 || _nouns.Length == 0)
            {
                return "null";
            }

            var adjective = _adjectives[_random.Next(0, _adjectives.Length)];
            var noun = _nouns[_random.Next(0, _nouns.Length)];
            return $"{adjective} {noun}";
        }
    }
}

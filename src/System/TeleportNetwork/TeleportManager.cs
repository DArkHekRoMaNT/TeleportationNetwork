using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace TeleportationNetwork
{
    public class TeleportManager : ITeleportManager
    {
        public Action<ITeleport> OnAdded { get; set; }
        public Action<ITeleport> OnRemoved { get; set; }
        public Action<ITeleport> OnModified { get; set; }
        public Action<ITeleport, IPlayer> OnActivatedByPlayer { get; set; }


        readonly Random rand;
        readonly List<string> defaultNames;
        Dictionary<BlockPos, ITeleport> teleports;

        public TeleportManager(List<string> defaultNames)
        {
            this.defaultNames = defaultNames;
            if (defaultNames == null || defaultNames.Count == 0)
            {
                this.defaultNames = new List<string>() { "null" };
            }

            rand = new Random();
            teleports = new Dictionary<BlockPos, ITeleport>();
        }

        public void SetTeleport(ITeleport teleport)
        {
            if (!teleports.ContainsKey(teleport.Pos))
            {
                teleports.Add(teleport.Pos, teleport);
                OnAdded?.Invoke(teleport);
            }
            else
            {
                teleports[teleport.Pos] = teleport;
                OnModified?.Invoke(teleport);
            }
        }

        public void RemoveTeleport(ITeleport teleport)
        {
            if (teleports.ContainsKey(teleport.Pos))
            {
                List<ITeleport> nodes = teleport.Neighbours
                    .Select((pos) => GetTeleport(pos))
                    .ToList();

                foreach (ITeleport node in nodes)
                {
                    if (node != null)
                    {
                        node.Neighbours.Remove(teleport.Pos);
                        SetTeleport(node);
                    }
                }

                teleports.Remove(teleport.Pos);
                OnRemoved?.Invoke(teleport);
            }
        }

        public ITeleport GetTeleport(BlockPos pos)
        {
            return pos != null && teleports.ContainsKey(pos) ? teleports[pos] : null;
        }

        public void SetTeleports(IEnumerable<ITeleport> teleports)
        {
            this.teleports = teleports.ToDictionary(e => e.Pos);
        }

        public void AddTeleports(IEnumerable<ITeleport> teleports)
        {
            foreach (ITeleport teleport in teleports)
            {
                SetTeleport(teleport);
                OnAdded?.Invoke(teleport);
            }
        }

        public List<ITeleport> GetAllTeleports(System.Func<ITeleport, bool> predicate = null)
        {
            if (predicate == null)
            {
                return teleports.Values.ToList();
            }
            else
            {
                return teleports.Values.Where(predicate).ToList();
            }
        }

        public List<ITeleport> GetAllEnabledTeleports() =>
            GetAllTeleports(teleport => teleport.Enabled);

        public List<ITeleport> GetAllEnabledActivatedByPlayer(IPlayer player) =>
            GetAllTeleports(teleport => teleport.Enabled &&
                teleport.ActivatedByPlayers.Contains(player.PlayerUID));

        public List<ITeleport> GetAllNeighbours(ITeleport teleport, System.Func<ITeleport, bool> predicate = null)
        {
            List<ITeleport> neighbours = new List<ITeleport>();
            Stack<ITeleport> stack = new Stack<ITeleport>(teleports.Count);

            foreach (BlockPos npos in teleport.Neighbours)
            {
                if (teleports.ContainsKey(npos))
                {
                    stack.Push(teleports[npos]);
                }
            }

            while (stack.Count > 0)
            {
                ITeleport node = stack.Pop();

                if (neighbours.Contains(node))
                {
                    continue;
                }

                neighbours.Add(node);

                if (predicate == null || predicate.Invoke(teleport))
                {
                    foreach (BlockPos npos in node.Neighbours)
                    {
                        if (teleports.ContainsKey(npos))
                        {
                            stack.Push(teleports[npos]);
                        }
                    }
                }
            }

            return neighbours;
        }

        public List<ITeleport> GetAllEnabledNeighbours(ITeleport teleport) =>
            GetAllNeighbours(teleport, (ntp) => ntp.Enabled);

        public List<ITeleport> GetAllEnabledNeighboursActivatedByPlayer(ITeleport teleport, IPlayer player) =>
            GetAllNeighbours(teleport, (ntp) => ntp.Enabled &&
                ntp.ActivatedByPlayers.Contains(player.PlayerUID));

        public void ActivateTeleport(ITeleport teleport, IPlayer player)
        {
            if (!teleport.ActivatedByPlayers.Contains(player.PlayerUID))
            {
                teleport.ActivatedByPlayers.Add(player.PlayerUID);
                OnActivatedByPlayer?.Invoke(teleport, player);
            }
        }

        public ITeleport CreateTeleport(BlockPos pos, bool enabled)
        {
            string state = enabled ? "normal" : "broken";
            Core.ModLogger.Debug("Start creating new {0} teleport at {1}", state, pos);

            Teleport teleport = new Teleport()
            {
                Name = defaultNames[rand.Next(defaultNames.Count)],
                Pos = pos,
                Enabled = enabled
            };

            Config.Current.MaxNetworkDistance = 10;

            teleport.Neighbours = FindNeighbours(teleport);

            foreach (BlockPos npos in teleport.Neighbours)
            {
                if (teleports.ContainsKey(npos))
                {
                    ITeleport ntp = teleports[npos];
                    ntp.Neighbours.Add(pos);
                    SetTeleport(ntp);
                }
            }

            SetTeleport(teleport);
            Core.ModLogger.Debug("Created new teleport {0} ({1}) at {2}", teleport.Name, state, teleport.Pos);

            return teleport;
        }

        public ITeleport GetOrCreateTeleport(BlockPos pos, bool enabled)
        {
            return GetTeleport(pos) ?? CreateTeleport(pos, enabled);
        }

        private List<BlockPos> FindNeighbours(ITeleport teleport)
        {
            List<BlockPos> neighbours = new List<BlockPos>();

            foreach (ITeleport node in teleports.Values)
            {
                if (node.Pos == teleport.Pos)
                {
                    continue;
                }

                if (node.Pos.ManhattenDistance(teleport.Pos) < Config.Current.MaxNetworkDistance)
                {
                    neighbours.Add(node.Pos);
                    Core.ModLogger.Notification(node.Name + " added as neighbour to " + teleport.Name);
                }
            }

            return neighbours;
        }
    }
}

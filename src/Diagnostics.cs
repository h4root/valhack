using System.Collections;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace ValheimAdminOverlay
{
    internal struct OwnerLoad
    {
        internal long Uid;
        internal string PlayerName;
        internal int ZdoCount;
        internal int Delta;
    }

    internal static class Diagnostics
    {
        private const float SampleInterval = 2f;
        private const int SpikeThreshold = 500;

        private static float _nextSample;
        private static int _lastTotalZdo;

        internal static int TotalZdo { get; private set; }
        internal static int ZdoDelta { get; private set; }
        internal static float Fps { get; private set; }
        internal static List<OwnerLoad> OwnerLoads { get; private set; } = new List<OwnerLoad>();
        internal static List<string> Warnings { get; } = new List<string>();

        private static readonly Dictionary<long, int> PreviousByOwner = new Dictionary<long, int>();

        internal static void Tick()
        {
            Fps = Mathf.Lerp(Fps, 1f / Mathf.Max(Time.unscaledDeltaTime, 0.0001f), 0.05f);

            if (Time.unscaledTime < _nextSample) return;
            _nextSample = Time.unscaledTime + SampleInterval;

            Sample();
        }

        private static void Sample()
        {
            var man = ZDOMan.instance;
            if (man == null) return;

            TotalZdo = man.NrOfObjects();
            ZdoDelta = TotalZdo - _lastTotalZdo;
            _lastTotalZdo = TotalZdo;

            if (ZNet.instance == null || !ZNet.instance.IsServer())
            {
                OwnerLoads = new List<OwnerLoad>();
                return;
            }

            OwnerLoads = BuildOwnerLoads(man);

            foreach (var load in OwnerLoads.Where(l => l.Delta >= SpikeThreshold))
            {
                var message = $"{load.PlayerName}: +{load.Delta} объектов за {SampleInterval:0} с (всего {load.ZdoCount})";
                Warnings.Add($"[{System.DateTime.Now:HH:mm:ss}] {message}");
                Log.Warn(message);

                if (Config.DiagnosticsInChat && Chat.instance != null)
                    Chat.instance.SendText(Talker.Type.Shout, message);
            }

            while (Warnings.Count > 50) Warnings.RemoveAt(0);
        }

        private static List<OwnerLoad> BuildOwnerLoads(ZDOMan man)
        {
            if (!(AccessTools.Field(typeof(ZDOMan), "m_objectsByID").GetValue(man) is IDictionary objects))
                return new List<OwnerLoad>();

            var counts = new Dictionary<long, int>();
            foreach (DictionaryEntry entry in objects)
            {
                if (!(entry.Value is ZDO zdo)) continue;
                var owner = zdo.GetOwner();
                counts.TryGetValue(owner, out var current);
                counts[owner] = current + 1;
            }

            var names = NamesByUid();
            var result = new List<OwnerLoad>();

            foreach (var pair in counts)
            {
                PreviousByOwner.TryGetValue(pair.Key, out var previous);
                result.Add(new OwnerLoad
                {
                    Uid = pair.Key,
                    PlayerName = names.TryGetValue(pair.Key, out var name) ? name : $"uid {pair.Key}",
                    ZdoCount = pair.Value,
                    Delta = pair.Value - previous
                });
                PreviousByOwner[pair.Key] = pair.Value;
            }

            return result.OrderByDescending(r => r.ZdoCount).ToList();
        }

        private static Dictionary<long, string> NamesByUid()
        {
            var map = new Dictionary<long, string>();

            var man = ZDOMan.instance;
            if (man != null)
            {
                var sessionId = (long)AccessTools.Field(typeof(ZDOMan), "m_sessionID").GetValue(man);
                map[sessionId] = Player.m_localPlayer != null
                    ? Player.m_localPlayer.GetPlayerName() + " (я)"
                    : "сервер";
            }

            foreach (var peer in Actions.Peers())
                map[peer.m_uid] = string.IsNullOrEmpty(peer.m_playerName) ? $"uid {peer.m_uid}" : peer.m_playerName;

            return map;
        }
    }
}

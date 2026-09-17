using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;

namespace ValheimAdminOverlay
{
    internal static class CheaterTag
    {
        private static string _rawCache;
        private static HashSet<string> _names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        internal static bool IsTagged(string playerName)
        {
            if (string.IsNullOrEmpty(playerName)) return false;
            RefreshCache();
            return _names.Contains(playerName.Trim());
        }

        internal static IEnumerable<string> Names
        {
            get { RefreshCache(); return _names; }
        }

        internal static void Toggle(string playerName)
        {
            RefreshCache();
            var next = new HashSet<string>(_names, StringComparer.OrdinalIgnoreCase);
            if (!next.Remove(playerName)) next.Add(playerName);
            Config.TaggedPlayers = string.Join(", ", next.ToArray());
            Config.Save();
            RefreshCache();
        }

        private static void RefreshCache()
        {
            var raw = Config.TaggedPlayers ?? string.Empty;
            if (raw == _rawCache) return;
            _rawCache = raw;
            _names = new HashSet<string>(
                raw.Split(',').Select(s => s.Trim()).Where(s => s.Length > 0),
                StringComparer.OrdinalIgnoreCase);
        }
    }

    [HarmonyPatch(typeof(EnemyHud), "UpdateHuds")]
    internal static class EnemyHudNameplatePatch
    {
        private static bool _disabled;
        private static System.Reflection.FieldInfo _hudsField;

        private static void Postfix(EnemyHud __instance)
        {
            if (_disabled || !Config.TagEnabled) return;

            try
            {
                if (_hudsField == null)
                    _hudsField = AccessTools.Field(typeof(EnemyHud), "m_huds");

                if (!(_hudsField.GetValue(__instance) is IDictionary huds))
                    return;

                foreach (DictionaryEntry entry in huds)
                {
                    if (!(entry.Key is Player player)) continue;

                    var name = player.GetPlayerName();
                    if (!CheaterTag.IsTagged(name)) continue;

                    var label = Traverse.Create(entry.Value).Field("m_name").GetValue();
                    if (label == null) continue;

                    Traverse.Create(label).Property("text")
                        .SetValue(Config.TagText + " " + name);
                }
            }
            catch (System.Exception e)
            {
                _disabled = true;
                Log.Error("метка отключена, не совместима с этой версией игры: " + e.Message);
            }
        }
    }
}

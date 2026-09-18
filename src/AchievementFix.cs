using System;
using System.Text;
using HarmonyLib;
using UnityEngine;

namespace ValheimAdminOverlay
{
    // Достижения не пропадают — игра их блокирует, когда считает персонажа
    // читерским. Признак постоянный: PlayerProfile.m_usedCheats пишется в профиль,
    // а заспавненные предметы помечаются отдельно. Здесь снимается и то и другое.
    internal static class AchievementFix
    {
        private static string _status = string.Empty;

        internal static string Status => _status;

        internal static bool Available
        {
            get
            {
                try
                {
                    return Achievements.CanGetAchievements();
                }
                catch (Exception)
                {
                    return false;
                }
            }
        }

        private static string _describeCache = string.Empty;
        private static float _describeAt;

        internal static string DescribeCached()
        {
            if (Time.unscaledTime < _describeAt) return _describeCache;

            _describeAt = Time.unscaledTime + 1f;
            _describeCache = Describe();
            return _describeCache;
        }

        internal static string Describe()
        {
            var text = new StringBuilder();

            try
            {
                text.Append(Available ? "достижения доступны" : "достижения заблокированы");

                var profile = Game.instance != null ? Game.instance.GetPlayerProfile() : null;
                if (profile != null)
                    text.Append(profile.m_usedCheats ? "  ·  профиль помечен" : "  ·  профиль чистый");

                var player = Player.m_localPlayer;
                if (player != null && player.GetInventory() != null && player.GetInventory().AnyCheatedItem())
                    text.Append("  ·  в инвентаре есть читерские предметы");

                if (Achievements.IsWorldCheated())
                    text.Append("  ·  мир помечен");
            }
            catch (Exception e)
            {
                return "состояние недоступно: " + e.Message;
            }

            return text.ToString();
        }

        internal static void Restore()
        {
            try
            {
                var profile = Game.instance != null ? Game.instance.GetPlayerProfile() : null;
                if (profile == null)
                {
                    _status = "профиль недоступен";
                    return;
                }

                profile.m_usedCheats = false;
                profile.Save();

                // Статический обход проверок, если он есть: без него отметка на
                // мире или предметах продолжит блокировать выдачу.
                var bypass = AccessTools.Property(typeof(PlayerProfile), "s_bypassCheatChecks");
                if (bypass != null && bypass.CanWrite)
                    bypass.SetValue(null, true, null);

                // Кэш проверки живёт один кадр, но сбросим явно, чтобы состояние
                // в интерфейсе обновилось сразу.
                AccessTools.Field(typeof(Achievements), "m_cheatCheckFrame")?.SetValue(null, -1);

                _status = Available
                    ? "готово, достижения снова доступны"
                    : "флаг профиля снят, но что-то ещё блокирует: " + Describe();

                Log.Info("восстановление достижений: " + _status);
            }
            catch (Exception e)
            {
                _status = "не удалось: " + e.Message;
                Log.Error("восстановление достижений: " + e);
            }
        }
    }
}

using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace ValheimAdminOverlay
{
    // Корабли в Valheim получают урон на сетевых рывках. Починка — штатный
    // WearNTear.Repair, но чинить можно только объект, которым владеешь,
    // поэтому владение сначала забирается.
    internal static class Ships
    {
        private const float AutoInterval = 2f;

        internal static bool AutoRepair;
        internal static float Radius = 30f;

        private static float _nextAuto;
        private static string _status = string.Empty;

        internal static string Status => _status;

        internal static void Tick()
        {
            if (!AutoRepair) return;
            if (Time.unscaledTime < _nextAuto) return;
            _nextAuto = Time.unscaledTime + AutoInterval;

            try
            {
                var repaired = RepairNearby(silent: true);
                if (repaired > 0)
                    _status = $"авто-починка: {repaired} в {DateTime.Now:HH:mm:ss}";
            }
            catch (Exception e)
            {
                AutoRepair = false;
                _status = "авто-починка отключена: " + e.Message;
                Log.Error("авто-починка кораблей: " + e);
            }
        }

        internal static int RepairNearby(bool silent = false)
        {
            var player = Player.m_localPlayer;
            if (player == null)
            {
                _status = "персонаж не загружен";
                return 0;
            }

            var origin = player.transform.position;
            var repaired = 0;

            foreach (var ship in Nearby(origin))
            {
                if (RepairShip(ship)) repaired++;
            }

            if (!silent)
                _status = repaired > 0
                    ? $"починено кораблей: {repaired}"
                    : "рядом нет повреждённых кораблей";

            if (repaired > 0 && !silent)
                Log.Info($"починено кораблей: {repaired}");

            return repaired;
        }

        private static IEnumerable<Ship> Nearby(Vector3 origin)
        {
            var all = UnityEngine.Object.FindObjectsOfType<Ship>();
            if (all == null) yield break;

            var maxSqr = Radius * Radius;

            foreach (var ship in all)
            {
                if (ship == null) continue;
                if ((ship.transform.position - origin).sqrMagnitude > maxSqr) continue;

                yield return ship;
            }
        }

        private static bool RepairShip(Ship ship)
        {
            // Корпус и все части корабля — отдельные WearNTear, чинить надо все.
            var parts = ship.GetComponentsInChildren<WearNTear>();
            if (parts == null || parts.Length == 0) return false;

            var touched = false;

            foreach (var part in parts)
            {
                if (part == null) continue;
                if (part.GetHealthPercentage() >= 0.999f) continue;

                var view = AccessTools.Field(typeof(WearNTear), "m_nview").GetValue(part) as ZNetView;
                if (view == null || !view.IsValid()) continue;

                if (!view.IsOwner()) view.ClaimOwnership();

                part.Repair();
                touched = true;
            }

            return touched;
        }

        private static float _healthCache = -1f;
        private static float _healthAt;

        // FindObjectsOfType сканирует всю сцену, поэтому в OnGUI его звать
        // каждый кадр нельзя: держим значение секунду.
        internal static float NearestHealthCached()
        {
            if (Time.unscaledTime < _healthAt) return _healthCache;

            _healthAt = Time.unscaledTime + 1f;
            _healthCache = NearestHealth();
            return _healthCache;
        }

        internal static float NearestHealth()
        {
            var player = Player.m_localPlayer;
            if (player == null) return -1f;

            var best = -1f;

            foreach (var ship in Nearby(player.transform.position))
            {
                var parts = ship.GetComponentsInChildren<WearNTear>();
                if (parts == null) continue;

                foreach (var part in parts)
                {
                    if (part == null) continue;

                    var health = part.GetHealthPercentage();
                    if (best < 0f || health < best) best = health;
                }
            }

            return best;
        }
    }
}

using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace ValheimAdminOverlay
{
    internal static class Cheats
    {
        internal static bool InfiniteStamina;
        internal static bool NoBuildCost;
        internal static float SpeedMultiplier = 1f;

        private static FieldInfo _stamina;
        private static FieldInfo _maxStamina;
        private static FieldInfo _noCost;

        private static bool _speedCaptured;
        private static float _baseWalk;
        private static float _baseRun;
        private static float _baseSwim;

        private static Vector3? _savedPoint;

        internal static bool HasSavedPoint => _savedPoint.HasValue;

        internal static void Tick()
        {
            var player = Player.m_localPlayer;
            if (player == null) return;

            if (InfiniteStamina)
            {
                if (_stamina == null) _stamina = AccessTools.Field(typeof(Player), "m_stamina");
                if (_maxStamina == null) _maxStamina = AccessTools.Field(typeof(Player), "m_maxStamina");
                _stamina.SetValue(player, (float)_maxStamina.GetValue(player));
            }

            if (_noCost == null) _noCost = AccessTools.Field(typeof(Player), "m_noPlacementCost");
            _noCost.SetValue(player, NoBuildCost);
        }

        internal static void SetSpeedMultiplier(float multiplier)
        {
            var player = Player.m_localPlayer;
            if (player == null) return;

            if (!_speedCaptured)
            {
                _baseWalk = player.m_walkSpeed;
                _baseRun = player.m_runSpeed;
                _baseSwim = player.m_swimSpeed;
                _speedCaptured = true;
            }

            SpeedMultiplier = Mathf.Clamp(multiplier, 1f, 10f);
            player.m_walkSpeed = _baseWalk * SpeedMultiplier;
            player.m_runSpeed = _baseRun * SpeedMultiplier;
            player.m_swimSpeed = _baseSwim * SpeedMultiplier;
        }

        internal static void SavePoint()
        {
            var player = Player.m_localPlayer;
            if (player != null) _savedPoint = player.transform.position;
        }

        internal static void GoToSavedPoint()
        {
            if (_savedPoint.HasValue)
                Actions.TeleportTo(_savedPoint.Value);
        }
    }
}

using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace ValheimAdminOverlay
{
    internal static class Actions
    {
        internal static bool HasLocalPlayer => Player.m_localPlayer != null;

        internal static bool IsHostOrDedicatedAdmin =>
            ZNet.instance != null && ZNet.instance.IsServer();

        // Сетевые соединения. На хосте это настоящие клиенты, и по их m_uid
        // диагностика сопоставляет владельцев объектов. Для списка игроков не
        // годится: у клиента здесь только сам сервер.
        internal static List<ZNetPeer> Peers()
        {
            return ZNet.instance != null ? ZNet.instance.GetPeers() : new List<ZNetPeer>();
        }

        // Настоящие игроки берутся из списка, который сервер рассылает всем
        // клиентам (им же игра рисует метки на карте). GetPeers() для этого не
        // годится: на выделенном сервере у клиента ровно один пир — сам сервер,
        // с именем-заглушкой и служебной позицией.
        internal static List<ZNet.PlayerInfo> Players()
        {
            var net = ZNet.instance;
            if (net == null) return new List<ZNet.PlayerInfo>();

            var all = net.GetPlayerList();
            if (all == null) return new List<ZNet.PlayerInfo>();

            var self = Player.m_localPlayer != null ? Player.m_localPlayer.GetPlayerName() : null;
            var result = new List<ZNet.PlayerInfo>();

            foreach (var info in all)
            {
                if (string.IsNullOrEmpty(info.m_name)) continue;
                if (self != null && info.m_name == self) continue;
                result.Add(info);
            }

            return result;
        }

        // Мир Valheim ограничен радиусом порядка 10000; всё, что вне его или
        // не является конечным числом, телепортировать нельзя — иначе персонаж
        // улетает за пределы мира, где нет ни земли, ни карты.
        internal static bool IsSaneTarget(Vector3 position)
        {
            if (float.IsNaN(position.x) || float.IsNaN(position.y) || float.IsNaN(position.z)) return false;
            if (float.IsInfinity(position.x) || float.IsInfinity(position.y) || float.IsInfinity(position.z)) return false;

            if (new Vector2(position.x, position.z).magnitude > 10500f) return false;

            return position.y > -1000f && position.y < 5000f;
        }

        internal static void ToggleGodMode()
        {
            var p = Player.m_localPlayer;
            if (p == null) return;
            p.SetGodMode(!p.InGodMode());
        }

        internal static bool GodModeOn => Player.m_localPlayer != null && Player.m_localPlayer.InGodMode();

        internal static void ToggleFly()
        {
            var p = Player.m_localPlayer;
            if (p == null) return;
            var field = AccessTools.Field(typeof(Player), "m_debugFly");
            field.SetValue(p, !(bool)field.GetValue(p));
        }

        internal static bool FlyOn
        {
            get
            {
                var p = Player.m_localPlayer;
                return p != null && (bool)AccessTools.Field(typeof(Player), "m_debugFly").GetValue(p);
            }
        }

        internal static void HealFull()
        {
            var p = Player.m_localPlayer;
            if (p == null) return;
            p.Heal(p.GetMaxHealth(), true);
        }

        internal static string LastTeleportError { get; private set; }

        internal static void TeleportTo(Vector3 position)
        {
            var p = Player.m_localPlayer;
            if (p == null) return;

            if (!IsSaneTarget(position))
            {
                LastTeleportError = $"точка вне мира: {position.x:0}, {position.y:0}, {position.z:0}";
                Log.Warn("телепорт отклонён, " + LastTeleportError);
                return;
            }

            var target = position;
            var zones = ZoneSystem.instance;
            if (zones != null && zones.GetSolidHeight(new Vector3(target.x, 0f, target.z), out var solid))
                target.y = Mathf.Max(target.y, solid + 2f);

            Cheats.RememberUndo(p.transform.position);
            LastTeleportError = null;
            p.TeleportTo(target, p.transform.rotation, true);
        }

        internal static void TeleportToPlayer(ZNet.PlayerInfo info)
        {
            if (!info.m_publicPosition)
            {
                LastTeleportError = info.m_name + " не делится позицией";
                return;
            }

            TeleportTo(info.m_position + Vector3.up * 2f);
        }

        internal static void ExploreMap()
        {
            if (Minimap.instance != null)
                Minimap.instance.ExploreAll();
        }

        internal static void SkipToMorning()
        {
            if (EnvMan.instance != null)
                EnvMan.instance.SkipToMorning();
        }

        internal static void SetTimeOfDay(float fraction)
        {
            var env = EnvMan.instance;
            if (env == null) return;
            env.m_debugTimeOfDay = true;
            env.m_debugTime = Mathf.Clamp01(fraction);
        }

        internal static void ReleaseTimeOfDay()
        {
            if (EnvMan.instance != null)
                EnvMan.instance.m_debugTimeOfDay = false;
        }

        internal static void ForceWeather(string environment)
        {
            if (EnvMan.instance != null)
                EnvMan.instance.SetForceEnvironment(environment);
        }

        internal static void SaveWorld()
        {
            if (ZNet.instance != null && ZNet.instance.IsServer())
                ZNet.instance.SaveWorldAndPlayerProfiles();
        }

        internal static void Kick(string playerName)
        {
            if (ZNet.instance != null)
                ZNet.instance.Kick(playerName);
        }

        internal static void Ban(string playerName)
        {
            if (ZNet.instance != null)
                ZNet.instance.Ban(playerName);
        }
    }
}

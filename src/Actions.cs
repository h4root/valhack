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

        internal static List<ZNetPeer> Peers()
        {
            return ZNet.instance != null ? ZNet.instance.GetPeers() : new List<ZNetPeer>();
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

        internal static void TeleportTo(Vector3 position)
        {
            var p = Player.m_localPlayer;
            if (p == null) return;
            p.TeleportTo(position, p.transform.rotation, true);
        }

        internal static void TeleportToPeer(ZNetPeer peer)
        {
            if (peer == null) return;
            TeleportTo(peer.m_refPos + Vector3.up * 2f);
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

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace ValheimAdminOverlay
{
    internal struct EspTarget
    {
        internal Transform Transform;
        internal string Label;
        internal Color Color;
        internal float Height;
    }

    internal static class Esp
    {
        private const float RescanInterval = 0.5f;
        private const int MaxTargets = 220;

        private static readonly List<EspTarget> Targets = new List<EspTarget>();
        private static float _nextScan;

        private static FieldInfo _instancesField;
        private static FieldInfo _cameraField;
        private static GUIStyle _style;

        internal static void Tick()
        {
            if (!Config.EspPlayers && !Config.EspMobs && !Config.EspOres)
            {
                if (Targets.Count > 0) Targets.Clear();
                return;
            }

            if (Time.unscaledTime < _nextScan) return;
            _nextScan = Time.unscaledTime + RescanInterval;

            try
            {
                Rescan();
            }
            catch (Exception e)
            {
                Log.Error("ESP: сканирование не удалось, отключаю: " + e.Message);
                Config.EspPlayers = Config.EspMobs = Config.EspOres = false;
                Targets.Clear();
            }
        }

        private static void Rescan()
        {
            Targets.Clear();

            var player = Player.m_localPlayer;
            if (player == null) return;

            var origin = player.transform.position;
            var maxDistance = Config.EspDistance;

            if (Config.EspPlayers || Config.EspMobs)
                CollectCharacters(player, origin, maxDistance);

            if (Config.EspOres)
                CollectOres(origin, maxDistance);

            if (Targets.Count > MaxTargets)
            {
                Targets.Sort((a, b) =>
                    SqrDistance(a, origin).CompareTo(SqrDistance(b, origin)));
                Targets.RemoveRange(MaxTargets, Targets.Count - MaxTargets);
            }
        }

        private static float SqrDistance(EspTarget target, Vector3 origin)
        {
            return target.Transform == null
                ? float.MaxValue
                : (target.Transform.position - origin).sqrMagnitude;
        }

        private static void CollectCharacters(Player self, Vector3 origin, float maxDistance)
        {
            var characters = Character.GetAllCharacters();
            if (characters == null) return;

            foreach (var character in characters)
            {
                if (character == null || character == self) continue;
                if (character.IsDead()) continue;

                var isPlayer = character.IsPlayer();
                if (isPlayer && !Config.EspPlayers) continue;
                if (!isPlayer && !Config.EspMobs) continue;

                var distance = Vector3.Distance(origin, character.transform.position);
                if (distance > maxDistance) continue;

                string label;
                Color color;

                if (isPlayer)
                {
                    var name = character.GetHoverName();
                    label = $"{name}  {distance:0}м";
                    color = CheaterTag.IsTagged(name) ? Theme.Danger : Theme.Accent;
                }
                else
                {
                    var level = character.GetLevel();
                    var stars = level > 1 ? new string('*', level - 1) + " " : string.Empty;
                    label = $"{stars}{character.m_name}  {distance:0}м";
                    color = Theme.EspMob;
                }

                Targets.Add(new EspTarget
                {
                    Transform = character.transform,
                    Label = label,
                    Color = color,
                    Height = 2f
                });
            }
        }

        private static void CollectOres(Vector3 origin, float maxDistance)
        {
            var scene = ZNetScene.instance;
            if (scene == null) return;

            if (_instancesField == null)
                _instancesField = AccessTools.Field(typeof(ZNetScene), "m_instances");

            if (!(_instancesField.GetValue(scene) is IDictionary instances)) return;

            var keywords = Config.EspOreKeywordList;
            if (keywords.Length == 0) return;

            var maxSqr = maxDistance * maxDistance;

            foreach (DictionaryEntry entry in instances)
            {
                var view = entry.Value as ZNetView;
                if (view == null) continue;

                var go = view.gameObject;
                if (go == null) continue;

                var position = go.transform.position;
                if ((position - origin).sqrMagnitude > maxSqr) continue;

                var prefab = CleanName(go.name);
                if (!MatchesOre(prefab, keywords)) continue;

                Targets.Add(new EspTarget
                {
                    Transform = go.transform,
                    Label = $"{prefab}  {Vector3.Distance(origin, position):0}м",
                    Color = Theme.EspOre,
                    Height = 1f
                });
            }
        }

        private static bool MatchesOre(string prefab, string[] keywords)
        {
            foreach (var keyword in keywords)
                if (prefab.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;

            return false;
        }

        private static string CleanName(string name)
        {
            var clone = name.IndexOf("(Clone)", StringComparison.Ordinal);
            return clone >= 0 ? name.Substring(0, clone) : name;
        }

        internal static void Draw()
        {
            if (Targets.Count == 0) return;
            if (Event.current.type != EventType.Repaint) return;

            var camera = ResolveCamera();
            if (camera == null) return;

            if (_style == null)
            {
                _style = new GUIStyle
                {
                    fontSize = 11,
                    alignment = TextAnchor.MiddleCenter,
                    fontStyle = FontStyle.Bold
                };
            }

            var screenHeight = Screen.height;

            foreach (var target in Targets)
            {
                if (target.Transform == null) continue;

                var world = target.Transform.position + Vector3.up * target.Height;
                var point = camera.WorldToScreenPoint(world);
                if (point.z <= 0f) continue;

                var rect = new Rect(point.x - 100f, screenHeight - point.y - 9f, 200f, 18f);

                _style.normal.textColor = Color.black;
                GUI.Label(new Rect(rect.x + 1f, rect.y + 1f, rect.width, rect.height), target.Label, _style);

                _style.normal.textColor = target.Color;
                GUI.Label(rect, target.Label, _style);
            }
        }

        private static Camera ResolveCamera()
        {
            var gameCamera = GameCamera.instance;
            if (gameCamera != null)
            {
                if (_cameraField == null)
                    _cameraField = AccessTools.Field(typeof(GameCamera), "m_camera");

                if (_cameraField.GetValue(gameCamera) is Camera cam && cam != null)
                    return cam;
            }

            return Camera.main;
        }

        internal static int Count => Targets.Count;
    }
}

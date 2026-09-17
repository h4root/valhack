using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace ValheimAdminOverlay
{
    internal enum EspKind
    {
        Players = 0,
        Mobs = 1,
        Ores = 2,
        Containers = 3,
        Loot = 4
    }

    internal sealed class EspCategory
    {
        internal string Title;
        internal bool Enabled;
        internal bool ShowLabel = true;
        internal bool ShowBox;
        internal bool ShowGlow = true;
        internal Color Color;
    }

    internal struct EspTarget
    {
        internal Transform Transform;
        internal Renderer Renderer;
        internal string Label;
        internal EspKind Kind;
        internal float Height;
    }

    internal static class Esp
    {
        private const float RescanInterval = 0.5f;
        private const int MaxTargets = 260;

        private static readonly List<EspTarget> Targets = new List<EspTarget>();
        private static float _nextScan;

        private static FieldInfo _instancesField;
        private static FieldInfo _cameraField;
        private static GUIStyle _labelStyle;

        internal static readonly EspCategory[] Categories =
        {
            new EspCategory { Title = "Игроки" },
            new EspCategory { Title = "Мобы" },
            new EspCategory { Title = "Руда" },
            new EspCategory { Title = "Сундуки" },
            new EspCategory { Title = "Лут" }
        };

        internal static int Count => Targets.Count;

        internal static EspCategory Category(EspKind kind) => Categories[(int)kind];

        internal static void ApplyColorsFromConfig()
        {
            Category(EspKind.Players).Color = Config.EspColorPlayers;
            Category(EspKind.Mobs).Color = Config.EspColorMobs;
            Category(EspKind.Ores).Color = Config.EspColorOres;
            Category(EspKind.Containers).Color = Config.EspColorContainers;
            Category(EspKind.Loot).Color = Config.EspColorLoot;

            Category(EspKind.Players).Enabled = Config.EspPlayers;
            Category(EspKind.Mobs).Enabled = Config.EspMobs;
            Category(EspKind.Ores).Enabled = Config.EspOres;
            Category(EspKind.Containers).Enabled = Config.EspContainers;
            Category(EspKind.Loot).Enabled = Config.EspLoot;
        }

        internal static void StoreColorsToConfig()
        {
            Config.EspColorPlayers = Category(EspKind.Players).Color;
            Config.EspColorMobs = Category(EspKind.Mobs).Color;
            Config.EspColorOres = Category(EspKind.Ores).Color;
            Config.EspColorContainers = Category(EspKind.Containers).Color;
            Config.EspColorLoot = Category(EspKind.Loot).Color;

            Config.EspPlayers = Category(EspKind.Players).Enabled;
            Config.EspMobs = Category(EspKind.Mobs).Enabled;
            Config.EspOres = Category(EspKind.Ores).Enabled;
            Config.EspContainers = Category(EspKind.Containers).Enabled;
            Config.EspLoot = Category(EspKind.Loot).Enabled;
        }

        private static bool AnyEnabled()
        {
            if (!Config.EspEnabled) return false;

            foreach (var category in Categories)
                if (category.Enabled) return true;

            return false;
        }

        internal static void Tick()
        {
            if (!AnyEnabled())
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
                Config.EspEnabled = false;
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

            if (Category(EspKind.Players).Enabled || Category(EspKind.Mobs).Enabled)
                CollectCharacters(player, origin, maxDistance);

            if (Category(EspKind.Ores).Enabled || Category(EspKind.Containers).Enabled ||
                Category(EspKind.Loot).Enabled)
                CollectSceneObjects(origin, maxDistance);

            if (Targets.Count > MaxTargets)
            {
                Targets.Sort((a, b) => SqrDistance(a, origin).CompareTo(SqrDistance(b, origin)));
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
                if (character == null || character == self || character.IsDead()) continue;

                var isPlayer = character.IsPlayer();
                var kind = isPlayer ? EspKind.Players : EspKind.Mobs;
                if (!Category(kind).Enabled) continue;

                var distance = Vector3.Distance(origin, character.transform.position);
                if (distance > maxDistance) continue;

                string label;
                if (isPlayer)
                {
                    label = $"{character.GetHoverName()}  {distance:0}м";
                }
                else
                {
                    var level = character.GetLevel();
                    var stars = level > 1 ? new string('*', level - 1) + " " : string.Empty;
                    label = $"{stars}{character.m_name}  {distance:0}м";
                }

                Targets.Add(new EspTarget
                {
                    Transform = character.transform,
                    Renderer = character.GetComponentInChildren<Renderer>(),
                    Label = label,
                    Kind = kind,
                    Height = 2f
                });
            }
        }

        private static void CollectSceneObjects(Vector3 origin, float maxDistance)
        {
            var scene = ZNetScene.instance;
            if (scene == null) return;

            if (_instancesField == null)
                _instancesField = AccessTools.Field(typeof(ZNetScene), "m_instances");

            if (!(_instancesField.GetValue(scene) is IDictionary instances)) return;

            var oreKeywords = Config.EspOreKeywordList;
            var wantOres = Category(EspKind.Ores).Enabled && oreKeywords.Length > 0;
            var wantContainers = Category(EspKind.Containers).Enabled;
            var wantLoot = Category(EspKind.Loot).Enabled;
            var maxSqr = maxDistance * maxDistance;

            foreach (DictionaryEntry entry in instances)
            {
                var view = entry.Value as ZNetView;
                if (view == null) continue;

                var go = view.gameObject;
                if (go == null) continue;

                var position = go.transform.position;
                if ((position - origin).sqrMagnitude > maxSqr) continue;

                var distance = Vector3.Distance(origin, position);

                if (wantContainers)
                {
                    var container = go.GetComponent<Container>();
                    if (container != null)
                    {
                        Add(go, EspKind.Containers, $"{Clean(container.GetHoverName())}  {distance:0}м", 0.8f);
                        continue;
                    }
                }

                if (wantLoot)
                {
                    var item = go.GetComponent<ItemDrop>();
                    if (item != null)
                    {
                        var stack = item.m_itemData != null && item.m_itemData.m_stack > 1
                            ? $" x{item.m_itemData.m_stack}"
                            : string.Empty;
                        Add(go, EspKind.Loot, $"{Clean(item.GetHoverName())}{stack}  {distance:0}м", 0.4f);
                        continue;
                    }
                }

                if (!wantOres) continue;

                var prefab = Clean(go.name);
                if (!MatchesOre(prefab, oreKeywords)) continue;

                Add(go, EspKind.Ores, $"{prefab}  {distance:0}м", 1f);
            }
        }

        private static void Add(GameObject go, EspKind kind, string label, float height)
        {
            Targets.Add(new EspTarget
            {
                Transform = go.transform,
                Renderer = go.GetComponentInChildren<Renderer>(),
                Label = label,
                Kind = kind,
                Height = height
            });
        }

        private static bool MatchesOre(string prefab, string[] keywords)
        {
            foreach (var keyword in keywords)
                if (prefab.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;

            return false;
        }

        private static string Clean(string name)
        {
            if (string.IsNullOrEmpty(name)) return "?";

            var clone = name.IndexOf("(Clone)", StringComparison.Ordinal);
            if (clone >= 0) name = name.Substring(0, clone);

            var newline = name.IndexOf('\n');
            return newline >= 0 ? name.Substring(0, newline) : name;
        }

        internal static void Draw()
        {
            if (Targets.Count == 0 || !Config.EspEnabled) return;
            if (Event.current.type != EventType.Repaint) return;

            var camera = ResolveCamera();
            if (camera == null) return;

            if (_labelStyle == null)
                _labelStyle = new GUIStyle
                {
                    fontSize = 11,
                    alignment = TextAnchor.MiddleCenter,
                    fontStyle = FontStyle.Bold
                };

            var previousColor = GUI.color;
            var screenHeight = Screen.height;

            foreach (var target in Targets)
            {
                if (target.Transform == null) continue;

                var category = Category(target.Kind);
                if (!category.Enabled) continue;

                var world = target.Transform.position + Vector3.up * target.Height;
                var point = camera.WorldToScreenPoint(world);
                if (point.z <= 0f) continue;

                var screen = new Vector2(point.x, screenHeight - point.y);
                var bounds = ScreenBounds(camera, target, screenHeight);

                if (category.ShowGlow) DrawGlow(bounds, category.Color);
                if (category.ShowBox) DrawBox(bounds, category.Color);

                if (category.ShowLabel)
                {
                    var top = bounds.width > 0f ? bounds.yMin - 16f : screen.y - 16f;
                    var rect = new Rect(screen.x - 110f, top, 220f, 16f);

                    _labelStyle.normal.textColor = new Color(0f, 0f, 0f, 0.85f);
                    GUI.Label(new Rect(rect.x + 1f, rect.y + 1f, rect.width, rect.height), target.Label, _labelStyle);

                    _labelStyle.normal.textColor = category.Color;
                    GUI.Label(rect, target.Label, _labelStyle);
                }
            }

            GUI.color = previousColor;
        }

        // Экранный прямоугольник цели: берём габариты рендерера и проецируем
        // восемь углов. Если рендерера нет — небольшой запас вокруг точки.
        private static Rect ScreenBounds(Camera camera, EspTarget target, int screenHeight)
        {
            if (target.Renderer == null)
            {
                var p = camera.WorldToScreenPoint(target.Transform.position + Vector3.up * target.Height);
                var y = screenHeight - p.y;
                return new Rect(p.x - 16f, y - 16f, 32f, 32f);
            }

            var b = target.Renderer.bounds;
            var min = new Vector2(float.MaxValue, float.MaxValue);
            var max = new Vector2(float.MinValue, float.MinValue);

            for (var i = 0; i < 8; i++)
            {
                var corner = new Vector3(
                    (i & 1) == 0 ? b.min.x : b.max.x,
                    (i & 2) == 0 ? b.min.y : b.max.y,
                    (i & 4) == 0 ? b.min.z : b.max.z);

                var p = camera.WorldToScreenPoint(corner);
                if (p.z <= 0f) continue;

                var y = screenHeight - p.y;
                min = Vector2.Min(min, new Vector2(p.x, y));
                max = Vector2.Max(max, new Vector2(p.x, y));
            }

            if (min.x > max.x) return new Rect(0f, 0f, 0f, 0f);

            return new Rect(min.x, min.y, max.x - min.x, max.y - min.y);
        }

        private static void DrawGlow(Rect bounds, Color color)
        {
            if (bounds.width <= 0f || Theme.Glow == null) return;

            var padding = Mathf.Max(bounds.width, bounds.height) * Config.EspGlowSize;
            var rect = new Rect(bounds.x - padding, bounds.y - padding,
                bounds.width + padding * 2f, bounds.height + padding * 2f);

            GUI.color = new Color(color.r, color.g, color.b, Config.EspGlowStrength);
            GUI.DrawTexture(rect, Theme.Glow);
            GUI.color = Color.white;
        }

        private static void DrawBox(Rect bounds, Color color)
        {
            if (bounds.width <= 0f || Theme.Pixel == null) return;

            var thickness = Config.EspOutline;
            GUI.color = color;

            GUI.DrawTexture(new Rect(bounds.xMin, bounds.yMin, bounds.width, thickness), Theme.Pixel);
            GUI.DrawTexture(new Rect(bounds.xMin, bounds.yMax - thickness, bounds.width, thickness), Theme.Pixel);
            GUI.DrawTexture(new Rect(bounds.xMin, bounds.yMin, thickness, bounds.height), Theme.Pixel);
            GUI.DrawTexture(new Rect(bounds.xMax - thickness, bounds.yMin, thickness, bounds.height), Theme.Pixel);

            GUI.color = Color.white;
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
    }
}

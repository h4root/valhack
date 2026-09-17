using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

namespace ValheimAdminOverlay
{
    internal static class Config
    {
        internal static KeyCode ToggleKey = KeyCode.Insert;
        internal static KeyCode UnloadKey = KeyCode.End;
        internal static KeyCode FlyKey = KeyCode.V;
        internal static KeyCode GodKey = KeyCode.None;
        internal static KeyCode EspKey = KeyCode.None;

        internal static bool TagEnabled = true;
        internal static string TaggedPlayers = "";
        internal static string TagText = "<color=#ff4040>[ЧИТЕР]</color>";
        internal static bool DiagnosticsInChat;

        internal static bool EspPlayers;
        internal static bool EspMobs;
        internal static bool EspOres;
        internal static float EspDistance = 150f;
        internal static string EspOreKeywords = "minerock,silvervein,copper,tin,obsidian,meteorite,flametal";

        internal static float UiScale = 1f;
        internal static bool Animations = true;
        internal static bool HoverEffects = true;
        internal static int Radius = 6;
        internal static Rect Window = new Rect(60f, 60f, 620f, 520f);

        internal static Color ColorBg = Hex("15171CFA");
        internal static Color ColorSurface = Hex("1C1F27");
        internal static Color ColorAccent = Hex("6E9BFF");
        internal static Color ColorText = Hex("E7EAF0");
        internal static Color ColorMuted = Hex("868E9E");
        internal static Color ColorDanger = Hex("E5484D");
        internal static Color ColorOn = Hex("3DD68C");

        private static string _oreKeywordsCache;
        private static string[] _oreKeywordList = new string[0];

        internal static string[] EspOreKeywordList
        {
            get
            {
                var raw = EspOreKeywords ?? string.Empty;
                if (raw != _oreKeywordsCache)
                {
                    _oreKeywordsCache = raw;
                    _oreKeywordList = raw.Split(',')
                        .Select(k => k.Trim())
                        .Where(k => k.Length > 0)
                        .ToArray();
                }

                return _oreKeywordList;
            }
        }

        private static string _path;

        internal static void Load(string path)
        {
            _path = path;

            if (!File.Exists(path))
            {
                Save();
                return;
            }

            var v = Parse(File.ReadAllLines(path, Encoding.UTF8));

            ToggleKey = Key(v, "toggle_key", ToggleKey);
            UnloadKey = Key(v, "unload_key", UnloadKey);
            FlyKey = Key(v, "fly_key", FlyKey);
            GodKey = Key(v, "god_key", GodKey);
            EspKey = Key(v, "esp_key", EspKey);

            TagEnabled = Bool(v, "tag_enabled", TagEnabled);
            TaggedPlayers = Str(v, "tagged_players", TaggedPlayers);
            TagText = Str(v, "tag_text", TagText);
            DiagnosticsInChat = Bool(v, "diagnostics_in_chat", DiagnosticsInChat);

            EspPlayers = Bool(v, "esp_players", EspPlayers);
            EspMobs = Bool(v, "esp_mobs", EspMobs);
            EspOres = Bool(v, "esp_ores", EspOres);
            EspDistance = Num(v, "esp_distance", EspDistance);
            EspOreKeywords = Str(v, "esp_ore_keywords", EspOreKeywords);

            UiScale = Mathf.Clamp(Num(v, "ui_scale", UiScale), 0.6f, 2.5f);
            Animations = Bool(v, "animations", Animations);
            HoverEffects = Bool(v, "hover_effects", HoverEffects);
            Radius = Mathf.Clamp((int)Num(v, "corner_radius", Radius), 0, 16);

            Window = new Rect(
                Num(v, "window_x", Window.x),
                Num(v, "window_y", Window.y),
                Mathf.Max(360f, Num(v, "window_w", Window.width)),
                Mathf.Max(240f, Num(v, "window_h", Window.height)));

            ColorBg = Col(v, "color_bg", ColorBg);
            ColorSurface = Col(v, "color_surface", ColorSurface);
            ColorAccent = Col(v, "color_accent", ColorAccent);
            ColorText = Col(v, "color_text", ColorText);
            ColorMuted = Col(v, "color_muted", ColorMuted);
            ColorDanger = Col(v, "color_danger", ColorDanger);
            ColorOn = Col(v, "color_on", ColorOn);
        }

        internal static void Save()
        {
            if (string.IsNullOrEmpty(_path)) return;

            var t = new StringBuilder()
                .AppendLine("# Горячие клавиши. Имена из UnityEngine.KeyCode, None — не назначено")
                .AppendLine($"toggle_key = {ToggleKey}")
                .AppendLine($"unload_key = {UnloadKey}")
                .AppendLine($"fly_key = {FlyKey}")
                .AppendLine($"god_key = {GodKey}")
                .AppendLine($"esp_key = {EspKey}")
                .AppendLine()
                .AppendLine("# Внешний вид")
                .AppendLine($"ui_scale = {F(UiScale)}")
                .AppendLine($"animations = {B(Animations)}")
                .AppendLine($"hover_effects = {B(HoverEffects)}")
                .AppendLine($"corner_radius = {Radius}")
                .AppendLine($"window_x = {F(Window.x)}")
                .AppendLine($"window_y = {F(Window.y)}")
                .AppendLine($"window_w = {F(Window.width)}")
                .AppendLine($"window_h = {F(Window.height)}")
                .AppendLine()
                .AppendLine("# Цвета темы в HEX: RRGGBB или RRGGBBAA")
                .AppendLine($"color_bg = {HexOf(ColorBg)}")
                .AppendLine($"color_surface = {HexOf(ColorSurface)}")
                .AppendLine($"color_accent = {HexOf(ColorAccent)}")
                .AppendLine($"color_text = {HexOf(ColorText)}")
                .AppendLine($"color_muted = {HexOf(ColorMuted)}")
                .AppendLine($"color_danger = {HexOf(ColorDanger)}")
                .AppendLine($"color_on = {HexOf(ColorOn)}")
                .AppendLine()
                .AppendLine("# Метка над головой. Видна только там, где установлен оверлей")
                .AppendLine($"tag_enabled = {B(TagEnabled)}")
                .AppendLine($"tagged_players = {TaggedPlayers}")
                .AppendLine($"tag_text = {TagText}")
                .AppendLine()
                .AppendLine("# ESP: подсветка сквозь стены, только у вас на экране")
                .AppendLine($"esp_players = {B(EspPlayers)}")
                .AppendLine($"esp_mobs = {B(EspMobs)}")
                .AppendLine($"esp_ores = {B(EspOres)}")
                .AppendLine($"esp_distance = {F(EspDistance)}")
                .AppendLine($"esp_ore_keywords = {EspOreKeywords}")
                .AppendLine()
                .AppendLine($"diagnostics_in_chat = {B(DiagnosticsInChat)}")
                .ToString();

            try
            {
                File.WriteAllText(_path, t, Encoding.UTF8);
            }
            catch (Exception e)
            {
                Log.Error("не удалось сохранить конфиг: " + e.Message);
            }
        }

        internal static Color Hex(string hex)
        {
            if (string.IsNullOrEmpty(hex)) return Color.magenta;
            hex = hex.TrimStart('#');

            try
            {
                var r = Convert.ToInt32(hex.Substring(0, 2), 16) / 255f;
                var g = Convert.ToInt32(hex.Substring(2, 2), 16) / 255f;
                var b = Convert.ToInt32(hex.Substring(4, 2), 16) / 255f;
                var a = hex.Length >= 8 ? Convert.ToInt32(hex.Substring(6, 2), 16) / 255f : 1f;
                return new Color(r, g, b, a);
            }
            catch (Exception)
            {
                return Color.magenta;
            }
        }

        internal static string HexOf(Color c)
        {
            var r = Mathf.RoundToInt(Mathf.Clamp01(c.r) * 255f);
            var g = Mathf.RoundToInt(Mathf.Clamp01(c.g) * 255f);
            var b = Mathf.RoundToInt(Mathf.Clamp01(c.b) * 255f);
            var a = Mathf.RoundToInt(Mathf.Clamp01(c.a) * 255f);
            return a >= 255 ? $"{r:X2}{g:X2}{b:X2}" : $"{r:X2}{g:X2}{b:X2}{a:X2}";
        }

        private static string F(float value) => value.ToString("0.###", CultureInfo.InvariantCulture);
        private static string B(bool value) => value ? "true" : "false";

        private static Dictionary<string, string> Parse(IEnumerable<string> lines)
        {
            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                if (trimmed.Length == 0 || trimmed[0] == '#') continue;

                var sep = trimmed.IndexOf('=');
                if (sep <= 0) continue;

                values[trimmed.Substring(0, sep).Trim()] = trimmed.Substring(sep + 1).Trim();
            }

            return values;
        }

        private static string Str(Dictionary<string, string> v, string k, string fallback)
            => v.TryGetValue(k, out var raw) ? raw : fallback;

        private static bool Bool(Dictionary<string, string> v, string k, bool fallback)
            => v.TryGetValue(k, out var raw) && bool.TryParse(raw, out var parsed) ? parsed : fallback;

        private static float Num(Dictionary<string, string> v, string k, float fallback)
            => v.TryGetValue(k, out var raw) &&
               float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
                ? parsed
                : fallback;

        private static Color Col(Dictionary<string, string> v, string k, Color fallback)
        {
            if (!v.TryGetValue(k, out var raw)) return fallback;
            var parsed = Hex(raw);
            return parsed == Color.magenta ? fallback : parsed;
        }

        private static KeyCode Key(Dictionary<string, string> v, string k, KeyCode fallback)
        {
            if (!v.TryGetValue(k, out var raw)) return fallback;

            try
            {
                return (KeyCode)Enum.Parse(typeof(KeyCode), raw, true);
            }
            catch (Exception)
            {
                Log.Warn($"не разобрал клавишу '{raw}' для {k}, оставляю {fallback}");
                return fallback;
            }
        }
    }
}

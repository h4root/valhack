using System.Collections.Generic;
using UnityEngine;

namespace ValheimAdminOverlay
{
    internal static class Theme
    {
        internal static Color Bg, Surface, SurfaceHover, Line, Text, Muted;
        internal static Color Accent, AccentSoft, Danger, DangerSoft, On, OnSoft;
        internal static Color EspMob, EspOre;

        internal static GUISkin Skin;
        internal static GUIStyle Panel, Card, Title, Hint, Body, MutedLabel, SectionLabel;
        internal static GUIStyle Tab, TabActive, Btn, BtnDanger, BtnAccent;
        internal static GUIStyle RowLabel, RowMuted;
        internal static GUIStyle SectionHeader, SectionHeaderOpen, MasterOn, MasterOff;
        internal static GUIStyle ListRow, ListRowActive, SliderTrack, SliderThumb;
        internal static GUIStyle Check, CheckOn, Segment, SegmentActive;
        internal static Texture2D Glow, Pixel;
        internal static GUIStyle ToggleOn, ToggleOff, Close, Rule, Grip, Swatch;

        private static bool _ready;
        private static readonly List<Texture2D> Textures = new List<Texture2D>();

        internal static void EnsureInit()
        {
            if (_ready && Skin != null) return;
            Build();
        }

        // Полная пересборка: зовётся при смене цветов, радиуса или ховеров,
        // чтобы правки применялись сразу, без перезагрузки payload.
        internal static void Rebuild()
        {
            Dispose();
            Build();
        }

        private static void Build()
        {
            Bg = Config.ColorBg;
            Surface = Config.ColorSurface;
            Text = Config.ColorText;
            Muted = Config.ColorMuted;
            Accent = Config.ColorAccent;
            Danger = Config.ColorDanger;
            On = Config.ColorOn;

            SurfaceHover = Lighten(Surface, 0.08f);
            Line = Lighten(Surface, 0.12f);
            AccentSoft = Fade(Accent, 0.16f);
            DangerSoft = Fade(Danger, 0.14f);
            OnSoft = Fade(On, 0.16f);
            EspMob = Lerp(Danger, Color.yellow, 0.35f);
            EspOre = Hexish(0xE8C05A);

            // Единая шкала отступов: всё остальное считается от неё, чтобы
            // вертикальный ритм не разъезжался между вкладками.
            const int gap = 4;
            const int rowHeight = 26;

            var radius = Mathf.Clamp(Config.Radius, 0, 16);
            var panelTex = Rounded(Mathf.Max(radius, 2) + 4, Bg);
            var surfaceTex = Rounded(radius, Surface);
            var hoverTex = Rounded(radius, Config.HoverEffects ? SurfaceHover : Surface);
            var accentTex = Rounded(radius, AccentSoft);
            var dangerTex = Rounded(radius, DangerSoft);
            var onTex = Rounded(radius, OnSoft);
            var lineTex = Solid(Line);
            var clear = Solid(new Color(0f, 0f, 0f, 0f));

            Panel = new GUIStyle
            {
                normal = { background = panelTex },
                border = new RectOffset(radius + 4, radius + 4, radius + 4, radius + 4),
                padding = new RectOffset(14, 14, 12, 12)
            };

            Card = new GUIStyle
            {
                normal = { background = surfaceTex },
                border = new RectOffset(radius, radius, radius, radius),
                padding = new RectOffset(10, 10, 8, 8),
                margin = new RectOffset(0, 0, gap, gap)
            };

            Body = new GUIStyle
            {
                normal = { textColor = Text },
                fontSize = 12,
                wordWrap = true,
                padding = new RectOffset(2, 2, 3, 3)
            };

            Title = new GUIStyle(Body) { fontSize = 13, fontStyle = FontStyle.Bold, wordWrap = false };
            MutedLabel = new GUIStyle(Body) { normal = { textColor = Muted }, fontSize = 11, wordWrap = false };
            Hint = new GUIStyle(MutedLabel) { wordWrap = true, margin = new RectOffset(2, 2, gap, gap + 2) };

            // Подписи внутри строк: та же высота, что у кнопок, и центрирование —
            // иначе текст «плавает» относительно соседних кнопок в BeginHorizontal.
            RowLabel = new GUIStyle(Body)
            {
                wordWrap = false,
                alignment = TextAnchor.MiddleLeft,
                fixedHeight = rowHeight,
                margin = new RectOffset(2, gap, gap - 1, gap - 1)
            };
            RowMuted = new GUIStyle(RowLabel) { normal = { textColor = Muted }, fontSize = 11 };
            SectionLabel = new GUIStyle(Body)
            {
                normal = { textColor = Muted },
                fontSize = 10,
                fontStyle = FontStyle.Bold,
                padding = new RectOffset(2, 2, 0, 0),
                margin = new RectOffset(2, 2, gap * 3, gap),
                wordWrap = false
            };

            Btn = new GUIStyle
            {
                normal = { background = surfaceTex, textColor = Text },
                hover = { background = hoverTex, textColor = Text },
                active = { background = accentTex, textColor = Accent },
                border = new RectOffset(radius, radius, radius, radius),
                padding = new RectOffset(12, 12, 0, 0),
                margin = new RectOffset(0, gap, gap - 1, gap - 1),
                alignment = TextAnchor.MiddleCenter,
                fixedHeight = rowHeight,
                fontSize = 12
            };

            // Заливка сплошным акцентом, а не полупрозрачной подложкой: раньше
            // BtnAccent брал ту же текстуру, что и состояние "нажато" у обычной
            // кнопки, поэтому выглядел постоянно вдавленным.
            var accentFill = Rounded(radius, Accent);
            var accentPressed = Rounded(radius, Darken(Accent, 0.18f));
            var onAccent = new Color(Bg.r, Bg.g, Bg.b, 1f);

            BtnAccent = new GUIStyle(Btn)
            {
                normal = { background = accentFill, textColor = onAccent },
                hover = { background = Config.HoverEffects ? Rounded(radius, Lighten(Accent, 0.06f)) : accentFill, textColor = onAccent },
                active = { background = accentPressed, textColor = onAccent },
                fontStyle = FontStyle.Bold
            };

            BtnDanger = new GUIStyle(Btn)
            {
                normal = { background = surfaceTex, textColor = Danger },
                hover = { background = dangerTex, textColor = Danger },
                active = { background = dangerTex, textColor = Danger }
            };

            ToggleOff = new GUIStyle(Btn) { normal = { background = surfaceTex, textColor = Muted } };
            ToggleOn = new GUIStyle(Btn)
            {
                normal = { background = onTex, textColor = On },
                hover = { background = onTex, textColor = On }
            };

            Tab = new GUIStyle
            {
                normal = { background = clear, textColor = Muted },
                hover = { background = clear, textColor = Config.HoverEffects ? Text : Muted },
                active = { background = clear, textColor = Text },
                padding = new RectOffset(12, 12, 0, 0),
                fixedHeight = rowHeight + 2,
                margin = new RectOffset(0, gap, 0, 0),
                alignment = TextAnchor.MiddleCenter,
                fontSize = 12
            };

            TabActive = new GUIStyle(Tab)
            {
                normal = { background = accentTex, textColor = Accent },
                hover = { background = accentTex, textColor = Accent },
                border = new RectOffset(radius, radius, radius, radius),
                fontStyle = FontStyle.Bold
            };

            Close = new GUIStyle(Tab)
            {
                padding = new RectOffset(7, 7, 0, 0),
                fixedHeight = rowHeight,
                fontSize = 14,
                normal = { background = clear, textColor = Muted },
                hover = { background = dangerTex, textColor = Danger }
            };

            Rule = new GUIStyle
            {
                normal = { background = lineTex },
                fixedHeight = 1f,
                margin = new RectOffset(0, 0, gap + 2, gap + 2)
            };

            SectionHeader = new GUIStyle(Btn)
            {
                normal = { background = clear, textColor = Text },
                hover = { background = Config.HoverEffects ? hoverTex : clear, textColor = Text },
                active = { background = clear, textColor = Accent },
                alignment = TextAnchor.MiddleLeft,
                fontStyle = FontStyle.Bold,
                margin = new RectOffset(0, 0, gap * 2, gap)
            };

            SectionHeaderOpen = new GUIStyle(SectionHeader)
            {
                normal = { background = clear, textColor = Accent },
                hover = { background = Config.HoverEffects ? hoverTex : clear, textColor = Accent }
            };

            MasterOff = new GUIStyle(Btn)
            {
                normal = { background = surfaceTex, textColor = Muted },
                hover = { background = hoverTex, textColor = Text },
                alignment = TextAnchor.MiddleLeft,
                fixedHeight = rowHeight + 6,
                fontSize = 13
            };

            MasterOn = new GUIStyle(MasterOff)
            {
                normal = { background = onTex, textColor = On },
                hover = { background = onTex, textColor = On },
                fontStyle = FontStyle.Bold
            };

            ListRow = new GUIStyle(Btn)
            {
                normal = { background = surfaceTex, textColor = Muted },
                hover = { background = hoverTex, textColor = Text },
                alignment = TextAnchor.MiddleLeft
            };

            ListRowActive = new GUIStyle(ListRow)
            {
                normal = { background = accentTex, textColor = Accent },
                hover = { background = accentTex, textColor = Accent },
                fontStyle = FontStyle.Bold
            };

            // Дорожка рисуется текстурой во всю высоту ползунка, а сама линия
            // проходит по её центру — тогда круг садится ровно на линию,
            // а не висит над ней.
            const int thumbSize = 14;

            SliderTrack = new GUIStyle
            {
                normal = { background = TrackLine(thumbSize, 3, Line) },
                border = new RectOffset(2, 2, 0, 0),
                fixedHeight = thumbSize,
                margin = new RectOffset(0, gap, (rowHeight - thumbSize) / 2, 0)
            };

            SliderThumb = new GUIStyle
            {
                normal = { background = Rounded(thumbSize / 2, Accent) },
                active = { background = Rounded(thumbSize / 2, Accent) },
                border = new RectOffset(thumbSize / 2, thumbSize / 2, thumbSize / 2, thumbSize / 2),
                fixedWidth = thumbSize,
                fixedHeight = thumbSize,
                margin = new RectOffset(0, 0, 0, 0)
            };

            Check = new GUIStyle(Btn)
            {
                normal = { background = surfaceTex, textColor = Muted },
                hover = { background = hoverTex, textColor = Text },
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(9, 12, 0, 0)
            };

            CheckOn = new GUIStyle(Check)
            {
                normal = { background = onTex, textColor = On },
                hover = { background = onTex, textColor = On }
            };

            Segment = new GUIStyle(Btn)
            {
                margin = new RectOffset(0, 1, gap - 1, gap - 1),
                padding = new RectOffset(10, 10, 0, 0)
            };

            SegmentActive = new GUIStyle(Segment)
            {
                normal = { background = accentTex, textColor = Accent },
                hover = { background = accentTex, textColor = Accent },
                fontStyle = FontStyle.Bold
            };

            Pixel = Solid(Color.white);
            Glow = RadialGlow(48);

            Grip = new GUIStyle
            {
                normal = { background = Rounded(2, Line) },
                border = new RectOffset(2, 2, 2, 2)
            };

            Swatch = new GUIStyle
            {
                normal = { background = Rounded(3, Color.white) },
                border = new RectOffset(3, 3, 3, 3),
                margin = new RectOffset(2, 4, 4, 2)
            };

            Skin = Object.Instantiate(GUI.skin);
            Skin.hideFlags = HideFlags.HideAndDontSave;
            Skin.window = Panel;
            Skin.box = Card;
            Skin.button = Btn;
            Skin.label = Body;

            Skin.textField = new GUIStyle(Skin.textField)
            {
                normal = { background = surfaceTex, textColor = Text },
                focused = { background = hoverTex, textColor = Text },
                border = new RectOffset(radius, radius, radius, radius),
                padding = new RectOffset(8, 8, 0, 0),
                margin = new RectOffset(0, gap, gap - 1, gap - 1),
                fixedHeight = rowHeight,
                alignment = TextAnchor.MiddleLeft,
                fontSize = 12
            };

            StyleScrollbar(Skin.verticalScrollbar, clear);
            StyleScrollbar(Skin.horizontalScrollbar, clear);
            StyleThumb(Skin.verticalScrollbarThumb, Rounded(3, Line));
            StyleThumb(Skin.horizontalScrollbarThumb, Rounded(3, Line));
            Skin.verticalScrollbarUpButton = GUIStyle.none;
            Skin.verticalScrollbarDownButton = GUIStyle.none;
            Skin.horizontalScrollbarLeftButton = GUIStyle.none;
            Skin.horizontalScrollbarRightButton = GUIStyle.none;

            _ready = true;
        }

        internal static void Separator()
        {
            GUILayout.Box(GUIContent.none, Rule, GUILayout.ExpandWidth(true), GUILayout.Height(1f));
        }

        internal static void Dispose()
        {
            foreach (var tex in Textures)
                if (tex != null) Object.Destroy(tex);

            Textures.Clear();

            if (Skin != null) Object.Destroy(Skin);
            Skin = null;
            _ready = false;
        }

        private static Color Darken(Color c, float amount)
            => new Color(Mathf.Max(0f, c.r - amount), Mathf.Max(0f, c.g - amount), Mathf.Max(0f, c.b - amount), c.a);

        private static Color Lighten(Color c, float amount)
            => new Color(c.r + amount, c.g + amount, c.b + amount, c.a);

        private static Color Fade(Color c, float alpha) => new Color(c.r, c.g, c.b, alpha);

        private static Color Lerp(Color a, Color b, float t) => Color.Lerp(a, b, t);

        private static Color Hexish(int rgb) => new Color(
            ((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, 1f);

        private static void StyleScrollbar(GUIStyle style, Texture2D clear)
        {
            style.normal.background = clear;
            style.fixedWidth = style.fixedWidth > 0f ? 6f : 0f;
            style.fixedHeight = style.fixedHeight > 0f ? 6f : 0f;
            style.border = new RectOffset(0, 0, 0, 0);
            style.margin = new RectOffset(4, 0, 0, 0);
        }

        private static void StyleThumb(GUIStyle style, Texture2D thumb)
        {
            style.normal.background = thumb;
            style.hover.background = thumb;
            style.active.background = thumb;
            style.border = new RectOffset(3, 3, 3, 3);
            style.fixedWidth = style.fixedWidth > 0f ? 6f : 0f;
            style.fixedHeight = style.fixedHeight > 0f ? 6f : 0f;
        }

        // Мягкое пятно с затуханием к краям: рисуется под целью и даёт свечение
        // без шейдеров и без вмешательства в материалы игры.
        private static Texture2D RadialGlow(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.ARGB32, false)
            {
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };

            var pixels = new Color[size * size];
            var center = (size - 1) * 0.5f;

            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var dx = (x - center) / center;
                    var dy = (y - center) / center;
                    var distance = Mathf.Sqrt(dx * dx + dy * dy);
                    var alpha = Mathf.Clamp01(1f - distance);
                    pixels[y * size + x] = new Color(1f, 1f, 1f, alpha * alpha);
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();
            Textures.Add(tex);
            return tex;
        }

        // Прямоугольник заданной высоты с горизонтальной линией по центру.
        private static Texture2D TrackLine(int height, int thickness, Color color)
        {
            var tex = new Texture2D(1, height, TextureFormat.ARGB32, false)
            {
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };

            var from = (height - thickness) / 2;
            var pixels = new Color[height];

            for (var y = 0; y < height; y++)
                pixels[y] = y >= from && y < from + thickness ? color : new Color(0f, 0f, 0f, 0f);

            tex.SetPixels(pixels);
            tex.Apply();
            Textures.Add(tex);
            return tex;
        }

        private static Texture2D Solid(Color color)
        {
            var tex = new Texture2D(1, 1, TextureFormat.ARGB32, false) { hideFlags = HideFlags.HideAndDontSave };
            tex.SetPixel(0, 0, color);
            tex.Apply();
            Textures.Add(tex);
            return tex;
        }

        // Скруглённый прямоугольник со сглаженными краями, растягивается как 9-slice.
        private static Texture2D Rounded(int radius, Color color)
        {
            if (radius <= 0) return Solid(color);

            var size = radius * 2 + 1;
            var tex = new Texture2D(size, size, TextureFormat.ARGB32, false)
            {
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };

            var pixels = new Color[size * size];

            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var cx = Mathf.Min(x, size - 1 - x);
                    var cy = Mathf.Min(y, size - 1 - y);

                    float alpha;
                    if (cx >= radius || cy >= radius)
                    {
                        alpha = 1f;
                    }
                    else
                    {
                        var dx = radius - cx;
                        var dy = radius - cy;
                        alpha = Mathf.Clamp01(radius - Mathf.Sqrt(dx * dx + dy * dy) + 0.5f);
                    }

                    pixels[y * size + x] = new Color(color.r, color.g, color.b, color.a * alpha);
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();
            Textures.Add(tex);
            return tex;
        }
    }
}

using System;
using UnityEngine;

namespace ValheimAdminOverlay
{
    // Переиспользуемые элементы интерфейса. Вся раскладка собрана здесь, чтобы
    // вкладки описывали только содержание, а отступы и поведение были едиными.
    internal static class Ui
    {
        internal static bool Section(string title, bool expanded)
        {
            GUILayout.BeginHorizontal();
            var clicked = GUILayout.Button((expanded ? "▾  " : "▸  ") + title,
                expanded ? Theme.SectionHeaderOpen : Theme.SectionHeader);
            GUILayout.EndHorizontal();

            return clicked ? !expanded : expanded;
        }

        internal static bool Toggle(string label, bool value)
        {
            return GUILayout.Button(label, value ? Theme.ToggleOn : Theme.ToggleOff) ? !value : value;
        }

        // Мастер-переключатель: крупная строка, от которой зависит показ остального.
        internal static bool Master(string label, bool value)
        {
            GUILayout.BeginHorizontal();
            var clicked = GUILayout.Button((value ? "◉   " : "○   ") + label,
                value ? Theme.MasterOn : Theme.MasterOff);
            GUILayout.EndHorizontal();

            return clicked ? !value : value;
        }

        internal static float Slider(string label, float value, float min, float max,
            string format = "0", string suffix = "")
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, Theme.RowLabel, GUILayout.Width(150f));

            var result = GUILayout.HorizontalSlider(value, min, max,
                Theme.SliderTrack, Theme.SliderThumb, GUILayout.ExpandWidth(true));

            GUILayout.Label(result.ToString(format) + suffix, Theme.RowMuted, GUILayout.Width(62f));
            GUILayout.EndHorizontal();

            return result;
        }

        internal static int SliderInt(string label, int value, int min, int max, string suffix = "")
        {
            return Mathf.RoundToInt(Slider(label, value, min, max, "0", suffix));
        }

        // Вертикальный список: выбранная строка подсвечивается, её настройки
        // вкладка рисует следом сама.
        internal static int List(string[] items, int selected, Func<int, string> badge = null)
        {
            for (var i = 0; i < items.Length; i++)
            {
                GUILayout.BeginHorizontal();

                var active = i == selected;
                if (GUILayout.Button(items[i], active ? Theme.ListRowActive : Theme.ListRow))
                    selected = i;

                if (badge != null)
                {
                    var text = badge(i);
                    if (!string.IsNullOrEmpty(text))
                        GUILayout.Label(text, Theme.RowMuted, GUILayout.Width(70f));
                }

                GUILayout.EndHorizontal();
            }

            return selected;
        }

        internal static void Hint(string text)
        {
            GUILayout.Label(text, Theme.Hint);
        }

        internal static void Caption(string text)
        {
            GUILayout.Label(text, Theme.SectionLabel);
        }

        // Отступ для вложенных настроек, чтобы иерархия читалась без рамок.
        internal static void BeginIndent()
        {
            GUILayout.BeginHorizontal();
            GUILayout.Space(12f);
            GUILayout.BeginVertical();
        }

        internal static void EndIndent()
        {
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
        }

        internal static Color ColorField(string label, Color value)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, Theme.RowLabel, GUILayout.Width(150f));

            var previous = GUI.color;
            GUI.color = value;
            GUILayout.Box(GUIContent.none, Theme.Swatch, GUILayout.Width(22f), GUILayout.Height(16f));
            GUI.color = previous;

            var current = Config.HexOf(value);
            var edited = GUILayout.TextField(current, 8, GUILayout.Width(84f));
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            if (edited == current || edited.Length < 6) return value;

            var parsed = Config.Hex(edited);
            return parsed == Color.magenta ? value : parsed;
        }
    }
}

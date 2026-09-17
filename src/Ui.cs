using UnityEngine;

namespace ValheimAdminOverlay
{
    // Переиспользуемые элементы интерфейса.
    // Правила: дискретный набор из нескольких вариантов — сегментный переключатель,
    // непрерывная величина — слайдер, независимый вкл/выкл — чекбокс.
    // Ничто не растягивается на всю ширину без причины.
    internal static class Ui
    {
        private const float LabelWidth = 150f;
        private const float SliderWidth = 190f;
        private const float ValueWidth = 58f;

        internal static bool Section(string title, bool expanded)
        {
            var clicked = GUILayout.Button((expanded ? "▾  " : "▸  ") + title,
                expanded ? Theme.SectionHeaderOpen : Theme.SectionHeader);

            return clicked ? !expanded : expanded;
        }

        // Подпись всегда одна и та же, состояние показывает отметка слева.
        internal static bool Checkbox(string label, bool value, float width = 0f)
        {
            var content = (value ? "✓   " : "     ") + label;
            var style = value ? Theme.CheckOn : Theme.Check;

            var clicked = width > 0f
                ? GUILayout.Button(content, style, GUILayout.Width(width))
                : GUILayout.Button(content, style, GUILayout.ExpandWidth(false));

            return clicked ? !value : value;
        }

        internal static int Segmented(string label, string[] options, int index)
        {
            GUILayout.BeginHorizontal();

            if (!string.IsNullOrEmpty(label))
                GUILayout.Label(label, Theme.RowLabel, GUILayout.Width(LabelWidth));

            for (var i = 0; i < options.Length; i++)
                if (GUILayout.Button(options[i], i == index ? Theme.SegmentActive : Theme.Segment,
                        GUILayout.ExpandWidth(false)))
                    index = i;

            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            return index;
        }

        internal static float Slider(string label, float value, float min, float max,
            string format = "0", string suffix = "")
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, Theme.RowLabel, GUILayout.Width(LabelWidth));

            var result = GUILayout.HorizontalSlider(value, min, max,
                Theme.SliderTrack, Theme.SliderThumb, GUILayout.Width(SliderWidth));

            GUILayout.Label(result.ToString(format) + suffix, Theme.RowMuted, GUILayout.Width(ValueWidth));
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            return result;
        }

        internal static int SliderInt(string label, int value, int min, int max, string suffix = "")
        {
            return Mathf.RoundToInt(Slider(label, value, min, max, "0", suffix));
        }

        internal static int List(string[] items, int selected, float width = 220f)
        {
            for (var i = 0; i < items.Length; i++)
            {
                GUILayout.BeginHorizontal();

                if (GUILayout.Button(items[i], i == selected ? Theme.ListRowActive : Theme.ListRow,
                        GUILayout.Width(width)))
                    selected = i;

                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
            }

            return selected;
        }

        internal static void Caption(string text)
        {
            GUILayout.Label(text, Theme.SectionLabel);
        }

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
            GUILayout.Label(label, Theme.RowLabel, GUILayout.Width(LabelWidth));

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

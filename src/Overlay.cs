using System;
using System.Globalization;
using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace ValheimAdminOverlay
{
    internal static class Overlay
    {
        private const int WindowId = 0x5A11;
        private const float MinWidth = 380f;
        private const float MinHeight = 260f;

        private static readonly string[] Tabs =
            { "Игроки", "Персонаж", "ESP", "Мир", "Диагностика", "Настройки" };

        private static readonly string[] Weathers =
        {
            "Clear", "Misty", "Rain", "ThunderStorm", "SnowStorm", "Ashrain", "Twilight_Clear"
        };

        private static int _tab;
        private static Vector2 _scroll;
        private static string _teleportX = "0";
        private static string _teleportZ = "0";
        private static bool _cursorWasLocked;

        private static float _anim;
        private static bool _resizing;
        private static bool _geometryDirty;
        private static string _binding;

        internal static bool IsOpen { get; private set; }

        internal static void Toggle()
        {
            if (IsOpen) Close();
            else Open();
        }

        private static void Open()
        {
            IsOpen = true;
            _cursorWasLocked = Cursor.lockState == CursorLockMode.Locked;
            SetMouseCapture(false);
        }

        internal static void Close()
        {
            if (!IsOpen) return;

            IsOpen = false;
            _binding = null;
            _resizing = false;
            SetMouseCapture(_cursorWasLocked);
            SaveGeometryIfDirty();
        }

        // Анимация живёт в Update, а не в OnGUI: OnGUI вызывается несколько раз
        // за кадр (layout, repaint, события), и там время считать нельзя.
        internal static void Tick()
        {
            var target = IsOpen ? 1f : 0f;

            if (!Config.Animations)
            {
                _anim = target;
                return;
            }

            _anim = Mathf.MoveTowards(_anim, target, Time.unscaledDeltaTime * 7f);
        }

        private static void SetMouseCapture(bool capture)
        {
            Cursor.lockState = capture ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !capture;

            var camera = GameCamera.instance;
            if (camera != null)
                AccessTools.Field(typeof(GameCamera), "m_mouseCapture").SetValue(camera, capture);
        }

        internal static void Draw()
        {
            if (!IsOpen && _anim <= 0.001f) return;

            Theme.EnsureInit();

            CaptureBinding();

            if (IsOpen && Event.current.type == EventType.KeyDown &&
                Event.current.keyCode == KeyCode.Escape && _binding == null)
            {
                Close();
                return;
            }

            var previousSkin = GUI.skin;
            var previousMatrix = GUI.matrix;
            var previousColor = GUI.color;

            GUI.skin = Theme.Skin;

            var eased = Ease(_anim);
            GUIUtility.ScaleAroundPivot(Vector2.one * Config.UiScale, Vector2.zero);

            if (eased < 0.999f)
            {
                var pivot = new Vector2(Config.Window.x + Config.Window.width * 0.5f,
                                        Config.Window.y + Config.Window.height * 0.5f);
                GUIUtility.ScaleAroundPivot(Vector2.one * Mathf.Lerp(0.95f, 1f, eased), pivot);
                GUI.color = new Color(1f, 1f, 1f, eased);
            }

            Config.Window = GUILayout.Window(
                WindowId, Config.Window, DrawWindow, GUIContent.none, Theme.Panel,
                GUILayout.Width(Config.Window.width), GUILayout.Height(Config.Window.height));

            HandleResize();

            GUI.color = previousColor;
            GUI.matrix = previousMatrix;
            GUI.skin = previousSkin;
        }

        private static float Ease(float t) => t * t * (3f - 2f * t);

        private static void CaptureBinding()
        {
            if (_binding == null) return;
            if (Event.current.type != EventType.KeyDown) return;

            var key = Event.current.keyCode;
            if (key == KeyCode.None) return;

            if (key != KeyCode.Escape) Assign(_binding, key);

            _binding = null;
            Event.current.Use();
        }

        private static void Assign(string id, KeyCode key)
        {
            switch (id)
            {
                case "toggle": Config.ToggleKey = key; break;
                case "unload": Config.UnloadKey = key; break;
                case "fly": Config.FlyKey = key; break;
                case "god": Config.GodKey = key; break;
                case "esp": Config.EspKey = key; break;
            }

            Config.Save();
        }

        private static void HandleResize()
        {
            var e = Event.current;

            if (_resizing && e.type == EventType.MouseDrag)
            {
                Config.Window.width = Mathf.Max(MinWidth, e.mousePosition.x - Config.Window.x + 8f);
                Config.Window.height = Mathf.Max(MinHeight, e.mousePosition.y - Config.Window.y + 8f);
                _geometryDirty = true;
                e.Use();
            }
            else if (_resizing && e.type == EventType.MouseUp)
            {
                _resizing = false;
                SaveGeometryIfDirty();
                e.Use();
            }
        }

        private static void SaveGeometryIfDirty()
        {
            if (!_geometryDirty) return;
            _geometryDirty = false;
            Config.Save();
        }

        private static void DrawWindow(int id)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("Админ-оверлей", Theme.Title);
            GUILayout.Label(HeaderHint(), Theme.MutedLabel);
            GUILayout.FlexibleSpace();
            var closeClicked = GUILayout.Button("✕", Theme.Close, GUILayout.Width(24f));
            GUILayout.EndHorizontal();

            Theme.Separator();

            GUILayout.BeginHorizontal();
            for (var i = 0; i < Tabs.Length; i++)
                if (GUILayout.Button(Tabs[i], i == _tab ? Theme.TabActive : Theme.Tab))
                    _tab = i;
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            _scroll = GUILayout.BeginScrollView(_scroll, GUILayout.ExpandHeight(true));
            switch (_tab)
            {
                case 0: DrawPlayers(); break;
                case 1: DrawSelf(); break;
                case 2: DrawEsp(); break;
                case 3: DrawWorld(); break;
                case 4: DrawDiagnostics(); break;
                default: DrawSettings(); break;
            }
            GUILayout.EndScrollView();

            Theme.Separator();

            GUILayout.BeginHorizontal();
            GUILayout.Label(StatusLine(), Theme.MutedLabel);
            GUILayout.FlexibleSpace();
            GUILayout.Label("◢", Theme.MutedLabel, GUILayout.Width(14f));
            GUILayout.EndHorizontal();

            var grip = new Rect(Config.Window.width - 18f, Config.Window.height - 18f, 16f, 16f);
            if (Event.current.type == EventType.MouseDown && grip.Contains(Event.current.mousePosition))
            {
                _resizing = true;
                Event.current.Use();
            }

            GUI.DragWindow(new Rect(0f, 0f, Config.Window.width, 26f));

            if (closeClicked) Close();
        }

        private static string HeaderHint()
        {
            return $"{Config.ToggleKey}  ·  {Config.FlyKey} полёт  ·  F6 перезагрузка";
        }

        private static bool ToggleButton(string label, bool on)
        {
            return GUILayout.Button(label, on ? Theme.ToggleOn : Theme.ToggleOff);
        }

        private static string StatusLine()
        {
            var role = ZNet.instance == null
                ? "нет сети"
                : ZNet.instance.IsServer() ? "хост" : "клиент";
            return $"{role} · игроков {Actions.Peers().Count + 1} · объектов {Diagnostics.TotalZdo} · {Diagnostics.Fps:0} fps";
        }

        private static void DrawPlayers()
        {
            var peers = Actions.Peers();
            if (peers.Count == 0)
            {
                GUILayout.Label("Других игроков в сети нет.", Theme.Hint);
                return;
            }

            var local = Player.m_localPlayer;
            var admin = Actions.IsHostOrDedicatedAdmin;

            foreach (var peer in peers)
            {
                var name = string.IsNullOrEmpty(peer.m_playerName) ? "(без имени)" : peer.m_playerName;
                var distance = local != null ? Vector3.Distance(local.transform.position, peer.m_refPos) : 0f;
                var tagged = CheaterTag.IsTagged(name);

                GUILayout.BeginVertical(Theme.Card);

                GUILayout.BeginHorizontal();
                GUILayout.Label(tagged ? name + "  ⚑" : name, Theme.RowLabel);
                GUILayout.FlexibleSpace();
                GUILayout.Label($"{distance:0} м   {peer.m_refPos.x:0}, {peer.m_refPos.z:0}", Theme.RowMuted);
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                if (GUILayout.Button("К нему")) Actions.TeleportToPeer(peer);
                if (GUILayout.Button(tagged ? "Снять метку" : "Пометить")) CheaterTag.Toggle(name);
                GUILayout.FlexibleSpace();

                GUI.enabled = admin;
                if (GUILayout.Button("Кик", Theme.BtnDanger, GUILayout.Width(60f))) Actions.Kick(name);
                if (GUILayout.Button("Бан", Theme.BtnDanger, GUILayout.Width(60f))) Actions.Ban(name);
                GUI.enabled = true;
                GUILayout.EndHorizontal();

                GUILayout.EndVertical();
            }

            if (!admin)
                GUILayout.Label("Кик и бан доступны только хосту или админу сервера.", Theme.Hint);
        }

        private static void DrawSelf()
        {
            if (Player.m_localPlayer == null)
            {
                GUILayout.Label("Персонаж не загружен.", Theme.Hint);
                return;
            }

            GUILayout.Label("Работает только на вашем клиенте.", Theme.Hint);

            GUILayout.Label("РЕЖИМЫ", Theme.SectionLabel);
            GUILayout.BeginHorizontal();
            if (ToggleButton("Бессмертие", Actions.GodModeOn)) Actions.ToggleGodMode();
            if (ToggleButton("Полёт / noclip", Actions.FlyOn)) Actions.ToggleFly();
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (ToggleButton("Беск. стамина", Cheats.InfiniteStamina))
                Cheats.InfiniteStamina = !Cheats.InfiniteStamina;
            if (ToggleButton("Бесплатная стройка", Cheats.NoBuildCost))
                Cheats.NoBuildCost = !Cheats.NoBuildCost;
            GUILayout.EndHorizontal();

            GUILayout.Label("СКОРОСТЬ", Theme.SectionLabel);
            GUILayout.BeginHorizontal();
            GUILayout.Label($"x{Cheats.SpeedMultiplier:0.#}", Theme.RowLabel, GUILayout.Width(44f));
            foreach (var mult in new[] { 1f, 2f, 4f, 8f })
                if (GUILayout.Button($"x{mult:0}")) Cheats.SetSpeedMultiplier(mult);
            GUILayout.EndHorizontal();

            GUILayout.Label("ДЕЙСТВИЯ", Theme.SectionLabel);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Вылечить")) Actions.HealFull();
            if (GUILayout.Button("Открыть карту")) Actions.ExploreMap();
            GUILayout.EndHorizontal();

            GUILayout.Label("ТЕЛЕПОРТ", Theme.SectionLabel);
            GUILayout.BeginHorizontal();
            GUILayout.Label("X", Theme.RowMuted, GUILayout.Width(12f));
            _teleportX = GUILayout.TextField(_teleportX, GUILayout.Width(70f));
            GUILayout.Label("Z", Theme.RowMuted, GUILayout.Width(12f));
            _teleportZ = GUILayout.TextField(_teleportZ, GUILayout.Width(70f));
            if (GUILayout.Button("Перенести")) TeleportToTypedCoordinates();
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Запомнить точку")) Cheats.SavePoint();
            GUI.enabled = Cheats.HasSavedPoint;
            if (GUILayout.Button("Вернуться к точке")) Cheats.GoToSavedPoint();
            GUI.enabled = true;
            GUILayout.EndHorizontal();
        }

        private static void TeleportToTypedCoordinates()
        {
            if (!float.TryParse(_teleportX, NumberStyles.Float, CultureInfo.InvariantCulture, out var x) ||
                !float.TryParse(_teleportZ, NumberStyles.Float, CultureInfo.InvariantCulture, out var z))
                return;

            var height = 40f;
            var zones = ZoneSystem.instance;
            if (zones != null && zones.GetSolidHeight(new Vector3(x, 0f, z), out var solid))
                height = solid + 2f;

            Actions.TeleportTo(new Vector3(x, height, z));
        }

        private static void DrawEsp()
        {
            GUILayout.Label("Подсветка сквозь стены. Рисуется только у вас.", Theme.Hint);

            GUILayout.Label("ЦЕЛИ", Theme.SectionLabel);
            GUILayout.BeginHorizontal();
            if (ToggleButton("Игроки", Config.EspPlayers)) { Config.EspPlayers = !Config.EspPlayers; Config.Save(); }
            if (ToggleButton("Мобы", Config.EspMobs)) { Config.EspMobs = !Config.EspMobs; Config.Save(); }
            if (ToggleButton("Руда", Config.EspOres)) { Config.EspOres = !Config.EspOres; Config.Save(); }
            GUILayout.EndHorizontal();

            GUILayout.Label("РАДИУС", Theme.SectionLabel);
            GUILayout.BeginHorizontal();
            GUILayout.Label($"{Config.EspDistance:0} м", Theme.RowLabel, GUILayout.Width(52f));
            foreach (var radius in new[] { 50f, 100f, 150f, 300f, 600f })
                if (GUILayout.Button($"{radius:0}")) { Config.EspDistance = radius; Config.Save(); }
            GUILayout.EndHorizontal();

            GUILayout.Label("ЧТО СЧИТАТЬ РУДОЙ", Theme.SectionLabel);
            var keywords = GUILayout.TextField(Config.EspOreKeywords);
            if (keywords != Config.EspOreKeywords)
            {
                Config.EspOreKeywords = keywords;
                Config.Save();
            }

            GUILayout.Label($"Подсвечено целей: {Esp.Count}", Theme.MutedLabel);
            GUILayout.Label("Клиент знает только о прогруженных зонах вокруг вас — дальше них ESP ничего не покажет.", Theme.Hint);
        }

        private static void DrawWorld()
        {
            GUILayout.Label("ВРЕМЯ СУТОК", Theme.SectionLabel);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Утро")) Actions.SetTimeOfDay(0.25f);
            if (GUILayout.Button("Полдень")) Actions.SetTimeOfDay(0.5f);
            if (GUILayout.Button("Вечер")) Actions.SetTimeOfDay(0.75f);
            if (GUILayout.Button("Ночь")) Actions.SetTimeOfDay(0f);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Пропустить до утра")) Actions.SkipToMorning();
            if (GUILayout.Button("Обычный ход времени")) Actions.ReleaseTimeOfDay();
            GUILayout.EndHorizontal();

            GUILayout.Label("ПОГОДА", Theme.SectionLabel);
            var perRow = Mathf.Max(2, (int)((Config.Window.width - 40f) / 130f));
            for (var i = 0; i < Weathers.Length; i += perRow)
            {
                GUILayout.BeginHorizontal();
                for (var j = i; j < Mathf.Min(i + perRow, Weathers.Length); j++)
                    if (GUILayout.Button(Weathers[j])) Actions.ForceWeather(Weathers[j]);
                GUILayout.EndHorizontal();
            }

            GUILayout.Label("СЕРВЕР", Theme.SectionLabel);
            GUI.enabled = Actions.IsHostOrDedicatedAdmin;
            if (GUILayout.Button("Сохранить мир")) Actions.SaveWorld();
            GUI.enabled = true;
        }

        private static void DrawDiagnostics()
        {
            GUILayout.BeginVertical(Theme.Card);
            GUILayout.Label($"Объектов в мире: {Diagnostics.TotalZdo}    прирост: {Diagnostics.ZdoDelta:+#;-#;0}", Theme.Body);
            GUILayout.Label($"Кадров в секунду: {Diagnostics.Fps:0}", Theme.Body);
            GUILayout.EndVertical();

            if (!Actions.IsHostOrDedicatedAdmin)
            {
                GUILayout.Label("Разбивка по владельцам объектов считается только на хосте.", Theme.Hint);
            }
            else
            {
                GUILayout.Label("КТО СКОЛЬКО ОБЪЕКТОВ ДЕРЖИТ", Theme.SectionLabel);
                foreach (var load in Diagnostics.OwnerLoads.Take(12))
                {
                    GUILayout.BeginHorizontal();
                    GUILayout.Label(load.PlayerName, Theme.RowLabel);
                    GUILayout.FlexibleSpace();
                    GUILayout.Label($"{load.ZdoCount}   {load.Delta:+#;-#;0}", Theme.RowMuted);
                    GUILayout.EndHorizontal();
                }
            }

            GUILayout.Label("ВСПЛЕСКИ", Theme.SectionLabel);
            if (Diagnostics.Warnings.Count == 0)
                GUILayout.Label("пока ничего подозрительного", Theme.Hint);
            else
                foreach (var warning in Enumerable.Reverse(Diagnostics.Warnings).Take(15))
                    GUILayout.Label(warning, Theme.MutedLabel);
        }

        private static void DrawSettings()
        {
            GUILayout.Label("МАСШТАБ И ГЕОМЕТРИЯ", Theme.SectionLabel);
            GUILayout.BeginHorizontal();
            GUILayout.Label($"Масштаб x{Config.UiScale:0.00}", Theme.RowLabel, GUILayout.Width(110f));
            foreach (var scale in new[] { 0.8f, 1f, 1.25f, 1.5f, 2f })
                if (GUILayout.Button($"{scale:0.##}"))
                {
                    Config.UiScale = scale;
                    Config.Save();
                }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label($"Размер {Config.Window.width:0}×{Config.Window.height:0}", Theme.RowMuted);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Сбросить размер"))
            {
                Config.Window = new Rect(60f, 60f, 620f, 520f);
                Config.Save();
            }
            GUILayout.EndHorizontal();
            GUILayout.Label("Тянуть за уголок ◢ справа внизу.", Theme.Hint);

            GUILayout.Label("ЭФФЕКТЫ", Theme.SectionLabel);
            GUILayout.BeginHorizontal();
            if (ToggleButton("Анимации", Config.Animations))
            {
                Config.Animations = !Config.Animations;
                Config.Save();
            }
            if (ToggleButton("Ховеры", Config.HoverEffects))
            {
                Config.HoverEffects = !Config.HoverEffects;
                Config.Save();
                Theme.Rebuild();
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label($"Скругление {Config.Radius}px", Theme.RowLabel, GUILayout.Width(120f));
            foreach (var radius in new[] { 0, 3, 6, 10, 14 })
                if (GUILayout.Button(radius.ToString()))
                {
                    Config.Radius = radius;
                    Config.Save();
                    Theme.Rebuild();
                }
            GUILayout.EndHorizontal();

            GUILayout.Label("ЦВЕТА", Theme.SectionLabel);
            ColorRow("Фон", () => Config.ColorBg, c => Config.ColorBg = c);
            ColorRow("Поверхность", () => Config.ColorSurface, c => Config.ColorSurface = c);
            ColorRow("Акцент", () => Config.ColorAccent, c => Config.ColorAccent = c);
            ColorRow("Текст", () => Config.ColorText, c => Config.ColorText = c);
            ColorRow("Приглушённый", () => Config.ColorMuted, c => Config.ColorMuted = c);
            ColorRow("Опасность", () => Config.ColorDanger, c => Config.ColorDanger = c);
            ColorRow("Включено", () => Config.ColorOn, c => Config.ColorOn = c);

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Тёмная")) ApplyPreset("15171CFA", "1C1F27", "6E9BFF", "E7EAF0", "868E9E");
            if (GUILayout.Button("Тёплая")) ApplyPreset("1A1613FA", "241F1A", "C9A227", "F0E9DE", "9A9086");
            if (GUILayout.Button("Контраст")) ApplyPreset("000000F2", "141414", "00E0A4", "FFFFFF", "9A9A9A");
            GUILayout.EndHorizontal();

            GUILayout.Label("ГОРЯЧИЕ КЛАВИШИ", Theme.SectionLabel);
            KeyRow("Открыть меню", "toggle", Config.ToggleKey);
            KeyRow("Полёт / noclip", "fly", Config.FlyKey);
            KeyRow("Бессмертие", "god", Config.GodKey);
            KeyRow("ESP игроков", "esp", Config.EspKey);
            KeyRow("Выгрузить из процесса", "unload", Config.UnloadKey);
            if (_binding != null)
                GUILayout.Label("Нажмите клавишу. Escape — отмена.", Theme.Hint);

            GUILayout.Label("РАЗРАБОТКА", Theme.SectionLabel);
            GUILayout.Label("Хост держит меню отдельной сборкой и читает её из памяти, поэтому перезагрузка не требует перезапуска игры.", Theme.Hint);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Перезагрузить меню", Theme.BtnAccent))
                Loader.RequestReload?.Invoke();
            if (GUILayout.Button("Выгрузить меню", Theme.BtnDanger))
                Loader.RequestUnload?.Invoke();
            GUILayout.EndHorizontal();
            GUILayout.Label("После выгрузки меню вернёт F6 или панель хоста на F7.", Theme.Hint);
        }

        private static void ColorRow(string label, Func<Color> get, Action<Color> set)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, Theme.RowLabel, GUILayout.Width(120f));

            var previous = GUI.color;
            GUI.color = get();
            GUILayout.Box(GUIContent.none, Theme.Swatch, GUILayout.Width(22f), GUILayout.Height(16f));
            GUI.color = previous;

            var current = Config.HexOf(get());
            var edited = GUILayout.TextField(current, 8, GUILayout.Width(80f));
            if (edited != current && edited.Length >= 6)
            {
                var parsed = Config.Hex(edited);
                if (parsed != Color.magenta)
                {
                    set(parsed);
                    Config.Save();
                    Theme.Rebuild();
                }
            }

            GUILayout.EndHorizontal();
        }

        private static void ApplyPreset(string bg, string surface, string accent, string text, string muted)
        {
            Config.ColorBg = Config.Hex(bg);
            Config.ColorSurface = Config.Hex(surface);
            Config.ColorAccent = Config.Hex(accent);
            Config.ColorText = Config.Hex(text);
            Config.ColorMuted = Config.Hex(muted);
            Config.Save();
            Theme.Rebuild();
        }

        private static void KeyRow(string label, string id, KeyCode key)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, Theme.RowLabel, GUILayout.Width(180f));

            var listening = _binding == id;
            if (GUILayout.Button(listening ? "нажмите клавишу…" : key.ToString(),
                    listening ? Theme.BtnAccent : Theme.Btn, GUILayout.Width(150f)))
                _binding = listening ? null : id;

            if (GUILayout.Button("—", GUILayout.Width(30f)))
                Assign(id, KeyCode.None);

            GUILayout.EndHorizontal();
        }
    }
}

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
            { "Игроки", "Персонаж", "ESP", "Предметы", "Навыки", "Мир", "Диагностика", "Настройки" };

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
        private static int _espCategory;
        private static int _skillIndex;
        private static bool _secAppearance = true;
        private static bool _secColors;
        private static bool _secKeys;
        private static bool _secDev;

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
                case 3: DrawSpawner(); break;
                case 4: DrawSkills(); break;
                case 5: DrawWorld(); break;
                case 6: DrawDiagnostics(); break;
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
            var enabled = Ui.Master("ESP включён", Config.EspEnabled);
            if (enabled != Config.EspEnabled)
            {
                Config.EspEnabled = enabled;
                Config.Save();
            }

            if (!Config.EspEnabled)
            {
                Ui.Hint("Подсветка сквозь стены. Рисуется только у вас на экране.");
                return;
            }

            Esp.ApplyColorsFromConfig();

            Ui.Caption("КАТЕГОРИИ");
            _espCategory = Ui.List(
                Esp.Categories.Select(c => c.Title).ToArray(),
                _espCategory,
                i => Esp.Categories[i].Enabled ? "вкл" : "");

            var category = Esp.Categories[_espCategory];

            Ui.BeginIndent();
            var on = Ui.Toggle(category.Enabled ? "Показывать" : "Не показывать", category.Enabled);
            if (on != category.Enabled)
            {
                category.Enabled = on;
                Esp.StoreColorsToConfig();
                Config.Save();
            }

            if (category.Enabled)
            {
                GUILayout.BeginHorizontal();
                category.ShowGlow = Ui.Toggle("Свечение", category.ShowGlow);
                category.ShowBox = Ui.Toggle("Обводка", category.ShowBox);
                category.ShowLabel = Ui.Toggle("Подпись", category.ShowLabel);
                GUILayout.EndHorizontal();

                var color = Ui.ColorField("Цвет", category.Color);
                if (color != category.Color)
                {
                    category.Color = color;
                    Esp.StoreColorsToConfig();
                    Config.Save();
                }

                if (_espCategory == (int)EspKind.Ores)
                {
                    Ui.Caption("ЧТО СЧИТАТЬ РУДОЙ");
                    var keywords = GUILayout.TextField(Config.EspOreKeywords);
                    if (keywords != Config.EspOreKeywords)
                    {
                        Config.EspOreKeywords = keywords;
                        Config.Save();
                    }
                    Ui.Hint("Подстроки имён префабов через запятую.");
                }
            }
            Ui.EndIndent();

            Ui.Caption("ОБЩЕЕ");
            var distance = Ui.Slider("Радиус", Config.EspDistance, 1f, 600f, "0", " м");
            var glow = Ui.Slider("Сила свечения", Config.EspGlowStrength, 0f, 1f, "0.00");
            var glowSize = Ui.Slider("Размер свечения", Config.EspGlowSize, 0f, 1.5f, "0.00");
            var outline = Ui.Slider("Толщина обводки", Config.EspOutline, 1f, 6f, "0", " px");

            if (!Mathf.Approximately(distance, Config.EspDistance) ||
                !Mathf.Approximately(glow, Config.EspGlowStrength) ||
                !Mathf.Approximately(glowSize, Config.EspGlowSize) ||
                !Mathf.Approximately(outline, Config.EspOutline))
            {
                Config.EspDistance = distance;
                Config.EspGlowStrength = glow;
                Config.EspGlowSize = glowSize;
                Config.EspOutline = outline;
                Config.Save();
            }

            GUILayout.Label($"Подсвечено целей: {Esp.Count}", Theme.RowMuted);
            Ui.Hint("Клиент знает только о прогруженных зонах вокруг вас — дальше них ESP ничего не покажет.");
        }

        private static void DrawSpawner()
        {
            if (Player.m_localPlayer == null)
            {
                Ui.Hint("Персонаж не загружен.");
                return;
            }

            Ui.Caption("ПОИСК");
            var filter = GUILayout.TextField(Spawner.Filter);
            if (filter != Spawner.Filter) Spawner.Filter = filter;

            Spawner.Amount = Ui.SliderInt("Количество", Spawner.Amount, 1, 100, " шт");
            Spawner.Quality = Ui.SliderInt("Качество", Spawner.Quality, 1, 4);

            var items = Spawner.Items;
            Ui.Caption($"ПРЕДМЕТЫ  ({items.Count} из {Spawner.TotalCount})");

            if (items.Count == 0)
            {
                Ui.Hint("Ничего не найдено. Очистите поиск или зайдите в мир — база предметов грузится вместе с ним.");
                return;
            }

            foreach (var prefab in items)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label(Spawner.Clean(prefab.name), Theme.RowLabel);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Создать", Theme.BtnAccent, GUILayout.Width(90f)))
                    Spawner.Spawn(prefab);
                GUILayout.EndHorizontal();
            }

            if (!string.IsNullOrEmpty(Spawner.Status))
                GUILayout.Label(Spawner.Status, Theme.RowMuted);
        }

        private static void DrawSkills()
        {
            if (Player.m_localPlayer == null)
            {
                Ui.Hint("Персонаж не загружен.");
                return;
            }

            Ui.Caption("РЕЦЕПТЫ");
            if (GUILayout.Button("Открыть все рецепты", Theme.BtnAccent))
                Progression.UnlockRecipes();

            Ui.Caption("НАВЫКИ");
            Progression.SkillLevel = Ui.SliderInt("Уровень", Progression.SkillLevel, 0, 100);

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Применить ко всем"))
                Progression.SetAllSkills(Progression.SkillLevel);
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            var names = Progression.SkillNames;
            _skillIndex = Ui.List(names, _skillIndex);

            Ui.BeginIndent();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button($"Задать {Progression.SkillLevel}", Theme.BtnAccent, GUILayout.Width(130f)))
                Progression.SetSkill(names[_skillIndex], Progression.SkillLevel);
            if (GUILayout.Button("Сбросить", Theme.BtnDanger, GUILayout.Width(110f)))
                Progression.SetSkill(names[_skillIndex], 0);
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            Ui.EndIndent();

            if (!string.IsNullOrEmpty(Progression.Status))
                GUILayout.Label(Progression.Status, Theme.RowMuted);
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
            _secAppearance = Ui.Section("Внешний вид", _secAppearance);
            if (_secAppearance)
            {
                Ui.BeginIndent();

                var scale = Ui.Slider("Масштаб", Config.UiScale, 0.6f, 2.5f, "0.00", "x");
                var radius = Ui.SliderInt("Скругление", Config.Radius, 0, 16, " px");

                if (!Mathf.Approximately(scale, Config.UiScale))
                {
                    Config.UiScale = scale;
                    Config.Save();
                }

                if (radius != Config.Radius)
                {
                    Config.Radius = radius;
                    Config.Save();
                    Theme.Rebuild();
                }

                GUILayout.BeginHorizontal();
                var animations = Ui.Toggle("Анимации", Config.Animations);
                var hovers = Ui.Toggle("Ховеры", Config.HoverEffects);
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();

                if (animations != Config.Animations)
                {
                    Config.Animations = animations;
                    Config.Save();
                }

                if (hovers != Config.HoverEffects)
                {
                    Config.HoverEffects = hovers;
                    Config.Save();
                    Theme.Rebuild();
                }

                GUILayout.BeginHorizontal();
                GUILayout.Label($"Окно {Config.Window.width:0}×{Config.Window.height:0}", Theme.RowMuted);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Сбросить размер", GUILayout.Width(150f)))
                {
                    Config.Window = new Rect(60f, 60f, 620f, 520f);
                    Config.Save();
                }
                GUILayout.EndHorizontal();
                Ui.Hint("Размер меняется перетаскиванием уголка ◢ справа внизу.");

                Ui.EndIndent();
            }

            _secColors = Ui.Section("Цвета темы", _secColors);
            if (_secColors)
            {
                Ui.BeginIndent();

                var changed = false;
                changed |= Apply(Ui.ColorField("Фон", Config.ColorBg), ref Config.ColorBg);
                changed |= Apply(Ui.ColorField("Поверхность", Config.ColorSurface), ref Config.ColorSurface);
                changed |= Apply(Ui.ColorField("Акцент", Config.ColorAccent), ref Config.ColorAccent);
                changed |= Apply(Ui.ColorField("Текст", Config.ColorText), ref Config.ColorText);
                changed |= Apply(Ui.ColorField("Приглушённый", Config.ColorMuted), ref Config.ColorMuted);
                changed |= Apply(Ui.ColorField("Опасность", Config.ColorDanger), ref Config.ColorDanger);
                changed |= Apply(Ui.ColorField("Включено", Config.ColorOn), ref Config.ColorOn);

                if (changed)
                {
                    Config.Save();
                    Theme.Rebuild();
                }

                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Тёмная")) ApplyPreset("15171CFA", "1C1F27", "6E9BFF", "E7EAF0", "868E9E");
                if (GUILayout.Button("Тёплая")) ApplyPreset("1A1613FA", "241F1A", "C9A227", "F0E9DE", "9A9086");
                if (GUILayout.Button("Контраст")) ApplyPreset("000000F2", "141414", "00E0A4", "FFFFFF", "9A9A9A");
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();

                Ui.EndIndent();
            }

            _secKeys = Ui.Section("Горячие клавиши", _secKeys);
            if (_secKeys)
            {
                Ui.BeginIndent();
                KeyRow("Открыть меню", "toggle", Config.ToggleKey);
                KeyRow("Полёт / noclip", "fly", Config.FlyKey);
                KeyRow("Бессмертие", "god", Config.GodKey);
                KeyRow("ESP", "esp", Config.EspKey);
                KeyRow("Выгрузить из процесса", "unload", Config.UnloadKey);

                if (_binding != null)
                    Ui.Hint("Нажмите клавишу. Escape — отмена.");

                Ui.EndIndent();
            }

            _secDev = Ui.Section("Разработка", _secDev);
            if (_secDev)
            {
                Ui.BeginIndent();
                Ui.Hint("Меню живёт отдельной сборкой, хост читает её из памяти — перезагрузка не требует перезапуска игры.");

                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Перезагрузить меню", Theme.BtnAccent))
                    Loader.RequestReload?.Invoke();
                if (GUILayout.Button("Выгрузить меню", Theme.BtnDanger))
                    Loader.RequestUnload?.Invoke();
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();

                Ui.Hint("После выгрузки меню вернёт F6 или панель хоста на F7.");
                Ui.EndIndent();
            }
        }

        private static bool Apply(Color edited, ref Color target)
        {
            if (edited == target) return false;
            target = edited;
            return true;
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

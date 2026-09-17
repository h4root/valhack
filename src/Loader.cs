using System;
using System.IO;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace ValheimAdminOverlay
{
    // Эту сборку грузит в память хост (ValheimAdminOverlay.Host) уже после того,
    // как движок Unity поднят, поэтому здесь можно сразу работать с Unity.
    // Load и Unload вызываются хостом по рефлексии — сигнатуры менять нельзя.
    public static class Loader
    {
        private const string HarmonyId = "sapphire.valheim.adminoverlay";

        // Заполняются хостом после загрузки: меню не знает о его типах,
        // поэтому просит перезагрузку/выгрузку через делегаты.
        public static Action RequestReload;
        public static Action RequestUnload;

        private static GameObject _host;
        private static Harmony _harmony;

        public static void Load()
        {
            try
            {
                Log.Init(BaseDirectory);
                Config.Load(Path.Combine(BaseDirectory, "adminoverlay.cfg"));
            }
            catch (Exception e)
            {
                Log.Error("конфиг не прочитан, беру значения по умолчанию: " + e);
            }

            Esp.ApplyColorsFromConfig();

            try
            {
                CreateHost();
                Log.Info($"меню готово, клавиша {Config.ToggleKey}");
            }
            catch (Exception e)
            {
                Log.Error("не удалось создать хост: " + e);
                return;
            }

            try
            {
                _harmony = new Harmony(HarmonyId);
                _harmony.PatchAll(Assembly.GetExecutingAssembly());
                Log.Info("патчи установлены");
            }
            catch (Exception e)
            {
                Log.Error("патчи не встали, окно работает, часть функций нет: " + e);
            }
        }

        // Вызывается хостом перед загрузкой новой версии. Обязан снять патчи и
        // убрать свои объекты: выгрузить саму сборку Mono не умеет, и если этого
        // не сделать, от старой версии останутся висеть патчи и MonoBehaviour.
        public static void Unload()
        {
            try
            {
                Overlay.Close();
            }
            catch (Exception e)
            {
                Log.Error("окно не закрылось: " + e.Message);
            }

            try
            {
                _harmony?.UnpatchSelf();
            }
            catch (Exception e)
            {
                Log.Error("патчи не сняты: " + e.Message);
            }
            finally
            {
                _harmony = null;
            }

            try
            {
                Theme.Dispose();
            }
            catch (Exception e)
            {
                Log.Error("тема не освобождена: " + e.Message);
            }

            try
            {
                if (_host != null)
                {
                    UnityEngine.Object.Destroy(_host);
                    _host = null;
                }
            }
            catch (Exception e)
            {
                Log.Error("хост не удалён: " + e.Message);
            }

            Log.Info("выгружен");
        }

        private static void CreateHost()
        {
            if (_host != null) return;

            _host = new GameObject("AdminOverlayPayload");
            _host.AddComponent<OverlayBehaviour>();
            UnityEngine.Object.DontDestroyOnLoad(_host);

            Log.UnityAvailable = true;
        }

        internal static string BaseDirectory
        {
            get
            {
                // Сборка загружена из байтов, поэтому Location пуст — берём папку хоста.
                var location = Assembly.GetExecutingAssembly().Location;
                if (!string.IsNullOrEmpty(location))
                    return Path.GetDirectoryName(location);

                var host = Assembly.GetEntryAssembly();
                if (host != null && !string.IsNullOrEmpty(host.Location))
                    return Path.GetDirectoryName(host.Location);

                foreach (var candidate in AppDomain.CurrentDomain.GetAssemblies())
                {
                    if (candidate.GetName().Name != "ValheimAdminOverlay.Host") continue;
                    if (string.IsNullOrEmpty(candidate.Location)) continue;
                    return Path.GetDirectoryName(candidate.Location);
                }

                return Directory.GetCurrentDirectory();
            }
        }
    }
}

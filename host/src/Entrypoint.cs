using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ValheimAdminOverlay.Host
{
    internal static class HostBootstrap
    {
        private static GameObject _host;
        private static bool _started;
        private static bool _initialized;

        // Точка входа Doorstop вызывается до запуска движка Unity: здесь нельзя
        // ни создавать GameObject, ни звать Debug.Log, ни ставить Harmony-патчи.
        // Единственное безопасное действие — подписка на sceneLoaded, это чистый
        // managed-код. Всё остальное делаем уже из обработчика, на главном потоке.
        internal static void Start()
        {
            if (_started) return;
            _started = true;

            HostLog.Init(HostPaths.BaseDirectory);
            AssemblyResolver.Install();

            try
            {
                SceneManager.sceneLoaded += OnSceneLoaded;
                HostLog.Info("подписался на загрузку сцены");
            }
            catch (Exception e)
            {
                HostLog.Error("не удалось подписаться на sceneLoaded: " + e);
            }
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (_initialized) return;
            _initialized = true;

            try
            {
                SceneManager.sceneLoaded -= OnSceneLoaded;
            }
            catch (Exception)
            {
            }

            try
            {
                _host = new GameObject("AdminOverlayHostRoot");
                _host.AddComponent<HostBehaviour>();
                UnityEngine.Object.DontDestroyOnLoad(_host);
                HostLog.UnityAvailable = true;
                HostLog.Info($"хост поднят на сцене '{scene.name}', F6 — перезагрузка меню");
            }
            catch (Exception e)
            {
                HostLog.Error("не удалось поднять хост: " + e);
                return;
            }

            PayloadLoader.Load();
        }
    }
}

namespace Doorstop
{
    public static class Entrypoint
    {
        // Наружу в Doorstop не должно уйти ни одно исключение: там его никто
        // не ловит, и игра остаётся на чёрном экране.
        public static void Start()
        {
            try
            {
                ValheimAdminOverlay.Host.HostBootstrap.Start();
            }
            catch (Exception e)
            {
                ValheimAdminOverlay.Host.HostLog.Error("точка входа: " + e);
            }
        }
    }
}

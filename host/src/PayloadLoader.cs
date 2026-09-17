using System;
using System.IO;
using System.Reflection;

namespace ValheimAdminOverlay.Host
{
    // Payload читается в память, а не грузится с диска: так файл DLL не блокируется
    // и его можно перезаписывать прямо во время игры. Выгрузить сборку в Mono нельзя,
    // поэтому каждая перезагрузка оставляет предыдущую версию в памяти — для итераций
    // это приемлемо, но перед загрузкой новой обязательно зовём Unload у старой,
    // иначе останутся висеть её Harmony-патчи и объекты сцены.
    internal static class PayloadLoader
    {
        private const string PayloadFile = "ValheimAdminOverlay.dll";
        private const string LoaderType = "ValheimAdminOverlay.Loader";

        private static MethodInfo _unload;
        private static int _generation;

        internal static bool IsLoaded => _unload != null;

        // Меню просит перезагрузку/выгрузку из своего OnGUI. Делать это сразу
        // нельзя — оно снесёт само себя посреди отрисовки, поэтому копим запрос
        // и выполняем его в следующем Update хоста.
        internal static bool PendingReload;
        internal static bool PendingUnload;

        internal static void PumpPending()
        {
            if (PendingUnload)
            {
                PendingUnload = false;
                PendingReload = false;
                Unload();
                return;
            }

            if (PendingReload)
            {
                PendingReload = false;
                Reload();
            }
        }

        internal static string PayloadPath =>
            Path.Combine(HostPaths.BaseDirectory, PayloadFile);

        internal static void Load()
        {
            if (!File.Exists(PayloadPath))
            {
                HostLog.Error($"не найден {PayloadFile} рядом с хостом");
                return;
            }

            byte[] bytes;
            try
            {
                bytes = File.ReadAllBytes(PayloadPath);
            }
            catch (Exception e)
            {
                HostLog.Error("не удалось прочитать payload: " + e.Message);
                return;
            }

            try
            {
                var assembly = Assembly.Load(bytes);
                AssemblyResolver.Payload = assembly;

                var type = assembly.GetType(LoaderType, false);
                if (type == null)
                {
                    HostLog.Error($"в payload нет типа {LoaderType}");
                    return;
                }

                var load = type.GetMethod("Load", BindingFlags.Public | BindingFlags.Static);
                if (load == null)
                {
                    HostLog.Error("в payload нет Load()");
                    return;
                }

                _unload = type.GetMethod("Unload", BindingFlags.Public | BindingFlags.Static);

                load.Invoke(null, null);

                var reloadField = type.GetField("RequestReload", BindingFlags.Public | BindingFlags.Static);
                var unloadField = type.GetField("RequestUnload", BindingFlags.Public | BindingFlags.Static);
                reloadField?.SetValue(null, new Action(() => PendingReload = true));
                unloadField?.SetValue(null, new Action(() => PendingUnload = true));

                _generation++;
                HostLog.Info($"payload загружен, поколение {_generation}");
            }
            catch (Exception e)
            {
                _unload = null;
                HostLog.Error("payload не загрузился: " + e);
            }
        }

        internal static void Unload()
        {
            if (_unload == null) return;

            try
            {
                _unload.Invoke(null, null);
            }
            catch (Exception e)
            {
                HostLog.Error("payload не выгрузился чисто: " + e);
            }
            finally
            {
                _unload = null;
            }
        }

        internal static void Reload()
        {
            HostLog.Info("перезагрузка payload");
            Unload();
            Load();
        }
    }

    internal static class HostPaths
    {
        internal static string BaseDirectory
        {
            get
            {
                var location = Assembly.GetExecutingAssembly().Location;
                return string.IsNullOrEmpty(location)
                    ? Directory.GetCurrentDirectory()
                    : Path.GetDirectoryName(location);
            }
        }
    }
}

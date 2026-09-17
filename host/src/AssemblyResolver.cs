using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace ValheimAdminOverlay.Host
{
    // Payload грузится из байтов, поэтому у него нет пути на диске, и Mono ищет
    // его зависимости (0Harmony, MonoMod, Mono.Cecil) в папке игры, где их нет.
    // Этот резолвер подставляет их из папки рядом с хостом.
    internal static class AssemblyResolver
    {
        private static readonly Dictionary<string, Assembly> Cache =
            new Dictionary<string, Assembly>(StringComparer.OrdinalIgnoreCase);

        private static bool _installed;

        // Текущая версия payload: Harmony и прочие могут спросить её по имени,
        // а на диске лежит уже другая сборка, чем та, что реально загружена.
        internal static Assembly Payload;

        internal static void Install()
        {
            if (_installed) return;
            _installed = true;

            AppDomain.CurrentDomain.AssemblyResolve += Resolve;
        }

        private static Assembly Resolve(object sender, ResolveEventArgs args)
        {
            try
            {
                var simpleName = new AssemblyName(args.Name).Name;

                if (Payload != null &&
                    string.Equals(simpleName, Payload.GetName().Name, StringComparison.OrdinalIgnoreCase))
                    return Payload;

                if (Cache.TryGetValue(simpleName, out var cached))
                    return cached;

                var path = Path.Combine(HostPaths.BaseDirectory, simpleName + ".dll");
                if (!File.Exists(path))
                    return null;

                var assembly = Assembly.LoadFrom(path);
                Cache[simpleName] = assembly;
                HostLog.Info($"подставил зависимость {simpleName}");
                return assembly;
            }
            catch (Exception e)
            {
                HostLog.Error($"не смог разрешить {args.Name}: {e.Message}");
                return null;
            }
        }
    }
}

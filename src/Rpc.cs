using System;
using System.Collections;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace ValheimAdminOverlay
{
    // Имя сетевого обработчика нельзя прочитать из метаданных: игра хранит их
    // хешами. Зато можно посчитать хеш кандидата и проверить, есть ли он в
    // реестре ZNetView у собственного персонажа — устройство у всех одинаковое.
    internal static class Rpc
    {
        private static string _probe;

        internal static string ProbeResult => _probe;

        // Тот же алгоритм, что и у игры: иначе хеши не совпадут.
        internal static int StableHash(string value)
        {
            var a = 5381;
            var b = a;

            for (var i = 0; i < value.Length && value[i] != '\0'; i += 2)
            {
                a = ((a << 5) + a) ^ value[i];
                if (i == value.Length - 1 || value[i + 1] == '\0') break;
                b = ((b << 5) + b) ^ value[i + 1];
            }

            return a + b * 1566083941;
        }

        internal static void Probe()
        {
            var player = Player.m_localPlayer;
            if (player == null)
            {
                _probe = "персонаж не загружен";
                return;
            }

            try
            {
                var view = player.GetComponent<ZNetView>();
                if (view == null)
                {
                    _probe = "у персонажа нет ZNetView";
                    return;
                }

                if (!(AccessTools.Field(typeof(ZNetView), "m_functions").GetValue(view) is IDictionary functions))
                {
                    _probe = "реестр RPC недоступен";
                    return;
                }

                var candidates = new[] { "TeleportTo", "RPC_TeleportTo", "Teleport", "TeleportPlayer" };
                var found = string.Empty;

                foreach (var name in candidates)
                    if (functions.Contains(StableHash(name)))
                        found += (found.Length > 0 ? ", " : string.Empty) + name;

                _probe = found.Length > 0
                    ? "зарегистрированы: " + found
                    : $"телепорт-RPC не найден (всего обработчиков: {functions.Count})";

                Log.Info("проба RPC: " + _probe);
            }
            catch (Exception e)
            {
                _probe = "проба не удалась: " + e.Message;
                Log.Error(_probe);
            }
        }

        internal static string BuildStamp
        {
            get
            {
                try
                {
                    var mvid = Assembly.GetExecutingAssembly().ManifestModule.ModuleVersionId;
                    return mvid.ToString("N").Substring(0, 8);
                }
                catch (Exception)
                {
                    return "????????";
                }
            }
        }
    }
}

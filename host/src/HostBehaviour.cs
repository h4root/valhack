using System;
using System.IO;
using UnityEngine;

namespace ValheimAdminOverlay.Host
{
    internal sealed class HostBehaviour : MonoBehaviour
    {
        private const KeyCode ReloadKey = KeyCode.F6;
        private const KeyCode PanelKey = KeyCode.F7;
        private const float PollInterval = 1f;

        private bool _panel;
        private Rect _panelRect = new Rect(20f, 20f, 240f, 120f);
        private float _nextPoll;
        private DateTime _lastSeenWrite;
        private DateTime _pendingWrite;
        private bool _hasPending;

        private void Start()
        {
            _lastSeenWrite = SafeWriteTime();
        }

        private void Update()
        {
            try
            {
                if (Input.GetKeyDown(ReloadKey))
                {
                    PayloadLoader.Reload();
                    _lastSeenWrite = SafeWriteTime();
                    _hasPending = false;
                    return;
                }

                if (Input.GetKeyDown(PanelKey))
                    _panel = !_panel;

                PayloadLoader.PumpPending();

                if (Time.unscaledTime < _nextPoll) return;
                _nextPoll = Time.unscaledTime + PollInterval;

                PollForNewBuild();
            }
            catch (Exception e)
            {
                HostLog.Error("Update: " + e.Message);
            }
        }

        // Ждём, пока метка времени перестанет меняться: иначе можно подхватить
        // файл, который ещё копируется, и загрузить обрезанную сборку.
        private void PollForNewBuild()
        {
            var write = SafeWriteTime();
            if (write == default) return;

            if (write != _lastSeenWrite)
            {
                if (_hasPending && write == _pendingWrite)
                {
                    _lastSeenWrite = write;
                    _hasPending = false;
                    HostLog.Info("замечена новая сборка payload");
                    PayloadLoader.Reload();
                    return;
                }

                _pendingWrite = write;
                _hasPending = true;
            }
            else
            {
                _hasPending = false;
            }
        }

        private void OnGUI()
        {
            if (!_panel) return;

            _panelRect = GUI.Window(0x5A12, _panelRect, DrawPanel, "Хост оверлея");
        }

        private void DrawPanel(int id)
        {
            GUILayout.Space(4f);
            GUILayout.Label(PayloadLoader.IsLoaded ? "Меню загружено" : "Меню выгружено");

            if (GUILayout.Button(PayloadLoader.IsLoaded ? "Перезагрузить (F6)" : "Загрузить (F6)"))
                PayloadLoader.PendingReload = true;

            GUI.enabled = PayloadLoader.IsLoaded;
            if (GUILayout.Button("Выгрузить"))
                PayloadLoader.PendingUnload = true;
            GUI.enabled = true;

            if (GUILayout.Button("Скрыть панель (F7)"))
                _panel = false;

            GUI.DragWindow(new Rect(0f, 0f, 10000f, 20f));
        }

        private static DateTime SafeWriteTime()
        {
            try
            {
                var path = PayloadLoader.PayloadPath;
                return File.Exists(path) ? File.GetLastWriteTimeUtc(path) : default;
            }
            catch (Exception)
            {
                return default;
            }
        }
    }
}

using System;
using UnityEngine;

namespace ValheimAdminOverlay
{
    internal sealed class OverlayBehaviour : MonoBehaviour
    {
        private void Update()
        {
            try
            {
                if (Input.GetKeyDown(Config.UnloadKey))
                {
                    Loader.Unload();
                    return;
                }

                if (Input.GetKeyDown(Config.ToggleKey))
                    Overlay.Toggle();

                if (!Overlay.IsOpen)
                {
                    if (Config.FlyKey != KeyCode.None && Input.GetKeyDown(Config.FlyKey))
                        Actions.ToggleFly();

                    if (Config.GodKey != KeyCode.None && Input.GetKeyDown(Config.GodKey))
                        Actions.ToggleGodMode();

                    if (Config.EspKey != KeyCode.None && Input.GetKeyDown(Config.EspKey))
                    {
                        Config.EspPlayers = !Config.EspPlayers;
                        Config.Save();
                    }
                }

                Overlay.Tick();
                Cheats.Tick();
                Ships.Tick();
                Esp.Tick();
                Diagnostics.Tick();
            }
            catch (Exception e)
            {
                Log.Error("Update: " + e.Message);
            }
        }

        private void OnGUI()
        {
            try
            {
                Esp.Draw();
                Overlay.Draw();
            }
            catch (Exception e)
            {
                Log.Error("OnGUI: " + e.Message);
            }
        }
    }
}

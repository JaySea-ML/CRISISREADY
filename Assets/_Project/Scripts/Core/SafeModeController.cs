using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Hands;
using UnityEngine.XR.Management;
using XRInputDevice = UnityEngine.XR.InputDevice;
using XRInputDevices = UnityEngine.XR.InputDevices;
using XRInputDeviceCharacteristics = UnityEngine.XR.InputDeviceCharacteristics;
using XRCommonUsages = UnityEngine.XR.CommonUsages;
using MRCrisisTrainer.Logging;

namespace MRCrisisTrainer.Core
{
    /// <summary>
    /// Tryb bezpieczeństwa: gracz może w każdej chwili wstrzymać sesję (menu button / Esc),
    /// co pauzuje grę i pozwala przerwać. Wymóg etyczny z wytycznych (możliwość przerwania).
    /// </summary>
    public class SafeModeController : MonoBehaviour
    {
        public static SafeModeController Instance { get; private set; }

        [SerializeField] private GameObject pausePanel;
        public bool IsPaused { get; private set; }

        private readonly List<XRInputDevice> devices = new List<XRInputDevice>();

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        void Update()
        {
            if (MenuPressed()) TogglePause();
        }

        private bool MenuPressed()
        {
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame) return true;

            XRInputDevices.GetDevices(devices);
            foreach (var d in devices)
            {
                if (!d.isValid || (d.characteristics & XRInputDeviceCharacteristics.Controller) == 0) continue;
                if (d.TryGetFeatureValue(XRCommonUsages.menuButton, out bool m) && m) return true;
            }
            return false;
        }

        public void TogglePause()
        {
            SetPaused(!IsPaused);
        }

        public void SetPaused(bool paused)
        {
            IsPaused = paused;
            Time.timeScale = paused ? 0f : 1f;
            if (pausePanel != null) pausePanel.SetActive(paused);
            JSONLLogger.Instance?.LogEvent(paused ? "session_paused" : "session_resumed", null);
        }

        public void Resume() => SetPaused(false);

        public void AbortSession()
        {
            JSONLLogger.Instance?.LogEvent("session_aborted", null);
            Time.timeScale = 1f;
            IsPaused = false;
            if (SceneLoader.Instance != null)
                SceneLoader.Instance.LoadActAdditive("MainMenu", GetActiveSceneName());
        }

        private string GetActiveSceneName() =>
            UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;

        public void RegisterPausePanel(GameObject panel) => pausePanel = panel;
    }
}

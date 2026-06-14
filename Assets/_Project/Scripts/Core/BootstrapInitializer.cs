using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Hands;
using UnityEngine.XR.Management;
using MRCrisisTrainer.XR;
using XRInputDevice = UnityEngine.XR.InputDevice;
using XRInputDevices = UnityEngine.XR.InputDevices;
using XRInputDeviceCharacteristics = UnityEngine.XR.InputDeviceCharacteristics;
using XRCommonUsages = UnityEngine.XR.CommonUsages;
using Keyboard = UnityEngine.InputSystem.Keyboard;

namespace MRCrisisTrainer.Core
{
    public class BootstrapInitializer : MonoBehaviour
    {
        [SerializeField] private string firstActSceneName = "MainMenu";
        [SerializeField] private float autoStartDelay = 1.5f;
        [SerializeField] private bool waitForEnterKey = true;
        [SerializeField] private GameObject bootstrapUI;

        [Header("Hand pinch detection")]
        [Tooltip("Maksymalna odległość (m) między kciukiem a wskazującym żeby uznać pinch")]
        [SerializeField] private float pinchThreshold = 0.03f;
        [Tooltip("Jak długo pinch musi być utrzymany (sekundy) żeby uniknąć przypadkowych triggerów")]
        [SerializeField] private float pinchHoldDuration = 0.3f;

        private bool started;
        private readonly List<XRInputDevice> xrDevices = new List<XRInputDevice>();
        private XRHandSubsystem handSubsystem;
        private float pinchStartTime = -1f;

        void Start()
        {
            EnsurePersistentManagers();
            TryAcquireHandSubsystem();

            if (waitForEnterKey)
            {
                Debug.Log("[Bootstrap] Aby rozpocząć Story Mode:\n" +
                          "- ENTER / SPACE (klawiatura)\n" +
                          "- Trigger / A / B na kontrolerze\n" +
                          "- Pinch dłonią (kciuk + wskazujący razem)");
            }
            else
            {
                Invoke(nameof(StartFirstAct), autoStartDelay);
            }
        }

        private void TryAcquireHandSubsystem()
        {
            if (handSubsystem != null) return;
            var loader = XRGeneralSettings.Instance?.Manager?.activeLoader;
            if (loader != null) handSubsystem = loader.GetLoadedSubsystem<XRHandSubsystem>();
        }

        void Update()
        {
            if (started) return;
            if (!waitForEnterKey) return;

            if (IsKeyboardConfirm() || IsControllerConfirm() || IsHandPinchConfirm())
            {
                StartFirstAct();
            }
        }

        private bool IsKeyboardConfirm()
        {
            var kb = Keyboard.current;
            if (kb == null) return false;
            return kb.enterKey.wasPressedThisFrame
                   || kb.numpadEnterKey.wasPressedThisFrame
                   || kb.spaceKey.wasPressedThisFrame;
        }

        private bool IsControllerConfirm()
        {
            XRInputDevices.GetDevices(xrDevices);
            foreach (var device in xrDevices)
            {
                if (!device.isValid) continue;
                if ((device.characteristics & XRInputDeviceCharacteristics.Controller) == 0) continue;

                if (device.TryGetFeatureValue(XRCommonUsages.triggerButton, out bool trigger) && trigger) return true;
                if (device.TryGetFeatureValue(XRCommonUsages.primaryButton, out bool primary) && primary) return true;
                if (device.TryGetFeatureValue(XRCommonUsages.secondaryButton, out bool secondary) && secondary) return true;
                if (device.TryGetFeatureValue(XRCommonUsages.gripButton, out bool grip) && grip) return true;
            }
            return false;
        }

        private bool IsHandPinchConfirm()
        {
            if (handSubsystem == null) TryAcquireHandSubsystem();
            if (handSubsystem == null) return false;

            bool pinching = IsHandPinching(handSubsystem.leftHand) || IsHandPinching(handSubsystem.rightHand);

            if (pinching)
            {
                if (pinchStartTime < 0) pinchStartTime = Time.time;
                if (Time.time - pinchStartTime >= pinchHoldDuration) return true;
            }
            else
            {
                pinchStartTime = -1f;
            }
            return false;
        }

        private bool IsHandPinching(XRHand hand)
        {
            if (!hand.isTracked) return false;
            var thumb = hand.GetJoint(XRHandJointID.ThumbTip);
            var index = hand.GetJoint(XRHandJointID.IndexTip);
            if (!thumb.TryGetPose(out var thumbPose) || !index.TryGetPose(out var indexPose)) return false;
            return Vector3.Distance(thumbPose.position, indexPose.position) < pinchThreshold;
        }

        private void EnsurePersistentManagers()
        {
            if (GameStateManager.Instance == null)
            {
                new GameObject("GameStateManager").AddComponent<GameStateManager>();
            }
            if (SceneLoader.Instance == null)
            {
                new GameObject("SceneLoader").AddComponent<SceneLoader>();
            }
            if (Logging.JSONLLogger.Instance == null)
            {
                new GameObject("JSONLLogger").AddComponent<Logging.JSONLLogger>();
            }
            if (PassthroughController.Instance == null)
            {
                new GameObject("PassthroughController").AddComponent<PassthroughController>();
            }
        }

        private void StartFirstAct()
        {
            if (started) return;
            started = true;

            if (bootstrapUI != null) bootstrapUI.SetActive(false);

            GameStateManager.Instance.SetCurrentAct(ActId.None);
            // WYŁADUJ scenę Bootstrap po wczytaniu menu — inaczej zostaje jej rig (duplikat dłoni „w oddali")
            // + splash „MR..." wiszący w tle. Managery są DontDestroyOnLoad → przetrwają.
            SceneLoader.Instance.LoadActAdditive(firstActSceneName, gameObject.scene.name);
        }
    }
}

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using MRCrisisTrainer.Config;
using MRCrisisTrainer.Core;
using MRCrisisTrainer.Logging;

namespace MRCrisisTrainer.Acts.Act2
{
    public enum Act2State
    {
        Idle,
        Intro,
        AwaitGrip,
        Driving,
        Skid,
        Recovered,
        Failed,
        Complete
    }

    public class Act2Controller : MonoBehaviour
    {
        [Header("Config")]
        [SerializeField] private VehicleConfig config;

        [Header("Refs")]
        [SerializeField] private SteeringWheelController steeringWheel;
        [SerializeField] private CockpitVisualizer cockpit;
        [SerializeField] private WindshieldView windshield;
        [SerializeField] private Act2FeedbackUI ui;

        [Header("Flow")]
        [SerializeField] private string nextSceneName = "Act3_Hide";

        public Act2State CurrentState { get; private set; } = Act2State.Idle;
        private SkidPhysicsSimulator skid;
        private float drivingStartTime;
        private bool skidTriggered;

#if UNITY_EDITOR
        void OnValidate()
        {
            if (config == null)
            {
                config = UnityEditor.AssetDatabase.LoadAssetAtPath<VehicleConfig>("Assets/_Project/ScriptableObjects/VehicleConfig.asset");
            }
        }
#endif

        void Start()
        {
            if (config == null)
            {
                Debug.LogError("[Act2Controller] VehicleConfig not assigned. Aborting.");
                return;
            }

            if (steeringWheel != null) steeringWheel.OnDriveStarted += OnGripConfirmed;

            JSONLLogger.Instance?.LogEvent("act2_start", new Dictionary<string, object>
            {
                { "skid_after_s", config.secondsBeforeSkid },
                { "initial_skid_deg", config.initialSkidAngleDeg }
            });

            StartCoroutine(IntroSequence());
        }

        void OnDestroy()
        {
            if (steeringWheel != null) steeringWheel.OnDriveStarted -= OnGripConfirmed;
        }

        private IEnumerator IntroSequence()
        {
            TransitionTo(Act2State.Intro);
            // MR: realny pokój (passthrough), gracz PODCHODZI do fotela i SIADA — auto pojawia się dopiero potem
            MRCrisisTrainer.XR.PassthroughController.Instance?.EnablePassthrough();
            var marker = SpawnSeatMarker();
            ui?.SetPrompt("Podejdź do swojego fotela i usiądź — to fotel kierowcy");
            yield return WaitForSit();
            if (marker != null) Destroy(marker);

            cockpit?.ShowCockpit();
            ui?.SetPrompt("Chwyć kierownicę obiema rękoma");
            TransitionTo(Act2State.AwaitGrip);
        }

        /// <summary>Zielony pierścień „usiądź tutaj" na podłodze przed graczem.</summary>
        private GameObject SpawnSeatMarker()
        {
            var cam = Camera.main; if (cam == null) cam = FindFirstObjectByType<Camera>();
            if (cam == null) return null;
            Vector3 fwd = cam.transform.forward; fwd.y = 0f;
            fwd = fwd.sqrMagnitude > 0.01f ? fwd.normalized : Vector3.forward;
            Vector3 pos = cam.transform.position + fwd * 0.1f; pos.y = 0.02f;
            var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ring.name = "SeatMarker";
            var col = ring.GetComponent<Collider>(); if (col != null) Destroy(col);
            ring.transform.position = pos;
            ring.transform.localScale = new Vector3(0.55f, 0.015f, 0.55f);
            var sh = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            var m = new Material(sh);
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", new Color(0.25f, 0.95f, 0.45f)); else m.color = new Color(0.25f, 0.95f, 0.45f);
            ring.GetComponent<Renderer>().sharedMaterial = m;
            return ring;
        }

        /// <summary>Czeka aż gracz usiądzie (HMD opada o ≥22 cm względem pozycji stojącej). Bezpieczny timeout 25 s.</summary>
        private IEnumerator WaitForSit()
        {
            var cam = Camera.main; if (cam == null) cam = FindFirstObjectByType<Camera>();
            if (cam == null) { yield return new WaitForSeconds(2f); yield break; }
            float baseline = cam.transform.position.y, ts = 0f;
            while (ts < 0.8f) { baseline = Mathf.Max(baseline, cam.transform.position.y); ts += Time.deltaTime; yield return null; }
            float sitY = baseline - 0.22f;
            float stable = 0f, timeout = 0f;
            while (stable < 0.6f)
            {
                timeout += Time.deltaTime;
                if (timeout > 25f) { JSONLLogger.Instance?.LogEvent("act2_sit_timeout", null); yield break; }
                if (cam.transform.position.y <= sitY) stable += Time.deltaTime; else stable = 0f;
                yield return null;
            }
            JSONLLogger.Instance?.LogEvent("act2_seated", null);
        }

        private void OnGripConfirmed()
        {
            if (CurrentState != Act2State.AwaitGrip) return;
            TransitionTo(Act2State.Driving);
            ui?.SetPrompt("Jedziesz... uważaj na drogę");
            drivingStartTime = Time.time;
            windshield?.SetActive(true);
        }

        void Update()
        {
            switch (CurrentState)
            {
                case Act2State.Driving:
                    DrivingTick();
                    break;
                case Act2State.Skid:
                    SkidTick();
                    break;
            }
        }

        private void DrivingTick()
        {
            windshield?.Step(Time.deltaTime, 0f);
            if (!skidTriggered && Time.time - drivingStartTime >= config.secondsBeforeSkid)
            {
                TriggerSkid();
            }
        }

        private void TriggerSkid()
        {
            skidTriggered = true;
            float dir = Random.value < 0.5f ? -1f : 1f;
            skid = new SkidPhysicsSimulator(config, config.initialSkidAngleDeg * dir);
            TransitionTo(Act2State.Skid);
            ui?.SetPrompt(dir > 0 ? "POŚLIZG W PRAWO! Skręć w prawo!" : "POŚLIZG W LEWO! Skręć w lewo!");
            JSONLLogger.Instance?.LogEvent("skid_triggered", new Dictionary<string, object>
            {
                { "initial_angle_deg", skid.CurrentAngleDeg }
            });
        }

        private void SkidTick()
        {
            float steering = steeringWheel != null ? steeringWheel.NormalizedSteering : 0f;
            skid.Step(steering, Time.deltaTime);

            var feedback = CounterSteerEvaluator.Evaluate(skid.CurrentAngleDeg, steering);
            ui?.SetSteeringFeedback(feedback);
            ui?.SetSkidAngle(skid.CurrentAngleDeg);
            windshield?.Step(Time.deltaTime, skid.CurrentAngleDeg);

            if (skid.IsRecovered)
            {
                OnSkidRecovered();
            }
            else if (skid.IsCatastrophe)
            {
                OnSkidFailed();
            }
        }

        private void OnSkidRecovered()
        {
            TransitionTo(Act2State.Recovered);
            ui?.SetPrompt("Wyszedłeś z poślizgu - droga prosta");
            JSONLLogger.Instance?.LogEvent("act2_recovered", new Dictionary<string, object>
            {
                { "time_to_recovery_s", skid.TotalElapsed }
            });
            StartCoroutine(CompleteSequence());
        }

        private void OnSkidFailed()
        {
            TransitionTo(Act2State.Failed);
            ui?.SetPrompt("Wypadek - spróbuj jeszcze raz");
            JSONLLogger.Instance?.LogEvent("act2_failed", new Dictionary<string, object>
            {
                { "final_angle_deg", skid.CurrentAngleDeg },
                { "elapsed_s", skid.TotalElapsed }
            });
            StartCoroutine(RetrySequence());
        }

        private IEnumerator RetrySequence()
        {
            yield return new WaitForSeconds(3f);
            skidTriggered = false;
            drivingStartTime = Time.time;
            windshield?.ResetWorld();
            steeringWheel?.ApplySteeringByValue(0f);
            TransitionTo(Act2State.Driving);
            ui?.SetPrompt("Próbuj jeszcze raz - jedziesz...");
        }

        private IEnumerator CompleteSequence()
        {
            yield return new WaitForSeconds(3f);
            cockpit?.HideCockpit();
            windshield?.SetActive(false);
            TransitionTo(Act2State.Complete);

            if (SceneLoader.Instance != null && !string.IsNullOrEmpty(nextSceneName))
            {
                SceneLoader.Instance.LoadActAdditive(nextSceneName, "Act2_Car");
                GameStateManager.Instance?.SetCurrentAct(ActId.Act3_Hide);
            }
        }

        private void TransitionTo(Act2State next)
        {
            Debug.Log($"[Act2] {CurrentState} -> {next}");
            CurrentState = next;
        }
    }
}

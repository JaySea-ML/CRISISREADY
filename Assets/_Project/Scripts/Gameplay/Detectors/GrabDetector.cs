using UnityEngine;
using MRCrisisTrainer.XR;

namespace MRCrisisTrainer.Gameplay.Detectors
{
    /// <summary>Ukończenie gdy dłoń gracza chwyci obiekt (zbliży się do niego w promieniu).</summary>
    public class GrabDetector : MicrostepDetector
    {
        [SerializeField] private MonoBehaviour handProviderBehaviour;
        [SerializeField] private Transform grabbable;
        [SerializeField] private float radius = 0.15f;
        [SerializeField] private Transform attachToOnGrab; // np. podnosi słuchawkę do ucha
        [SerializeField] private bool moveGrabbableToHand;
        [Tooltip("Awaryjny limit (s) — po nim krok zalicza się sam, by przepływ nie utknął. 0 = wyłączony.")]
        [SerializeField] private float maxSeconds = 0f;

        private IHandPoseProvider hands;
        private float elapsed;

        void Awake() => hands = handProviderBehaviour as IHandPoseProvider;

        protected override void OnBegin() => elapsed = 0f;

        void Update()
        {
            if (!IsActive || grabbable == null) return;
            elapsed += Time.deltaTime;
            if (hands != null && (TryGrip(HandSide.Left) || TryGrip(HandSide.Right)))
            {
                if (attachToOnGrab != null) grabbable.position = attachToOnGrab.position;
                Complete();
                return;
            }
            // Awaryjnie: po maxSeconds zalicz krok (np. „grip_wheel"), żeby recovery poślizgu ZAWSZE ruszył.
            if (maxSeconds > 0f && elapsed >= maxSeconds) Complete();
        }

        private bool TryGrip(HandSide s)
        {
            if (!hands.TryGetHandPosition(s, out var p)) return false;
            if ((p - grabbable.position).sqrMagnitude > radius * radius) return false;
            if (moveGrabbableToHand && attachToOnGrab == null) grabbable.position = p;
            return true;
        }
    }
}

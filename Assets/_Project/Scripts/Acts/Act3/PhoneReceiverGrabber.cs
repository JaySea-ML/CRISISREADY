using System;
using UnityEngine;
using MRCrisisTrainer.Config;
using MRCrisisTrainer.XR;

namespace MRCrisisTrainer.Acts.Act3
{
    /// <summary>
    /// Wykrywa kiedy dłoń gracza chwyta słuchawkę telefonu.
    /// Po grabbie podnosi słuchawkę do ucha (visual) i emituje event.
    /// </summary>
    public class PhoneReceiverGrabber : MonoBehaviour
    {
        [SerializeField] private Act3Config config;
        [SerializeField] private MonoBehaviour handProviderBehaviour;
        [SerializeField] private Transform receiverTransform;
        [SerializeField] private Transform raiseToPoint;

        private IHandPoseProvider handProvider;
        private bool grabbed;

        public bool IsGrabbed => grabbed;
        public event Action OnReceiverGrabbed;

        void Awake()
        {
            handProvider = handProviderBehaviour as IHandPoseProvider;
        }

        void Update()
        {
            if (grabbed || handProvider == null || receiverTransform == null) return;

            bool grabFound = TryHandGrip(HandSide.Left) || TryHandGrip(HandSide.Right);
            if (grabFound)
            {
                grabbed = true;
                if (raiseToPoint != null) receiverTransform.position = raiseToPoint.position;
                OnReceiverGrabbed?.Invoke();
            }
        }

        private bool TryHandGrip(HandSide side)
        {
            if (!handProvider.TryGetHandPosition(side, out var pos)) return false;
            return (pos - receiverTransform.position).sqrMagnitude <= config.receiverGrabRadius * config.receiverGrabRadius;
        }
    }
}

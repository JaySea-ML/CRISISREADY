using UnityEngine;

namespace MRCrisisTrainer.Acts.Act1
{
    public class VictimAnimator : MonoBehaviour
    {
        private static readonly int FallTrigger = Animator.StringToHash("Fall");
        private static readonly int StabilizedTrigger = Animator.StringToHash("Stabilized");
        private static readonly int OnGroundBool = Animator.StringToHash("OnGround");

        [SerializeField] private Animator animator;
        [SerializeField] private float fallToGroundDelay = 1.5f;
        [SerializeField] private Renderer victimRenderer;
        [SerializeField] private Color stabilizedTint = new Color(0.85f, 1f, 0.85f);

        void Awake()
        {
            if (animator == null) animator = GetComponent<Animator>();
        }

        public void PlayFall()
        {
            if (animator != null) animator.SetTrigger(FallTrigger);
            CancelInvoke(nameof(MarkOnGround));
            Invoke(nameof(MarkOnGround), fallToGroundDelay);
        }

        private void MarkOnGround()
        {
            if (animator != null) animator.SetBool(OnGroundBool, true);
        }

        public void PlayStabilized()
        {
            if (animator != null) animator.SetTrigger(StabilizedTrigger);
            if (victimRenderer != null)
            {
                foreach (var mat in victimRenderer.materials)
                {
                    if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", stabilizedTint);
                    else if (mat.HasProperty("_Color")) mat.SetColor("_Color", stabilizedTint);
                }
            }
        }
    }
}

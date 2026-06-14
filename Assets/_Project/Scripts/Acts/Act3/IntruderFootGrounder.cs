using UnityEngine;

namespace MRCrisisTrainer.Acts.Act3
{
    /// <summary>
    /// Utrzymuje STOPY intruza na podłodze. Retarget chodu (Mixamo → CC zombie) zatapiał model
    /// („chodzi w ziemi"), bo animowana poza opuszcza najniższy punkt poniżej pozy bazowej.
    /// Zamiast kruchego jednorazowego podniesienia mierzymy NAJNIŻSZY punkt modelu KAŻDĄ klatkę
    /// w LateUpdate (po animacji) i przesuwamy roota tak, by stopy spoczęły dokładnie na podłodze.
    /// Tanie: jeden przebieg po rendererach + opcjonalnie jeden raycast w dół.
    /// Grounding jest pomijany w trakcie jumpscare'a (ThiefWanderAI ustawia pozycję ręcznie).
    /// </summary>
    public class IntruderFootGrounder : MonoBehaviour
    {
        [Tooltip("Warstwy traktowane jako podłoga przy raycaście w dół. Jeśli puste/None, używamy poziomu rodzica.")]
        [SerializeField] private LayerMask floorMask = ~0;
        [Tooltip("Stała korekta — wysokość, na jakiej stopy mają być nad podłogą (np. by uniknąć z-fightu).")]
        [SerializeField] private float footOffset = 0f;
        [Tooltip("Maksymalna korekta na klatkę (m) — wygładza skoki przy gwałtownej zmianie pozy.")]
        [SerializeField] private float maxStepPerFrame = 0.5f;

        private Renderer[] rends;
        private bool suspended;

        /// <summary>Wyłącza/włącza grounding (np. na czas jumpscare'a, który ustawia pozycję ręcznie).</summary>
        public void SetGroundingSuspended(bool value) => suspended = value;

        void Start() => rends = GetComponentsInChildren<Renderer>(true);

        void LateUpdate()
        {
            if (suspended) return;
            if (rends == null || rends.Length == 0) { rends = GetComponentsInChildren<Renderer>(true); if (rends.Length == 0) return; }

            // Najniższy punkt WIDOCZNEGO modelu (po zastosowaniu animacji w tej klatce).
            float lowest = float.MaxValue;
            for (int i = 0; i < rends.Length; i++)
            {
                var r = rends[i];
                if (r != null && r.enabled) lowest = Mathf.Min(lowest, r.bounds.min.y);
            }
            if (lowest >= float.MaxValue * 0.5f) return;   // niewidoczny — czekaj na Appear()

            // Poziom podłogi: preferuj raycast w dół spod modelu; w razie braku trafienia użyj poziomu rodzica.
            float floorY;
            Vector3 origin = new Vector3(transform.position.x, lowest + 0.5f, transform.position.z);
            if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 5f, floorMask, QueryTriggerInteraction.Ignore))
                floorY = hit.point.y;
            else
                floorY = transform.parent != null ? transform.parent.position.y : 0f;

            // Przesuń roota tak, by najniższy punkt = floorY + footOffset. Działa w obie strony (sink i unoszenie).
            float delta = (floorY + footOffset) - lowest;
            if (Mathf.Abs(delta) > 0.0005f)
            {
                delta = Mathf.Clamp(delta, -maxStepPerFrame, maxStepPerFrame);
                transform.position += new Vector3(0f, delta, 0f);
            }
        }
    }
}

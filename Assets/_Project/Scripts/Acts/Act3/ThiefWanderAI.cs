using System.Collections;
using UnityEngine;
using MRCrisisTrainer.Config;

namespace MRCrisisTrainer.Acts.Act3
{
    /// <summary>
    /// Intruz widoczny przez szparę szafy. Startuje ukryty; po Appear() chodzi po pokoju
    /// „przeszukując" go (latarka omiata otoczenie) — losuje świeże, osiągalne cele wokół pokoju,
    /// żeby nie zacinał się i nie „dreptał w miejscu". Po hałasie (OnNoiseDetected) odwraca się i
    /// rusza w stronę szafy. Flee() = ucieczka po przyjeździe policji.
    /// RootMotion OFF — transform przesuwamy ręcznie; chód (zapętlony klip) gra przez Animator.speed.
    /// </summary>
    public class ThiefWanderAI : MonoBehaviour
    {
        [SerializeField] private Act3Config config;
        [SerializeField] private Transform[] waypoints;
        [SerializeField] private Transform closetTarget;
        [SerializeField] private GameObject visualRoot;
        [SerializeField] private Light flashlight;
        [SerializeField] private float arriveThreshold = 0.25f;
        [SerializeField] private float pauseDuration = 1.0f;
        [SerializeField] private float speedFallback = 0.6f;
        [Tooltip("Maksymalny czas (s) marszu do jednego celu zanim uznamy go za nieosiągalny i wybierzemy nowy.")]
        [SerializeField] private float moveTimeout = 6f;
        [Tooltip("Losowe rozproszenie (m) wokół waypointów przy wyborze świeżego celu — żeby chodził po całym pokoju.")]
        [SerializeField] private float wanderJitter = 0.6f;

        private int currentIndex;
        private bool alerted;
        private bool appeared;
        private Animator animator;
        private IntruderFootGrounder grounder;

        private float Speed => config != null ? config.thiefSpeed : speedFallback;

        void Awake()
        {
            animator = GetComponentInChildren<Animator>();
            grounder = GetComponentInChildren<IntruderFootGrounder>();
            HideImmediate();
        }

        public void HideImmediate()
        {
            appeared = false;
            alerted = false;
            StopAllCoroutines();
            SetGrounding(true);
            SetWalking(false);
            if (visualRoot != null) visualRoot.SetActive(false);
            else SetRenderers(false);
            if (flashlight != null) flashlight.enabled = false;
        }

        public void Appear()
        {
            if (appeared) return;
            appeared = true;
            SetGrounding(true);
            if (visualRoot != null) visualRoot.SetActive(true);
            else SetRenderers(true);
            if (flashlight != null) flashlight.enabled = true;
            StartCoroutine(WanderLoop());
        }

        public void OnNoiseDetected()
        {
            if (!appeared || alerted) return;
            alerted = true;
            StopAllCoroutines();
            StartCoroutine(LookTowardCloset());
        }

        public void Flee(Transform fleeTarget)
        {
            StopAllCoroutines();
            SetGrounding(true);
            StartCoroutine(FleeRoutine(fleeTarget));
        }

        /// <summary>HORROR JUMPSCARE: gdy gracz za długo hałasuje, intruz ZNAJDUJE go i rzuca się
        /// tuż przed twarz z ostrym dźwiękiem (jak w grach grozy).</summary>
        public void Jumpscare(Transform cam)
        {
            if (cam == null) return;
            StopAllCoroutines();
            alerted = true; appeared = true;
            SetGrounding(false);                 // grounding NIE może walczyć z ręcznym ustawianiem pozycji w jumpscarze
            if (visualRoot != null) visualRoot.SetActive(true); else SetRenderers(true);
            if (flashlight != null) flashlight.enabled = true;
            if (animator != null) animator.speed = 0f;   // ZATRZYMAJ chód — nie dreptać w miejscu w trakcie jumpscare'a
            StartCoroutine(JumpscareRoutine(cam));
        }

        private IEnumerator JumpscareRoutine(Transform cam)
        {
            Vector3 baseScale = transform.localScale;

            // 0) Stań tuż przed graczem, ale jeszcze trochę dalej — za chwilę RZUT
            PlaceLunge(cam, baseScale, 0.95f, 1.0f);

            // 1) HUK + czerwony błysk + dźwięk dokładnie w chwili rzutu
            Core.AudioManager.Instance?.PlaySfx("jumpscare_hit", 1f);    // „Fear Hit" — prawdziwy horror-stinger (Spicy Overlord)
            Core.AudioManager.Instance?.PlaySfx("jumpscare_boom", 0.85f);// „Big Bang" — niskie uderzenie pod spodem
            var flash = MakeFlash(cam);

            // 2) RZUT (lunge): błyskawicznie z ~0.95 m na ~0.28 m + powiększenie — wygląda jak rzucenie się
            float t = 0f;
            while (t < 0.16f)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / 0.16f);
                PlaceLunge(cam, baseScale, Mathf.Lerp(0.95f, 0.28f, k), Mathf.Lerp(1.0f, 1.4f, k));
                yield return null;
            }
            if (flash != null) Destroy(flash);

            // 3) LOOM + drżenie tuż przy twarzy (groza), powoli jeszcze bliżej i większy
            float hold = 0f;
            while (hold < 1.9f)
            {
                hold += Time.deltaTime;
                float k = Mathf.Clamp01(hold / 1.9f);
                float jitter = Mathf.Sin(hold * 42f) * 0.013f;   // drganie/trzęsienie
                PlaceLunge(cam, baseScale, Mathf.Lerp(0.28f, 0.22f, k) + jitter, Mathf.Lerp(1.4f, 1.55f, k));
                yield return null;
            }
            transform.localScale = baseScale;
        }

        /// <summary>Ustawia intruza w danej odległości przed twarzą gracza tak, by jego GŁOWA
        /// (oczy modelu) wylądowała na wysokości oczu gracza, twarzą do niego, z lekkim pochyleniem
        /// do przodu (groźna poza) i skalą (loom/lunge).</summary>
        private void PlaceLunge(Transform cam, Vector3 baseScale, float dist, float scaleMul)
        {
            // Cel: punkt na wysokości oczu gracza, w odległości `dist` przed kamerą (tylko yaw — bez patrzenia w górę/dół).
            Vector3 flatFwd = cam.forward; flatFwd.y = 0f;
            if (flatFwd.sqrMagnitude < 0.0001f) flatFwd = transform.forward; // kamera patrzy pionowo — awaryjnie
            flatFwd.Normalize();
            Vector3 target = cam.position + flatFwd * dist; target.y = cam.position.y;

            // Najpierw orientacja i skala (offset głowy zależy od obu).
            Vector3 look = cam.position - target; look.y = 0f;
            Quaternion rot = (look.sqrMagnitude > 0.0001f)
                ? Quaternion.LookRotation(look) * Quaternion.Euler(12f, 0f, 0f)   // twarzą do gracza + nachył do przodu
                : transform.rotation;
            transform.rotation = rot;
            transform.localScale = baseScale * scaleMul;

            // Offset root->głowa w lokalnym układzie (niezależny od pozycji), przeskalowany i obrócony do świata.
            Vector3 headLocalOffset = GetHeadLocalOffset();
            Vector3 worldHeadOffset = rot * Vector3.Scale(headLocalOffset, transform.localScale);

            // Ustaw root tak, by GŁOWA trafiła w `target` (na wysokości oczu).
            transform.position = target - worldHeadOffset;
        }

        /// <summary>Pozycja głowy modelu względem roota, wyrażona w LOKALNYM (nieskalowanym, nieobróconym)
        /// układzie roota. Mnożąc przez skalę i obracając rotacją roota otrzymujemy offset w świecie.</summary>
        private Vector3 GetHeadLocalOffset()
        {
            var anim = animator != null ? animator : GetComponentInChildren<Animator>();
            Transform head = (anim != null && anim.isHuman) ? anim.GetBoneTransform(HumanBodyBones.Head) : null;
            if (head != null)
            {
                // world->local względem roota, ale bez wpływu skali/rotacji roota (chcemy „surowy" offset modelu).
                Vector3 worldDelta = head.position - transform.position;
                Quaternion invRot = Quaternion.Inverse(transform.rotation);
                Vector3 local = invRot * worldDelta;
                Vector3 s = transform.localScale;
                local.x = Mathf.Approximately(s.x, 0f) ? local.x : local.x / s.x;
                local.y = Mathf.Approximately(s.y, 0f) ? local.y : local.y / s.y;
                local.z = Mathf.Approximately(s.z, 0f) ? local.z : local.z / s.z;
                return local;
            }

            // Brak kości głowy — oszacuj głowę przy szczycie połączonych boundsów rendererów.
            Bounds b = default; bool has = false;
            foreach (var r in GetComponentsInChildren<Renderer>(true))
            {
                if (has) b.Encapsulate(r.bounds); else { b = r.bounds; has = true; }
            }
            if (!has) return new Vector3(0f, 1.6f, 0f); // ostateczny fallback: typowa wysokość oczu
            Vector3 headWorld = new Vector3(b.center.x, b.max.y - b.size.y * 0.08f, b.center.z); // ~8% poniżej czubka = okolice oczu
            Vector3 d = headWorld - transform.position;
            Vector3 sc = transform.lossyScale;
            return new Vector3(
                Mathf.Approximately(sc.x, 0f) ? d.x : d.x / sc.x,
                Mathf.Approximately(sc.y, 0f) ? d.y : d.y / sc.y,
                Mathf.Approximately(sc.z, 0f) ? d.z : d.z / sc.z);
        }

        private GameObject MakeFlash(Transform cam)
        {
            var flash = GameObject.CreatePrimitive(PrimitiveType.Quad); flash.name = "ScareFlash";
            var fcol = flash.GetComponent<Collider>(); if (fcol != null) Destroy(fcol);
            flash.transform.SetParent(cam, false);
            flash.transform.localPosition = new Vector3(0f, 0f, 0.25f);
            flash.transform.localRotation = Quaternion.identity;
            flash.transform.localScale = new Vector3(3f, 3f, 1f);
            var sh = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            var fm = new Material(sh); var rc = new Color(0.55f, 0f, 0f);
            if (fm.HasProperty("_BaseColor")) fm.SetColor("_BaseColor", rc); else fm.color = rc;
            flash.GetComponent<Renderer>().sharedMaterial = fm;
            return flash;
        }

        private IEnumerator WanderLoop()
        {
            // Brak waypointów: krąż wokół pozycji startowej, żeby i tak chodził (a nie zamarzał).
            Vector3 home = transform.position;
            while (appeared && !alerted)
            {
                Vector3 target = NextWanderTarget(home);

                // Idź dopóki nie dojdziemy (płasko, w XZ — bo MoveToward też rusza tylko w poziomie),
                // ale nie dłużej niż moveTimeout (cel nieosiągalny → wybierz nowy zamiast zamarzać).
                SetWalking(true);
                float started = Time.time;
                while (appeared && !alerted && HorizontalDistance(target) > arriveThreshold)
                {
                    MoveToward(target, 1f);
                    if (Time.time - started > moveTimeout) break;   // utknął/nieosiągalny → przerwij, weź świeży cel
                    yield return null;
                }
                if (!appeared || alerted) yield break;

                // Pauza „rozglądania się" — zatrzymaj chód, żeby nie dreptał w miejscu.
                SetWalking(false);
                float pause = 0f;
                while (appeared && !alerted && pause < pauseDuration) { pause += Time.deltaTime; yield return null; }
            }
        }

        /// <summary>Wybiera świeży, osiągalny cel: kolejny waypoint z losowym rozproszeniem (wanderJitter),
        /// tak by intruz przemierzał cały pokój, a nie zacinał się w jednym punkcie. Gdy nie ma waypointów,
        /// losuje punkt w pobliżu pozycji startowej.</summary>
        private Vector3 NextWanderTarget(Vector3 home)
        {
            Vector3 basePos;
            if (waypoints != null && waypoints.Length > 0)
            {
                // Przesuń wskaźnik i pomiń ewentualne puste sloty.
                for (int i = 0; i < waypoints.Length; i++)
                {
                    currentIndex = (currentIndex + 1) % waypoints.Length;
                    if (waypoints[currentIndex] != null) break;
                }
                Transform wp = waypoints[currentIndex];
                basePos = wp != null ? wp.position : home;
            }
            else
            {
                basePos = home;
            }

            Vector2 j = Random.insideUnitCircle * wanderJitter;
            return new Vector3(basePos.x + j.x, transform.position.y, basePos.z + j.y);
        }

        private IEnumerator LookTowardCloset()
        {
            Transform t = closetTarget;
            SetWalking(false);
            if (t == null) { alerted = false; if (appeared) StartCoroutine(WanderLoop()); yield break; }
            Vector3 dir = t.position - transform.position; dir.y = 0;
            if (dir.sqrMagnitude > 0.0001f)
            {
                Quaternion targetRot = Quaternion.LookRotation(dir.normalized);
                float a = 0;
                while (a < 1f) { a += Time.deltaTime * 2f; transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, a); yield return null; }
            }
            // Podejdź groźnie w stronę szafy
            SetWalking(true);
            float startTime = Time.time;
            while (Time.time - startTime < 3.5f)
            {
                MoveToward(t.position, 0.5f);
                yield return null;
            }
            // Po chwili wraca do przeszukiwania (chyba że to był decydujący strike → Fail obsłuży detektor)
            alerted = false;
            if (appeared) StartCoroutine(WanderLoop());
        }

        private IEnumerator FleeRoutine(Transform fleeTarget)
        {
            Vector3 dest = fleeTarget != null ? fleeTarget.position : transform.position + transform.forward * -5f;
            SetWalking(true);
            float startTime = Time.time;
            while (Time.time - startTime < 4f && HorizontalDistance(dest) > 0.5f)
            {
                MoveToward(dest, 2.2f);
                yield return null;
            }
            HideImmediate();
        }

        private void MoveToward(Vector3 worldPos, float speedMul)
        {
            Vector3 dir = (worldPos - transform.position); dir.y = 0;
            if (dir.sqrMagnitude < 0.0001f) return;
            dir.Normalize();
            transform.position += dir * Speed * speedMul * Time.deltaTime;
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), 0.12f);
        }

        /// <summary>Odległość pozioma (XZ) do celu — MoveToward porusza się tylko w poziomie, więc próg
        /// dojścia też musi ignorować Y. Inaczej różnica wysokości (np. po podniesieniu modelu przez
        /// foot-grounder) sprawia, że pętla „dochodzenia" nigdy się nie kończy = chód w miejscu.</summary>
        private float HorizontalDistance(Vector3 worldPos)
        {
            float dx = worldPos.x - transform.position.x;
            float dz = worldPos.z - transform.position.z;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        /// <summary>Włącza/zatrzymuje chód: przy RootMotion OFF jedyny stan kontrolera to zapętlony „Walk",
        /// więc sterujemy Animator.speed (1 = idzie i nogi się ruszają, 0 = stoi i nie drepcze w miejscu).</summary>
        private void SetWalking(bool walking)
        {
            if (animator != null) animator.speed = walking ? 1f : 0f;
        }

        private void SetGrounding(bool enabledGrounding)
        {
            if (grounder != null) grounder.SetGroundingSuspended(!enabledGrounding);
        }

        private void SetRenderers(bool on)
        {
            foreach (var r in GetComponentsInChildren<Renderer>(true)) r.enabled = on;
        }
    }
}

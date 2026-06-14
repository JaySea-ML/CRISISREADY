using UnityEngine;

namespace MRCrisisTrainer.XR
{
    /// <summary>
    /// Pierwszoosobowe ciało gracza: model humanoidalny (ReadyPlayerMe/Mixamo) podąża za głową (kamerą),
    /// kość głowy ukryta (nie zasłania widoku), a RAMIONA są naginane IK do śledzonych dłoni. Tułów i nogi
    /// są widoczne, gdy patrzysz w dół. Działa tylko na rękach (bez kontrolerów).
    /// </summary>
    public class FirstPersonAvatar : MonoBehaviour
    {
        [SerializeField] private Transform head;            // kamera (oczy)
        [SerializeField] private Transform trackingOrigin;
        [SerializeField] private MonoBehaviour handProviderBehaviour;
        [SerializeField] private Transform avatarRoot;      // korzeń instancji modelu
        [SerializeField] private float seatedThighDeg = 78f; // zgięcie biodra (uda do przodu) — poza siedząca
        [SerializeField] private float seatedShinDeg = -82f; // zgięcie kolana (łydka w dół)
        [SerializeField] private float headDrop = 0.10f;     // kość głowy nieco poniżej oczu (oczy = czubek głowy)

        private IHandPoseProvider hands;
        private Transform bHead, bLArm, bLFore, bLHand, bRArm, bRFore, bRHand;
        private Transform bLUpLeg, bLLeg, bRUpLeg, bRLeg;

        void Start()
        {
            hands = handProviderBehaviour as IHandPoseProvider;
            if (head == null && Camera.main != null) head = Camera.main.transform;
            if (avatarRoot == null) avatarRoot = transform;
            BindBones(avatarRoot);
            // naturalna poza zamiast T-pozy (idle z modelu)
            var anim = avatarRoot.GetComponentInChildren<Animation>();
            if (anim != null) { anim.playAutomatically = true; if (anim.clip != null) anim.Play(); }
            if (bHead != null) bHead.localScale = Vector3.one * 0.01f;   // ukryj głowę (nie patrzymy od środka czaszki)
            if (bLHand != null) bLHand.localScale = Vector3.one * 0.02f; // ukryj dłonie avatara — używamy śledzonych dłoni
            if (bRHand != null) bRHand.localScale = Vector3.one * 0.02f;
        }

        void LateUpdate()
        {
            if (avatarRoot == null || head == null) return;

            // 1) obrót ciała za głową (tylko yaw — ciało nie pochyla się z głową)
            Vector3 fwd = head.forward; fwd.y = 0f;
            if (fwd.sqrMagnitude < 0.0001f) fwd = Vector3.forward;
            avatarRoot.rotation = Quaternion.LookRotation(fwd.normalized, Vector3.up);

            // 2) POZA SIEDZĄCA — uda do przodu, łydki w dół (zamiast stojącego ciała gdy gracz siedzi)
            SeatLegs();

            // 3) ustaw ciało tak, by kość głowy znalazła się przy oczach (lekko niżej = naturalnie)
            if (bHead != null) avatarRoot.position += (head.position - head.up * headDrop) - bHead.position;
            else avatarRoot.position = head.position - Vector3.up * 1.6f;

            // 4) IK ramion do realnych (śledzonych) dłoni
            if (hands != null)
            {
                if (hands.TryGetHandPosition(HandSide.Left, out var lp)) SolveArm(bLArm, bLFore, bLHand, lp);
                if (hands.TryGetHandPosition(HandSide.Right, out var rp)) SolveArm(bRArm, bRFore, bRHand, rp);
            }
        }

        private void SeatLegs()
        {
            if (bLUpLeg != null) bLUpLeg.localRotation = Quaternion.Euler(seatedThighDeg, 0f, 0f) * bLUpLeg.localRotation;
            if (bRUpLeg != null) bRUpLeg.localRotation = Quaternion.Euler(seatedThighDeg, 0f, 0f) * bRUpLeg.localRotation;
            if (bLLeg != null) bLLeg.localRotation = Quaternion.Euler(seatedShinDeg, 0f, 0f) * bLLeg.localRotation;
            if (bRLeg != null) bRLeg.localRotation = Quaternion.Euler(seatedShinDeg, 0f, 0f) * bRLeg.localRotation;
        }

        /// <summary>Przybliżone dwukostne IK: nagina ramię (upper) + przedramię (fore) tak, by dłoń (hand) sięgnęła celu.</summary>
        private void SolveArm(Transform upper, Transform fore, Transform hand, Vector3 target)
        {
            if (upper == null || fore == null || hand == null) return;
            Vector3 a = upper.position;
            float lu = Vector3.Distance(a, fore.position);
            float lf = Vector3.Distance(fore.position, hand.position);
            float reach = (lu + lf) * 0.999f;
            Vector3 toT = target - a;
            float dist = Mathf.Clamp(toT.magnitude, 0.02f, reach);
            Vector3 tgt = a + toT.normalized * dist;
            Vector3 pole = -head.up;   // łokieć skierowany w dół

            // skieruj cały łańcuch tak, by koniec (dłoń) wskazywał cel
            upper.rotation = Quaternion.FromToRotation(hand.position - a, tgt - a) * upper.rotation;

            // zegnij łokieć (prawo cosinusów)
            a = upper.position;
            float cosU = Mathf.Clamp((lu * lu + dist * dist - lf * lf) / (2f * lu * dist), -1f, 1f);
            float upAng = Mathf.Acos(cosU) * Mathf.Rad2Deg;
            Vector3 tgtDir = (tgt - a).normalized;
            Vector3 boneDir = (fore.position - a).normalized;
            float curAng = Vector3.Angle(boneDir, tgtDir);
            Vector3 axis = Vector3.Cross(tgtDir, pole).normalized;
            if (axis.sqrMagnitude < 0.001f) axis = Vector3.Cross(tgtDir, Vector3.forward).normalized;
            upper.rotation = Quaternion.AngleAxis(upAng - curAng, axis) * upper.rotation;

            // dociągnij przedramię do celu
            fore.rotation = Quaternion.FromToRotation(hand.position - fore.position, tgt - fore.position) * fore.rotation;
        }

        private void BindBones(Transform r)
        {
            foreach (var t in r.GetComponentsInChildren<Transform>(true))
            {
                string n = t.name;
                if (bHead == null && n.StartsWith("Head") && !n.StartsWith("HeadTop")) bHead = t;
                else if (bLFore == null && n.StartsWith("LeftForeArm")) bLFore = t;
                else if (bLArm == null && n.StartsWith("LeftArm")) bLArm = t;
                else if (bLHand == null && n.StartsWith("LeftHand_")) bLHand = t;
                else if (bRFore == null && n.StartsWith("RightForeArm")) bRFore = t;
                else if (bRArm == null && n.StartsWith("RightArm")) bRArm = t;
                else if (bRHand == null && n.StartsWith("RightHand_")) bRHand = t;
                else if (bLUpLeg == null && n.StartsWith("LeftUpLeg")) bLUpLeg = t;
                else if (bLLeg == null && n.StartsWith("LeftLeg")) bLLeg = t;
                else if (bRUpLeg == null && n.StartsWith("RightUpLeg")) bRUpLeg = t;
                else if (bRLeg == null && n.StartsWith("RightLeg")) bRLeg = t;
            }
        }
    }
}

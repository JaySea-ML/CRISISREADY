using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Hands;
using UnityEngine.XR.Management;

namespace MRCrisisTrainer.XR
{
    /// <summary>
    /// Profesjonalna wizualizacja dłoni (tylko hand-tracking): rysuje PEŁNY szkielet z XRHandSubsystem
    /// — staw przy każdym jointcie + kości między nimi (5 palców), w przestrzeni świata przez origin riga.
    /// Znika gdy dłoń nie jest śledzona. Zastępuje stare „dwie kulki".
    /// </summary>
    public class HandVisualizer : MonoBehaviour
    {
        [SerializeField] private MonoBehaviour handProviderBehaviour; // (zgodność wstecz — nieużywane przez szkielet)
        [SerializeField] private Transform trackingOrigin;
        [SerializeField] private float jointSize = 0.012f;
        [SerializeField] private float boneWidth = 0.015f;   // grubsze „palce" — wygląda jak dłoń, nie kościotrup
        [SerializeField] private Color skin = new Color(0.86f, 0.66f, 0.52f, 1f);   // kolor skóry — dłonie wyglądają realnie, spójne z ciałem

        private XRHandSubsystem subsystem;
        private Material mat;
        private HandViz left, right;

        private Transform Origin => trackingOrigin != null ? trackingOrigin : transform;

        // łańcuchy palców (od nadgarstka) — do rysowania kości
        private static readonly XRHandJointID[][] Chains =
        {
            new[]{ XRHandJointID.Wrist, XRHandJointID.ThumbMetacarpal, XRHandJointID.ThumbProximal, XRHandJointID.ThumbDistal, XRHandJointID.ThumbTip },
            new[]{ XRHandJointID.Wrist, XRHandJointID.IndexMetacarpal, XRHandJointID.IndexProximal, XRHandJointID.IndexIntermediate, XRHandJointID.IndexDistal, XRHandJointID.IndexTip },
            new[]{ XRHandJointID.Wrist, XRHandJointID.MiddleMetacarpal, XRHandJointID.MiddleProximal, XRHandJointID.MiddleIntermediate, XRHandJointID.MiddleDistal, XRHandJointID.MiddleTip },
            new[]{ XRHandJointID.Wrist, XRHandJointID.RingMetacarpal, XRHandJointID.RingProximal, XRHandJointID.RingIntermediate, XRHandJointID.RingDistal, XRHandJointID.RingTip },
            new[]{ XRHandJointID.Wrist, XRHandJointID.LittleMetacarpal, XRHandJointID.LittleProximal, XRHandJointID.LittleIntermediate, XRHandJointID.LittleDistal, XRHandJointID.LittleTip },
        };

        private class HandViz
        {
            public GameObject root;
            public Dictionary<XRHandJointID, Transform> joints = new Dictionary<XRHandJointID, Transform>();
            public List<Transform> bones = new List<Transform>();
        }

        void Awake()
        {
            var sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            mat = new Material(sh);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", skin); else mat.color = skin;
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.55f);
            // delikatny self-glow → dłonie czytelne także w ciemności (Akt III) i w passthrough
            if (mat.HasProperty("_EmissionColor"))
            {
                mat.EnableKeyword("_EMISSION");
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                mat.SetColor("_EmissionColor", new Color(skin.r, skin.g, skin.b) * 0.12f);   // subtelna poświata (nie hologram)
            }

            left = BuildHand("Hand_L");
            right = BuildHand("Hand_R");
        }

        private HandViz BuildHand(string name)
        {
            var viz = new HandViz();
            viz.root = new GameObject(name);
            viz.root.transform.SetParent(transform, false);

            var unique = new HashSet<XRHandJointID>();
            int boneCount = 0;
            foreach (var chain in Chains)
            {
                foreach (var id in chain) unique.Add(id);
                boneCount += chain.Length - 1;
            }
            foreach (var id in unique)
            {
                var s = GameObject.CreatePrimitive(PrimitiveType.Sphere); s.name = "J_" + id;
                DestroyImmediateCollider(s);
                s.transform.SetParent(viz.root.transform, false);
                float sz = (id == XRHandJointID.Wrist) ? jointSize * 1.8f : jointSize;
                s.transform.localScale = Vector3.one * sz;
                s.GetComponent<Renderer>().sharedMaterial = mat;
                viz.joints[id] = s.transform;
            }
            for (int i = 0; i < boneCount; i++)
            {
                var b = GameObject.CreatePrimitive(PrimitiveType.Cylinder); b.name = "Bone";
                DestroyImmediateCollider(b);
                b.transform.SetParent(viz.root.transform, false);
                b.GetComponent<Renderer>().sharedMaterial = mat;
                viz.bones.Add(b.transform);
            }
            viz.root.SetActive(false);
            return viz;
        }

        private static void DestroyImmediateCollider(GameObject go)
        {
            var c = go.GetComponent<Collider>();
            if (c != null) Destroy(c);
        }

        void OnEnable() => TryAcquire();

        private void TryAcquire()
        {
            if (subsystem != null) return;
            var loader = XRGeneralSettings.Instance?.Manager?.activeLoader;
            if (loader != null) subsystem = loader.GetLoadedSubsystem<XRHandSubsystem>();
            Debug.Log($"[HandViz] TryAcquire: loaderActive={(loader != null)} XRHandSubsystem={(subsystem != null ? "LOADED" : "NULL")}");
        }

        private float _diagT;
        void Update()
        {
            if (subsystem == null) TryAcquire();
            if (subsystem == null) { if (left != null) left.root.SetActive(false); if (right != null) right.root.SetActive(false); return; }
            if (Time.unscaledTime - _diagT > 2f) { _diagT = Time.unscaledTime; Debug.Log($"[HandViz] L.isTracked={subsystem.leftHand.isTracked} R.isTracked={subsystem.rightHand.isTracked} running={subsystem.running}"); }
            UpdateHand(subsystem.leftHand, left);
            UpdateHand(subsystem.rightHand, right);
        }

        private void UpdateHand(XRHand hand, HandViz viz)
        {
            if (viz == null) return;
            if (!hand.isTracked) { viz.root.SetActive(false); return; }
            viz.root.SetActive(true);

            // jointy
            foreach (var kv in viz.joints)
            {
                var joint = hand.GetJoint(kv.Key);
                if (joint.TryGetPose(out var pose))
                {
                    kv.Value.gameObject.SetActive(true);
                    kv.Value.position = Origin.TransformPoint(pose.position);
                }
                else kv.Value.gameObject.SetActive(false);
            }

            // kości (cylinder od jointa do jointa)
            int bi = 0;
            foreach (var chain in Chains)
            {
                for (int i = 0; i < chain.Length - 1; i++)
                {
                    var t = viz.bones[bi++];
                    bool a = viz.joints.TryGetValue(chain[i], out var ja) && ja.gameObject.activeSelf;
                    bool b = viz.joints.TryGetValue(chain[i + 1], out var jb) && jb.gameObject.activeSelf;
                    if (!a || !b) { t.gameObject.SetActive(false); continue; }
                    Vector3 pa = ja.position, pb = jb.position;
                    Vector3 d = pb - pa; float len = d.magnitude;
                    if (len < 1e-4f) { t.gameObject.SetActive(false); continue; }
                    t.gameObject.SetActive(true);
                    t.position = (pa + pb) * 0.5f;
                    t.rotation = Quaternion.FromToRotation(Vector3.up, d / len);
                    t.localScale = new Vector3(boneWidth, len * 0.5f, boneWidth);
                }
            }
        }
    }
}

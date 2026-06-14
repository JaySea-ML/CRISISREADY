using UnityEditor;
using UnityEngine;

namespace MRCrisisTrainer.EditorBootstrap
{
    /// <summary>Weryfikuje import Kenney: shadery materiałów (URP vs magenta) + skalę modeli.</summary>
    public static class KenneyVerify
    {
        [MenuItem("MRCrisis/Verify Kenney Import")]
        public static void Verify()
        {
            AssetDatabase.Refresh();
            string[] paths = {
                "Assets/_Project/External/Kenney/Nature/tree_default.fbx",
                "Assets/_Project/External/Kenney/Nature/tree_pineDefaultA.fbx",
                "Assets/_Project/External/Kenney/Nature/tree_detailed.fbx",
                "Assets/_Project/External/Kenney/Nature/tree_oak.fbx",
                "Assets/_Project/External/Kenney/Car/sedan.fbx",
                "Assets/_Project/External/Kenney/Car/suv.fbx",
            };
            foreach (var p in paths)
            {
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(p);
                if (go == null) { Debug.Log($"[KenneyVerify] MISSING {p}"); continue; }
                var rends = go.GetComponentsInChildren<MeshRenderer>();
                Bounds b = default; bool first = true; string mats = "";
                foreach (var r in rends)
                {
                    var mf = r.GetComponent<MeshFilter>();
                    if (mf != null && mf.sharedMesh != null)
                    {
                        if (first) { b = mf.sharedMesh.bounds; first = false; }
                        else b.Encapsulate(mf.sharedMesh.bounds);
                    }
                    foreach (var m in r.sharedMaterials)
                        mats += m != null ? $"{m.name}<{(m.shader != null ? m.shader.name : "NULLSHADER")}> " : "NULLMAT ";
                }
                Debug.Log($"[KenneyVerify] {p.Substring(p.LastIndexOf('/') + 1)}: renderers={rends.Length} meshSize={b.size} mats=[{mats}]");
            }
            Debug.Log("[KenneyVerify] DONE");
        }
    }
}

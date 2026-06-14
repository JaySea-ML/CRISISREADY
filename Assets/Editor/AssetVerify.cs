using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace MRCrisisTrainer.EditorBootstrap
{
    /// <summary>Weryfikuje nowe assety (Jaguar FBX + racing GLB): polygony, skala, materiały, części.</summary>
    public static class AssetVerify
    {
        [MenuItem("MRCrisis/Verify New Assets")]
        public static void Verify()
        {
            AssetDatabase.Refresh();

            var jag = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/External/CarInterior/Jaguar.fbx");
            if (jag == null) Debug.Log("[AV] Jaguar MISSING");
            else
            {
                var mrs = jag.GetComponentsInChildren<MeshRenderer>();
                int tris = 0; Bounds b = default; bool first = true;
                var mats = new HashSet<string>();
                var parts = new List<string>();
                foreach (var r in mrs)
                {
                    var mf = r.GetComponent<MeshFilter>();
                    if (mf != null && mf.sharedMesh != null)
                    {
                        tris += mf.sharedMesh.triangles.Length / 3;
                        if (first) { b = mf.sharedMesh.bounds; first = false; } else b.Encapsulate(mf.sharedMesh.bounds);
                    }
                    foreach (var m in r.sharedMaterials)
                        if (m != null) mats.Add(m.name + "<" + (m.shader != null ? m.shader.name : "NULL") + ">");
                    parts.Add(r.name);
                }
                Debug.Log($"[AV] Jaguar: renderers={mrs.Length} tris={tris} size={b.size}");
                Debug.Log($"[AV] Jaguar mats=[{string.Join(" | ", mats)}]");
                Debug.Log($"[AV] Jaguar parts=[{string.Join(", ", parts)}]");
            }

            string[] glbs = { "track-straight", "track-corner", "decoration-forest", "vehicle-truck-red" };
            foreach (var g in glbs)
            {
                var go = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/_Project/External/RacingKit/{g}.glb");
                Debug.Log($"[AV] {g}.glb imported = {(go != null)}");
            }
            Debug.Log("[AV] DONE");
        }
    }
}

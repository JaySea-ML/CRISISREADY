using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;

/// <summary>Diagnostyka (tymczasowa): renderuje LeafiaRoom (jak w ActsBuilder) z etykietami Object_N
/// i markerem na obecnym celu strzałki (Object_13), żeby ustalić KTÓRY mesh to ŁÓŻKO, a który BIURKO.</summary>
public static class LeafiaBedInspect
{
    public static void Render()
    {
        var dir = "C:/Users/cieni/Music/MRREALITY2/Logs/leafia"; Directory.CreateDirectory(dir);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/External/LeafiaRoom/LeafiaRoom.glb");
        if (prefab == null) { Debug.Log("[BedInspect] PREFAB NULL"); return; }
        var room = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        var baseRot = room.transform.localRotation;
        room.transform.localRotation = Quaternion.Euler(0f, 180f, 0f) * baseRot;   // jak ActsBuilder
        room.transform.localPosition = Vector3.zero; room.transform.localScale = Vector3.one;
        foreach (var r in room.GetComponentsInChildren<MeshRenderer>(true))
            if (Mathf.Max(r.bounds.size.x, r.bounds.size.y, r.bounds.size.z) > 30f) r.enabled = false;
        // ukryj POWŁOKĘ pokoju (podłoga/sufit/ściany/drzwi >4 m w poziomie), by widzieć MEBLE w środku
        foreach (var r in room.GetComponentsInChildren<MeshRenderer>(true))
            if (r.enabled && (r.bounds.size.x > 4f || r.bounds.size.z > 4f)) r.enabled = false;
        float minY = float.MaxValue;
        foreach (var r in room.GetComponentsInChildren<MeshRenderer>(true)) if (r.enabled) minY = Mathf.Min(minY, r.bounds.min.y);
        room.transform.position += new Vector3(0f, -minY, 0f);

        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.9f, 0.9f, 0.95f); RenderSettings.skybox = null; RenderSettings.fog = false;
        var lg = new GameObject("L"); var L = lg.AddComponent<Light>(); L.type = LightType.Directional; L.intensity = 1.2f; lg.transform.rotation = Quaternion.Euler(55f, 35f, 0f);

        // bounds pokoju
        Bounds bb = default; bool f = true; var cam0 = new GameObject("c").AddComponent<Camera>();
        foreach (var r in room.GetComponentsInChildren<MeshRenderer>(true)) { if (!r.enabled) continue; if (f) { bb = r.bounds; f = false; } else bb.Encapsulate(r.bounds); }
        Object.DestroyImmediate(cam0.gameObject);
        Debug.Log($"[BedInspect] room bounds center={bb.center} size={bb.size}");

        // etykiety + marker Object_13
        foreach (var t in room.GetComponentsInChildren<Transform>(true))
        {
            if (!t.name.StartsWith("Object_")) continue;
            var r = t.GetComponent<MeshRenderer>(); if (r == null || !r.enabled) continue;
            var c = r.bounds.center; var s = r.bounds.size;
            Debug.Log($"[BedInspect] {t.name} center=({c.x:F2},{c.y:F2},{c.z:F2}) size=({s.x:F2},{s.y:F2},{s.z:F2})");
            var go = new GameObject("lbl_" + t.name); go.transform.position = c + Vector3.up * (s.y * 0.5f + 0.2f);
            go.transform.rotation = Quaternion.Euler(90f, 0f, 0f);   // leży płasko — czytelne z kamery z góry
            var tmp = go.AddComponent<TextMeshPro>(); tmp.text = t.name.Replace("Object_", "#"); tmp.fontSize = 5; tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = (t.name == "Object_13") ? new Color(1f, 0.2f, 0.2f) : new Color(1f, 0.95f, 0.2f); tmp.fontStyle = FontStyles.Bold;
            if (t.name == "Object_13")   // marker na obecnym celu strzałki
            {
                var mk = GameObject.CreatePrimitive(PrimitiveType.Sphere); Object.DestroyImmediate(mk.GetComponent<Collider>());
                mk.transform.position = c; mk.transform.localScale = Vector3.one * 0.25f;
                var m = new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color"));
                if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", Color.red); else m.color = Color.red; mk.GetComponent<Renderer>().sharedMaterial = m;
            }
        }

        const int W = 1100, H = 950;
        var camGo = new GameObject("RenderCam"); var cam = camGo.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.08f, 0.08f, 0.1f);
        cam.nearClipPlane = 0.05f; cam.farClipPlane = 60f;
        var rt = new RenderTexture(W, H, 24) { antiAliasing = 4 }; cam.targetTexture = rt;
        var tex = new Texture2D(W, H, TextureFormat.RGB24, false);
        Vector3 ctr = new Vector3(-0.53f, 0f, -0.25f);   // środek pokoju (XZ)

        void Shot(string name) {
            cam.Render();
            var prev = RenderTexture.active; RenderTexture.active = rt; tex.ReadPixels(new Rect(0, 0, W, H), 0, 0); tex.Apply(); RenderTexture.active = prev;
            File.WriteAllBytes($"{dir}/{name}.png", tex.EncodeToPNG());
        }

        // 1) WIDOK Z GÓRY (rzut ortho) — plan pomieszczenia z numerami i markerem #13 (czerwony)
        cam.orthographic = true; cam.orthographicSize = 3.7f;
        cam.transform.position = ctr + new Vector3(0f, 9f, 0f);
        cam.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        Shot("leafia_top");

        // 2) PERSPEKTYWA z przodu-góry — widać KSZTAŁTY mebli (łóżko vs biurko)
        cam.orthographic = false; cam.fieldOfView = 60;
        cam.transform.position = ctr + new Vector3(0.2f, 2.6f, 4.6f);
        cam.transform.rotation = Quaternion.LookRotation((ctr + Vector3.up * 0.6f) - cam.transform.position, Vector3.up);
        Shot("leafia_persp");

        cam.targetTexture = null; Object.DestroyImmediate(rt); Object.DestroyImmediate(tex);
        Debug.Log($"[BedInspect] DONE -> {dir}");
    }
}

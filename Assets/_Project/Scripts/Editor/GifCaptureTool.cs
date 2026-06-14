// GifCaptureTool — renderuje klatki PNG „z rozgrywki" do GIF-ow (batchmode).
// Uruchomienie: Unity.exe -batchmode -executeMethod MRCrisisTrainer.EditorTools.GifCaptureTool.CaptureAll
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace MRCrisisTrainer.EditorTools
{
    public static class GifCaptureTool
    {
        const int W = 640, H = 360;
        static string Root => Path.Combine(Directory.GetParent(Application.dataPath).FullName, "_gifcap");
        static string FramesRoot => Path.Combine(Root, "frames");
        static Camera cam;
        static RenderTexture rt;
        static Texture2D tex;
        static readonly List<string> done = new List<string>();

        public static void CaptureAll()
        {
            int code = 0;
            try
            {
                Directory.CreateDirectory(FramesRoot);
                rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                rt.hideFlags = HideFlags.HideAndDontSave;   // przezyj zmiane sceny
                tex = new Texture2D(W, H, TextureFormat.RGB24, false);
                tex.hideFlags = HideFlags.HideAndDontSave;

                CaptureMainMenu();
                CaptureTutorial();
                CaptureTrainingRoom();
                CaptureGameOver();

                File.WriteAllText(Path.Combine(Root, "DONE.txt"), "OK\n" + string.Join("\n", done));
            }
            catch (Exception e)
            {
                code = 1;
                File.WriteAllText(Path.Combine(Root, "DONE.txt"), "FAIL\n" + e + "\n" + string.Join("\n", done));
            }
            finally { EditorApplication.Exit(code); }
        }

        // ---------- infrastruktura ----------

        static void OpenScene(string name)
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/" + name + ".unity", OpenSceneMode.Single);
            foreach (var c in UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                c.enabled = false;
            SetActive("XR Rig", false);
            SetActive("Reticle", false);
        }

        static void NewCam(Color bg, float fov = 72f, bool skybox = false)
        {
            var old = GameObject.Find("CapCam");
            if (old != null) UnityEngine.Object.DestroyImmediate(old);
            var go = new GameObject("CapCam");
            cam = go.AddComponent<Camera>();
            cam.nearClipPlane = 0.03f;
            cam.farClipPlane = 400f;
            cam.fieldOfView = fov;
            cam.clearFlags = skybox ? CameraClearFlags.Skybox : CameraClearFlags.SolidColor;
            cam.backgroundColor = bg;
            cam.GetUniversalAdditionalCameraData().renderPostProcessing = false;
        }

        static void Shot(string name, int frames, Action<int, float> frame)
        {
            string dir = Path.Combine(FramesRoot, name);
            Directory.CreateDirectory(dir);
            for (int i = 0; i < frames; i++)
            {
                float t = frames > 1 ? i / (float)(frames - 1) : 0f;
                frame(i, t);
                Render(Path.Combine(dir, "f" + i.ToString("0000") + ".png"));
            }
            done.Add(name);
            File.AppendAllText(Path.Combine(Root, "progress.txt"), name + "\n");
        }

        static void Render(string path)
        {
            var req = new UniversalRenderPipeline.SingleCameraRequest { destination = rt };
            if (RenderPipeline.SupportsRenderRequest(cam, req))
                RenderPipeline.SubmitRenderRequest(cam, req);
            else
            {
                var std = new RenderPipeline.StandardRequest { destination = rt };
                if (RenderPipeline.SupportsRenderRequest(cam, std)) RenderPipeline.SubmitRenderRequest(cam, std);
                else { cam.targetTexture = rt; cam.Render(); cam.targetTexture = null; }
            }
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, W, H), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            File.WriteAllBytes(path, tex.EncodeToPNG());
        }

        static Transform FindDeep(string name)
        {
            foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects())
                foreach (var tr in root.GetComponentsInChildren<Transform>(true))
                    if (tr.name == name) return tr;
            return null;
        }

        static void SetActive(string name, bool on)
        {
            var tr = FindDeep(name);
            if (tr != null) tr.gameObject.SetActive(on);
        }

        static Bounds BoundsOf(Transform tr)
        {
            var rs = tr.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0) return new Bounds(tr.position, Vector3.one);
            Bounds b = rs[0].bounds;
            for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
            return b;
        }

        static float Smooth(float t) => t * t * (3f - 2f * t);

        static void LookFrom(Vector3 pos, Vector3 target)
        {
            cam.transform.position = pos;
            cam.transform.LookAt(target);
        }

        static Light MakeLight(string name, Color c, float intensity, float range, Vector3 pos)
        {
            var go = new GameObject(name);
            var l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = c; l.intensity = intensity; l.range = range;
            go.transform.position = pos;
            return l;
        }

        // ---------- MENU ----------

        static void CaptureMainMenu()
        {
            OpenScene("MainMenu");
            NewCam(new Color(0.06f, 0.065f, 0.08f), 68f);
            var canvas = FindDeep("MenuCanvas");
            Vector3 look = canvas != null ? canvas.position : new Vector3(0, 1.5f, 1.85f);
            var buttons = canvas != null
                ? canvas.GetComponentsInChildren<Transform>(true).Where(x => x.name == "Button").ToArray()
                : new Transform[0];
            var baseScale = buttons.Select(b => b.localScale).ToArray();

            Shot("01_menu_glowne", 40, (i, t) =>
            {
                LookFrom(new Vector3(Mathf.Sin(t * Mathf.PI * 2f) * 0.05f, 1.5f, Mathf.Lerp(0.05f, 0.6f, Smooth(t))), look);
                for (int b = 0; b < buttons.Length; b++)
                    buttons[b].localScale = baseScale[b] * (1f + 0.05f * Mathf.Sin(t * Mathf.PI * 4f + b * 1.7f));
            });
        }

        // ---------- TRENING ----------

        static void CaptureTutorial()
        {
            OpenScene("Tutorial");
            NewCam(new Color(0.07f, 0.07f, 0.085f), 72f);
            var canvas = FindDeep("TutorialCanvas");
            var prompt = canvas != null
                ? canvas.GetComponentsInChildren<TMP_Text>(true).FirstOrDefault(x => x.rectTransform.sizeDelta.x > 800f)
                : null;
            Vector3 cv = canvas != null ? canvas.position : new Vector3(0, 1.55f, 1.7f);

            // kierownica ustawiona na wprost kamery, ponizej tablicy
            var wheel = FindDeep("JagSteeringWheel");
            var spin = FindDeep("JagWheelSpin");
            Quaternion spinBase = spin != null ? spin.localRotation : Quaternion.identity;
            if (wheel != null)
            {
                wheel.rotation = Quaternion.LookRotation(Vector3.back) * Quaternion.Euler(-18f, 0f, 0f);
                wheel.position = new Vector3(0f, 0.95f, 0.85f);
                var b = BoundsOf(wheel);
                wheel.position += new Vector3(0f - b.center.x, 0.95f - b.center.y, 0.85f - b.center.z);
            }

            if (prompt != null) prompt.text = "ĆWICZENIE 1 / 3\n\nKIEROWNICA\nObróć kierownicę dłońmi w obie strony";
            Shot("02_trening_kierownica", 40, (i, t) =>
            {
                if (spin != null) spin.localRotation = spinBase * Quaternion.AngleAxis(Mathf.Sin(t * Mathf.PI * 2f) * 70f, Vector3.forward);
                LookFrom(new Vector3(0f, 1.5f, -0.15f), new Vector3(0f, 1.25f, 1.4f));
            });

            if (wheel != null) wheel.gameObject.SetActive(false);
            if (prompt != null) prompt.text = "ĆWICZENIE 2 / 3\n\nMOWA\nPowiedz wyraźnie: POMOCY";
            Shot("03_trening_mowa", 40, (i, t) =>
                LookFrom(new Vector3(Mathf.Sin(t * Mathf.PI * 2f) * 0.04f, 1.53f, Mathf.Lerp(0.1f, 0.4f, Smooth(t))), cv));

            if (prompt != null) prompt.text = "ĆWICZENIE 3 / 3\n\nCISZA\nZachowaj całkowitą ciszę";
            Shot("04_trening_cisza", 40, (i, t) =>
                LookFrom(new Vector3(0f, 1.55f, Mathf.Lerp(0.0f, 0.45f, Smooth(t))), cv));
        }

        // ---------- AKT II + AKT III (TrainingRoom) ----------

        static void CaptureTrainingRoom()
        {
            OpenScene("TrainingRoom");
            Act2Shots();
            Act3Shots();
        }

        static void Act2Shots()
        {
            // tylko swiat jazdy — UWAGA: root Gameplay (akty) siedzi pod LabRoom,
            // wiec wylaczamy wylacznie geometrie pokoju (child Visuals), nie caly LabRoom
            var lab = FindDeep("LabRoom");
            var vis = lab != null ? lab.Find("Visuals") : null;
            if (vis != null) vis.gameObject.SetActive(false);
            SetActive("SunLight", false);
            SetActive("Act3_Call", false);
            SetActive("Thief", false);
            SetActive("Act2_Skid", true);
            SetActive("HUD", false);

            var skyGuid = AssetDatabase.FindAssets("M_DriveSky t:Material").FirstOrDefault();
            var sky = skyGuid != null ? AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(skyGuid)) : null;
            if (sky != null) RenderSettings.skybox = sky;
            NewCam(new Color(0.6f, 0.72f, 0.86f), 72f, skybox: sky != null);

            var dh = FindDeep("DriveHolder");
            var forest = FindDeep("ForestWorld");
            var spin = FindDeep("JagWheelSpin");
            Vector3 eye = dh != null ? dh.TransformPoint(new Vector3(0f, 1.32f, 0f)) : new Vector3(0f, 1.32f, 0f);
            Vector3 fLP = forest != null ? forest.localPosition : Vector3.zero;
            Quaternion fLR = forest != null ? forest.localRotation : Quaternion.identity;
            Quaternion spinBase = spin != null ? spin.localRotation : Quaternion.identity;

            // 05 — spokojna jazda lasem
            Shot("05_akt2_jazda_lasem", 44, (i, t) =>
            {
                if (forest != null) forest.localPosition = fLP + Vector3.back * (t * 46f);
                if (spin != null) spin.localRotation = spinBase * Quaternion.AngleAxis(Mathf.Sin(t * Mathf.PI * 5f) * 4f, Vector3.forward);
                cam.transform.position = eye;
                cam.transform.rotation = Quaternion.LookRotation(Vector3.forward + Vector3.down * 0.06f);
            });

            // 06 — poslizg (swiat sie obraca, kontra kierownica)
            Shot("06_akt2_poslizg", 44, (i, t) =>
            {
                float yaw = Mathf.Sin(t * Mathf.PI * 3f) * 8f;
                if (forest != null)
                {
                    forest.localPosition = fLP + Vector3.back * (t * 34f);
                    forest.localRotation = fLR * Quaternion.AngleAxis(yaw, Vector3.up);
                }
                if (spin != null) spin.localRotation = spinBase * Quaternion.AngleAxis(-yaw * 6f, Vector3.forward);
                cam.transform.position = eye;
                cam.transform.rotation = Quaternion.LookRotation(Vector3.forward + Vector3.down * 0.05f) * Quaternion.Euler(0f, 0f, -yaw * 0.35f);
            });
            if (forest != null) { forest.localPosition = fLP; forest.localRotation = fLR; }

            // 07 — dzwoni telefon na fotelu pasazera
            var seatPhone = FindDeep("SeatPhone");
            Light ring = null;
            if (seatPhone != null)
            {
                seatPhone.gameObject.SetActive(true);
                var rl = seatPhone.GetComponentsInChildren<Light>(true).FirstOrDefault();
                ring = rl != null ? rl : MakeLight("CapRing", new Color(0.3f, 0.85f, 1f), 0f, 1.4f, seatPhone.position + Vector3.up * 0.2f);
                ring.enabled = true;
            }
            Vector3 phonePos = seatPhone != null ? BoundsOf(seatPhone).center : new Vector3(0.38f, 0.45f, 0.18f);
            Quaternion phoneRot = seatPhone != null ? seatPhone.rotation : Quaternion.identity;
            Shot("07_akt2_telefon_dzwoni", 40, (i, t) =>
            {
                if (ring != null) ring.intensity = 1.2f + 1.6f * Mathf.Abs(Mathf.Sin(t * Mathf.PI * 6f));
                if (seatPhone != null) seatPhone.rotation = phoneRot * Quaternion.Euler(0f, 0f, Mathf.Sin(t * Mathf.PI * 16f) * 3f);
                LookFrom(eye + new Vector3(-0.05f, 0f, -0.05f), Vector3.Lerp(phonePos + Vector3.up * 0.4f, phonePos, Smooth(t)));
            });
            if (seatPhone != null) { seatPhone.rotation = phoneRot; seatPhone.gameObject.SetActive(false); }

            // 08 — ciezarowka nadjezdza z naprzeciwka (dokladnie jak w grze: LookRotation(-fwd), kola na jezdni)
            var truck = FindDeep("CrashTruck");
            if (truck != null)
            {
                truck.gameObject.SetActive(true);
                Vector3 fwd = dh != null ? dh.forward : Vector3.forward; fwd.y = 0f; fwd.Normalize();
                Quaternion face = Quaternion.LookRotation(-fwd, Vector3.up);
                truck.SetPositionAndRotation(new Vector3(eye.x, 0f, eye.z), face);
                float yWheels = -BoundsOf(truck).min.y;
                Shot("08_akt2_ciezarowka", 44, (i, t) =>
                {
                    float k = t * t;
                    Vector3 p = Vector3.Lerp(eye + fwd * 46f, eye + fwd * 5f, k);
                    p.y = yWheels;
                    truck.SetPositionAndRotation(p, face);
                    if (forest != null) forest.localPosition = fLP + Vector3.back * (t * 26f);
                    float shake = t > 0.85f ? (t - 0.85f) * 0.25f : 0f;
                    cam.transform.position = eye + new Vector3(Mathf.Sin(i * 2.7f), Mathf.Cos(i * 3.1f), 0f) * shake;
                    cam.transform.LookAt(p + Vector3.up * 1.6f);
                });
                truck.gameObject.SetActive(false);
            }
            if (forest != null) forest.localPosition = fLP;

            // 09 — rozejrzenie po kokpicie
            Shot("09_akt2_kokpit", 40, (i, t) =>
            {
                float yaw = Mathf.Lerp(-42f, 42f, Smooth(t));
                cam.transform.position = eye;
                cam.transform.rotation = Quaternion.Euler(8f, yaw, 0f);
            });
        }

        static void Act3Shots()
        {
            // Akt III rozgrywa sie w SYPIALNI (LeafiaRoom pod HidingHolder), nie w labie
            SetActive("Act2_Skid", false);
            SetActive("LabRoom", true);
            var lab3 = FindDeep("LabRoom");
            var vis3 = lab3 != null ? lab3.Find("Visuals") : null;
            if (vis3 != null) vis3.gameObject.SetActive(false);   // geometria labu precz — zostaje sypialnia
            SetActive("SunLight", false);
            SetActive("Light_LED_1", false);
            SetActive("Light_LED_2", false);
            SetActive("Act3_Call", true);
            SetActive("Thief", false);
            SetActive("HUD", false);

            // aktywuj sypialnie (HidingHolder + przodkowie) — drzwi, lampa, lozko
            var hh = FindDeep("HidingHolder");
            if (hh != null)
                for (var pt = hh; pt != null; pt = pt.parent) pt.gameObject.SetActive(true);
            Vector3 HP(Vector3 l) => hh != null ? hh.TransformPoint(l) : l;
            Vector3 HD(Vector3 l) => hh != null ? hh.TransformDirection(l) : l;
            // lozko prosto z modelu sypialni (Object_27 = lozko wg buildera); BedHideTarget bywa poza pokojem w bake
            Vector3 bed = HP(new Vector3(-1.5f, 0.12f, -0.6f));
            var leafia = FindDeep("LeafiaRoom");
            var bedObj = leafia != null
                ? leafia.GetComponentsInChildren<Transform>(true).FirstOrDefault(x => x.name == "Object_27")
                : null;
            // POKOJ W BAKE STOI ZA DRZWIAMI (z>3.1), podloga na wysokosci spodu lozka (~0.27)
            float floorY = 0.27f;
            if (bedObj != null)
            {
                var bb = BoundsOf(bedObj);
                floorY = Mathf.Clamp(bb.min.y, -0.1f, 1f);
                bed = new Vector3(bb.center.x, floorY + 0.12f, bb.center.z);
            }
            else bed = HP(new Vector3(-1.5f, floorY + 0.12f, 4.76f));
            Vector3 door = HP(new Vector3(0f, floorY, 3.1f));

            // DEBUG — zrzut pozycji do pliku
            var dbg = new System.Text.StringBuilder();
            dbg.AppendLine("hh=" + (hh != null ? hh.name + " pos=" + hh.position + " scale=" + hh.lossyScale : "NULL"));
            dbg.AppendLine("HP(0,0,0)=" + HP(Vector3.zero) + "  HP(0,1.5,0.1)=" + HP(new Vector3(0f, 1.5f, 0.1f)));
            dbg.AppendLine("leafia=" + (leafia != null ? leafia.name + " pos=" + leafia.position : "NULL"));
            if (leafia != null) dbg.AppendLine("leafiaBounds=" + BoundsOf(leafia).center + " size=" + BoundsOf(leafia).size);
            dbg.AppendLine("bedObj=" + (bedObj != null ? bedObj.name + " bounds=" + BoundsOf(bedObj).center + " size=" + BoundsOf(bedObj).size : "NULL"));
            dbg.AppendLine("bed=" + bed + "  door=" + door);
            File.WriteAllText(Path.Combine(Root, "debug3.txt"), dbg.ToString());
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.05f, 0.06f, 0.10f);
            var moonGO = new GameObject("CapMoon");
            var moon = moonGO.AddComponent<Light>();
            moon.type = LightType.Directional;
            moon.color = new Color(0.55f, 0.65f, 1f);
            moon.intensity = 0.35f;
            moonGO.transform.rotation = Quaternion.Euler(38f, -25f, 0f);

            NewCam(new Color(0.015f, 0.018f, 0.03f), 72f);

            // 10 — nocna sypialnia (rozejrzenie sie z wnetrza pokoju, od drzwi)
            Shot("10_akt3_pokoj_noca", 44, (i, t) =>
            {
                float yaw = Mathf.Lerp(-65f, 65f, Smooth(t));
                cam.transform.position = HP(new Vector3(0f, floorY + 1.45f, 3.55f));
                cam.transform.rotation = (hh != null ? hh.rotation : Quaternion.identity) * Quaternion.Euler(9f, yaw, 0f);
            });

            // intruz + animacja chodu
            var thief = FindDeep("Thief");
            Animator anim = null;
            AnimationClip walk = null;
            if (thief != null)
            {
                // rodzice (HidingHolder!) sa nieaktywni w bake — aktywuj caly lancuch w gore
                for (var pt = thief.parent; pt != null; pt = pt.parent) pt.gameObject.SetActive(true);
                thief.gameObject.SetActive(true);
                // odkryj TYLKO galaz Visual (jak ThiefWanderAI.Appear) — NIE flash i inne efekty
                var visual = thief.Find("Visual");
                if (visual != null)
                {
                    foreach (var tr2 in visual.GetComponentsInChildren<Transform>(true)) tr2.gameObject.SetActive(true);
                    foreach (var r2 in visual.GetComponentsInChildren<Renderer>(true)) r2.enabled = true;
                }
                else
                    foreach (var r2 in thief.GetComponentsInChildren<Renderer>(true)) r2.enabled = true;
                anim = thief.GetComponentInChildren<Animator>(true);
                if (anim != null && anim.runtimeAnimatorController != null)
                {
                    var clips = anim.runtimeAnimatorController.animationClips;
                    walk = clips.FirstOrDefault(c => c.name.ToLowerInvariant().Contains("walk"));
                    if (walk == null && clips.Length > 0) walk = clips[0];
                }
                if (walk != null) AnimationMode.StartAnimationMode();
            }

            void PoseThief(Vector3 pos, Vector3 dir, float animT)
            {
                if (thief == null) return;
                if (walk != null && anim != null)
                {
                    AnimationMode.BeginSampling();
                    AnimationMode.SampleAnimationClip(anim.gameObject, walk, animT % walk.length);
                    AnimationMode.EndSampling();
                }
                thief.rotation = Quaternion.LookRotation(dir, Vector3.up);
                thief.position = pos;   // pivot Mixamo = stopy, podloga sypialni na y=0
            }

            // 11 — intruz wchodzi przez drzwi i idzie ku lozku (kamera spod lozka)
            Vector3 bedCamDir = (door - bed); bedCamDir.y = 0f; bedCamDir.Normalize();
            Vector3 bedCam = bed + bedCamDir * 0.55f; bedCam.y = floorY + 0.17f;
            Vector3 walkStart = door + HD(Vector3.forward) * 0.3f;
            Vector3 walkEnd = bed + bedCamDir * 1.1f; walkEnd.y = floorY;
            walkStart.y = floorY;
            Vector3 walkDir = (walkEnd - walkStart); walkDir.y = 0f; walkDir.Normalize();
            Vector3 overBedCam = bed - bedCamDir * 0.45f; overBedCam.y = floorY + 1.08f;
            Shot("11_akt3_intruz", 48, (i, t) =>
            {
                Vector3 p = Vector3.Lerp(walkStart, walkEnd, Smooth(t));
                PoseThief(p, walkDir, t * (walk != null ? walk.length : 1f) * 2.2f);
                LookFrom(overBedCam, p + Vector3.up * 0.85f);
            });

            // 12 — pod lozkiem, telefon swieci, intruz krazy w glebi
            var phone = FindDeep("Phone");
            Light glow = MakeLight("CapGlow", new Color(0.3f, 0.85f, 1f), 1.5f, 1.6f, bedCam + bedCamDir * 0.7f);
            Vector3 phoneTarget = bedCam + bedCamDir * 0.75f; phoneTarget.y = floorY + 0.02f;
            if (phone != null)
            {
                phone.gameObject.SetActive(true);
                var pb = BoundsOf(phone);
                phone.position += new Vector3(phoneTarget.x - pb.center.x, phoneTarget.y - pb.min.y, phoneTarget.z - pb.center.z);
                if (Vector3.Distance(BoundsOf(phone).center, phoneTarget) > 1.5f) phone.position = phoneTarget;
                glow.transform.position = phoneTarget + Vector3.up * 0.18f;
            }
            Vector3 phoneLook = phoneTarget + Vector3.up * 0.07f;
            Shot("12_akt3_pod_lozkiem", 40, (i, t) =>
            {
                glow.intensity = 0.7f + 2.0f * Mathf.Abs(Mathf.Sin(t * Mathf.PI * 5f));
                Vector3 walkP = Vector3.Lerp(HP(new Vector3(0.9f, floorY, 5.7f)), HP(new Vector3(0.1f, floorY, 3.6f)), t);
                Vector3 wd = HP(new Vector3(0.1f, floorY, 3.6f)) - HP(new Vector3(0.9f, floorY, 5.7f)); wd.y = 0f;
                PoseThief(walkP, wd.normalized, t * (walk != null ? walk.length : 1f) * 1.8f);
                LookFrom(bedCam, phoneLook);
            });

            // 13 — rozmowa ze 112 (czerwona kwestia gracza nad telefonem)
            var dlgGO = new GameObject("CapDialog");
            var dlg = dlgGO.AddComponent<TextMeshPro>();
            dlg.text = "Warszawa, ulica Długa 12,\nmieszkanie 5";
            dlg.fontSize = 0.62f;
            dlg.fontStyle = FontStyles.Bold;
            dlg.alignment = TextAlignmentOptions.Center;
            dlg.color = new Color(1f, 0.08f, 0.10f);
            dlg.rectTransform.sizeDelta = new Vector2(2.2f, 0.8f);
            var dlgPos = bedCam + bedCamDir * 1.15f; dlgPos.y = floorY + 0.16f;
            dlgGO.transform.position = dlgPos;
            Shot("13_akt3_rozmowa_112", 40, (i, t) =>
            {
                glow.intensity = 0.7f + 2.0f * Mathf.Abs(Mathf.Sin(t * Mathf.PI * 5f));
                var c = dlg.color; c.a = 0.82f + 0.18f * Mathf.Sin(t * Mathf.PI * 4f); dlg.color = c;
                cam.transform.position = bedCam;
                cam.transform.rotation = Quaternion.LookRotation((dlgGO.transform.position - bedCam).normalized);
                dlgGO.transform.rotation = Quaternion.LookRotation(dlgGO.transform.position - bedCam);
            });
            UnityEngine.Object.DestroyImmediate(dlgGO);
            if (phone != null) phone.gameObject.SetActive(false);
            glow.gameObject.SetActive(false);

            // 14 — jumpscare (twarz przy twarzy, w sypialni)
            if (thief != null)
            {
                Shot("14_akt3_jumpscare", 30, (i, t) =>
                {
                    PoseThief(HP(new Vector3(-0.6f, floorY, 4.6f)), bedCamDir * -1f, t * 0.6f);
                    Vector3 head;
                    var hb = anim != null && anim.isHuman ? anim.GetBoneTransform(HumanBodyBones.Head) : null;
                    if (hb != null) head = hb.position;
                    else { var b = BoundsOf(thief); head = new Vector3(b.center.x, b.max.y - 0.14f, b.center.z); }
                    float d = Mathf.Lerp(1.15f, 0.34f, Mathf.Pow(t, 2.2f));
                    Vector3 p = head + thief.forward * d;
                    cam.transform.position = p;
                    cam.transform.rotation = Quaternion.LookRotation(head - p) * Quaternion.Euler(0f, 0f, Mathf.Sin(t * Mathf.PI * 6f) * 2.5f);
                });
            }

            if (walk != null) AnimationMode.StopAnimationMode();
        }

        // ---------- KONIEC GRY ----------

        static void CaptureGameOver()
        {
            OpenScene("GameOver");
            NewCam(Color.black, 66f);
            var canvas = FindDeep("GameOverCanvas");
            Vector3 look = canvas != null ? canvas.position : new Vector3(0f, 1.5f, 1.8f);
            var texts = canvas != null ? canvas.GetComponentsInChildren<TMP_Text>(true) : new TMP_Text[0];
            var msg = texts.FirstOrDefault(x => x.fontSize > 60f);
            var btn = canvas != null
                ? canvas.GetComponentsInChildren<Transform>(true).FirstOrDefault(x => x.name == "Button")
                : null;
            Vector3 btnScale = btn != null ? btn.localScale : Vector3.one;

            if (msg != null) { msg.text = "KONIEC"; msg.color = new Color(0.95f, 0.22f, 0.22f); }
            Shot("15_koniec_przegrana", 40, (i, t) =>
            {
                LookFrom(new Vector3(0f, 1.5f, Mathf.Lerp(0.0f, 0.45f, Smooth(t))), look);
                if (btn != null) btn.localScale = btnScale * (1f + 0.06f * Mathf.Sin(t * Mathf.PI * 4f));
            });

            if (msg != null)
            {
                msg.text = "POLICJA NA MIEJSCU\nJESTEŚ BEZPIECZNY";
                msg.fontSize = 50f;
                msg.color = new Color(0.30f, 1f, 0.45f);
            }
            Shot("16_koniec_wygrana", 40, (i, t) =>
            {
                LookFrom(new Vector3(0f, 1.5f, Mathf.Lerp(0.0f, 0.45f, Smooth(t))), look);
                if (btn != null) btn.localScale = btnScale * (1f + 0.06f * Mathf.Sin(t * Mathf.PI * 4f));
            });
        }
    }
}

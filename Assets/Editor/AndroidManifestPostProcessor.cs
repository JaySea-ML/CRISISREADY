using System.IO;
using System.Xml;
using UnityEditor.Android;
using UnityEngine;

namespace MRCrisisTrainer.EditorTools
{
    /// <summary>
    /// Wstrzykuje do wygenerowanego AndroidManifest.xml wpisy wymagane przez Quest 3
    /// do passthrough + Scene API + hand tracking. Meta auto-injector nie działa na OpenXR,
    /// więc robimy to ręcznie przy każdym buildzie Androida.
    /// </summary>
    public class AndroidManifestPostProcessor : IPostGenerateGradleAndroidProject
    {
        public int callbackOrder => 1;

        public void OnPostGenerateGradleAndroidProject(string path)
        {
            string manifestPath = Path.Combine(path, "src", "main", "AndroidManifest.xml");
            if (!File.Exists(manifestPath))
            {
                // niektóre wersje: bez src/main
                manifestPath = Path.Combine(path, "AndroidManifest.xml");
                if (!File.Exists(manifestPath)) { Debug.LogWarning("[ManifestPP] manifest not found at " + path); return; }
            }

            var doc = new XmlDocument();
            doc.Load(manifestPath);
            const string androidNs = "http://schemas.android.com/apk/res/android";

            var manifest = doc.DocumentElement;          // <manifest>
            var application = manifest.SelectSingleNode("application") as XmlElement;

            // --- uses-feature: passthrough + hand tracking ---
            AddUsesFeature(doc, manifest, androidNs, "com.oculus.feature.PASSTHROUGH", true);
            AddUsesFeature(doc, manifest, androidNs, "oculus.software.handtracking", false);
            AddUsesFeature(doc, manifest, androidNs, "com.oculus.feature.RENDER_MODEL", false);

            // --- uses-permission: hand tracking, scene, anchors, mic ---
            AddUsesPermission(doc, manifest, androidNs, "com.oculus.permission.HAND_TRACKING");
            AddUsesPermission(doc, manifest, androidNs, "com.oculus.permission.USE_PASSTHROUGH");
            AddUsesPermission(doc, manifest, androidNs, "com.oculus.permission.USE_SCENE");
            AddUsesPermission(doc, manifest, androidNs, "com.oculus.permission.USE_ANCHOR_API");
            AddUsesPermission(doc, manifest, androidNs, "android.permission.RECORD_AUDIO");

            // --- meta-data: supported devices + passthrough hint ---
            if (application != null)
            {
                AddMetaData(doc, application, androidNs, "com.oculus.supportedDevices", "quest2|questpro|quest3|quest3s");
                AddMetaData(doc, application, androidNs, "com.oculus.ossplash.background", "passthrough-contextual");
            }

            doc.Save(manifestPath);
            Debug.Log("[ManifestPP] Passthrough + hand tracking + scene wpisy dodane do AndroidManifest.");
        }

        private static void AddUsesFeature(XmlDocument doc, XmlElement manifest, string ns, string name, bool required)
        {
            foreach (XmlElement e in manifest.SelectNodes("uses-feature"))
                if (e.GetAttribute("name", ns) == name) { e.SetAttribute("required", ns, required ? "true" : "false"); return; }
            var f = doc.CreateElement("uses-feature");
            f.SetAttribute("name", ns, name);
            f.SetAttribute("required", ns, required ? "true" : "false");
            manifest.AppendChild(f);
        }

        private static void AddUsesPermission(XmlDocument doc, XmlElement manifest, string ns, string name)
        {
            foreach (XmlElement e in manifest.SelectNodes("uses-permission"))
                if (e.GetAttribute("name", ns) == name) return;
            var p = doc.CreateElement("uses-permission");
            p.SetAttribute("name", ns, name);
            manifest.AppendChild(p);
        }

        private static void AddMetaData(XmlDocument doc, XmlElement application, string ns, string name, string value)
        {
            foreach (XmlElement e in application.SelectNodes("meta-data"))
                if (e.GetAttribute("name", ns) == name) { e.SetAttribute("value", ns, value); return; }
            var m = doc.CreateElement("meta-data");
            m.SetAttribute("name", ns, name);
            m.SetAttribute("value", ns, value);
            application.AppendChild(m);
        }
    }
}

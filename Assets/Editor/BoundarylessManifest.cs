#if UNITY_ANDROID
using System.IO;
using System.Xml;
using UnityEditor.Android;
using UnityEngine;

namespace MRCrisisTrainer.EditorBuild
{
    /// <summary>
    /// Wstrzykuje do wygenerowanego AndroidManifest.xml feature BOUNDARYLESS_APP — dzięki temu Quest NIE wymaga
    /// ani nie pokazuje granicy (Guardian) w naszej grze MR (siedzącej, passthrough). Koniec komunikatu
    /// „Utwórz nową granicę". Zgodne z dokumentacją Meta (Boundaryless Mode for Mixed Reality).
    /// </summary>
    public class BoundarylessManifest : IPostGenerateGradleAndroidProject
    {
        public int callbackOrder => 100;   // po preprocesorze Meta (żeby wpis nie został nadpisany)

        public void OnPostGenerateGradleAndroidProject(string path)
        {
            string manifestPath = Path.Combine(path, "src", "main", "AndroidManifest.xml");
            if (!File.Exists(manifestPath))
            {
                Debug.LogWarning("[Boundaryless] AndroidManifest.xml nie znaleziony: " + manifestPath);
                return;
            }

            const string androidNs = "http://schemas.android.com/apk/res/android";
            var doc = new XmlDocument();
            doc.Load(manifestPath);
            var root = doc.DocumentElement;   // <manifest>
            if (root == null) return;

            // już dodane?
            foreach (XmlNode n in doc.GetElementsByTagName("uses-feature"))
            {
                var a = n.Attributes?["android:name"];
                if (a != null && a.Value == "com.oculus.feature.BOUNDARYLESS_APP")
                {
                    Debug.Log("[Boundaryless] feature już obecny — pomijam.");
                    return;
                }
            }

            var feat = doc.CreateElement("uses-feature");
            var nameAttr = doc.CreateAttribute("android", "name", androidNs); nameAttr.Value = "com.oculus.feature.BOUNDARYLESS_APP";
            var reqAttr = doc.CreateAttribute("android", "required", androidNs); reqAttr.Value = "true";
            feat.Attributes.Append(nameAttr);
            feat.Attributes.Append(reqAttr);
            root.AppendChild(feat);

            doc.Save(manifestPath);
            Debug.Log("[Boundaryless] dodano com.oculus.feature.BOUNDARYLESS_APP do " + manifestPath);
        }
    }
}
#endif

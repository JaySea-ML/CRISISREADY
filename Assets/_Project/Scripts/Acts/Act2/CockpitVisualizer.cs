using UnityEngine;

namespace MRCrisisTrainer.Acts.Act2
{
    /// <summary>
    /// Pokazuje/ukrywa wirtualne elementy kokpitu (kierownica, dashboard, szyba)
    /// z fade-in efektem przy starcie aktu.
    /// </summary>
    public class CockpitVisualizer : MonoBehaviour
    {
        [SerializeField] private GameObject[] cockpitElements;
        [SerializeField] private float fadeInDuration = 1.0f;

        private float fadeT;
        private bool fadingIn;

        void Awake()
        {
            SetAlpha(0f);
        }

        public void ShowCockpit()
        {
            foreach (var el in cockpitElements)
            {
                if (el != null) el.SetActive(true);
            }
            fadingIn = true;
            fadeT = 0f;
        }

        public void HideCockpit()
        {
            foreach (var el in cockpitElements)
            {
                if (el != null) el.SetActive(false);
            }
        }

        void Update()
        {
            if (!fadingIn) return;
            fadeT += Time.deltaTime / Mathf.Max(0.01f, fadeInDuration);
            SetAlpha(Mathf.Clamp01(fadeT));
            if (fadeT >= 1f) fadingIn = false;
        }

        private void SetAlpha(float a)
        {
            foreach (var el in cockpitElements)
            {
                if (el == null) continue;
                foreach (var r in el.GetComponentsInChildren<Renderer>())
                {
                    foreach (var mat in r.materials)
                    {
                        if (mat.HasProperty("_BaseColor"))
                        {
                            var c = mat.GetColor("_BaseColor"); c.a = a; mat.SetColor("_BaseColor", c);
                        }
                        else if (mat.HasProperty("_Color"))
                        {
                            var c = mat.GetColor("_Color"); c.a = a; mat.SetColor("_Color", c);
                        }
                    }
                }
            }
        }
    }
}

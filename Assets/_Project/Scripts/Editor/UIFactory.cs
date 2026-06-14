using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace MRCrisisTrainer.EditorTools
{
    /// <summary>Pomocniki do programatycznego budowania world-space UI (HUD, menu, kwestionariusze).</summary>
    public static class UIFactory
    {
        public static Canvas WorldCanvas(string name, Vector3 pos, Quaternion rot, Vector2 sizeDelta, float scale = 0.0025f)
        {
            var go = new GameObject(name, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            go.transform.position = pos;
            go.transform.rotation = rot;
            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = sizeDelta;
            go.transform.localScale = Vector3.one * scale;
            return canvas;
        }

        public static Image Panel(Transform parent, Vector2 size, Color color, Vector2 anchoredPos = default)
        {
            var go = new GameObject("Panel", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.color = color;
            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = size;
            rt.anchoredPosition = anchoredPos;
            return img;
        }

        public static TMP_Text Text(Transform parent, string content, float fontSize, Vector2 size, Vector2 anchoredPos,
            TextAlignmentOptions align = TextAlignmentOptions.Center, Color? color = null)
        {
            var go = new GameObject("Text", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = content;
            tmp.fontSize = fontSize;
            tmp.alignment = align;
            tmp.color = color ?? Color.white;
            tmp.textWrappingMode = TextWrappingModes.Normal;
            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = size;
            rt.anchoredPosition = anchoredPos;
            return tmp;
        }

        public static Image FilledBar(Transform parent, Vector2 size, Vector2 anchoredPos, Color color)
        {
            var go = new GameObject("Bar", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.color = color;
            img.type = Image.Type.Filled;
            img.fillMethod = Image.FillMethod.Horizontal;
            img.fillAmount = 0f;
            img.sprite = WhiteSprite();
            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = size;
            rt.anchoredPosition = anchoredPos;
            return img;
        }

        public static Button Button(Transform parent, string label, Vector2 size, Vector2 anchoredPos, Color? bg = null)
        {
            var go = new GameObject("Button", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.color = bg ?? new Color(0.2f, 0.4f, 0.8f);
            img.sprite = WhiteSprite();
            var btn = go.AddComponent<Button>();
            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = size;
            rt.anchoredPosition = anchoredPos;
            var txt = Text(go.transform, label, 28, size, Vector2.zero);
            txt.color = Color.white;
            return btn;
        }

        public static Toggle Toggle(Transform parent, string label, Vector2 anchoredPos)
        {
            var go = new GameObject("Toggle", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(700, 60);
            rt.anchoredPosition = anchoredPos;
            var toggle = go.AddComponent<Toggle>();

            var box = new GameObject("Box", typeof(RectTransform));
            box.transform.SetParent(go.transform, false);
            var boxImg = box.AddComponent<Image>();
            boxImg.color = Color.white;
            boxImg.sprite = WhiteSprite();
            var boxRt = box.GetComponent<RectTransform>();
            boxRt.sizeDelta = new Vector2(44, 44);
            boxRt.anchoredPosition = new Vector2(-320, 0);

            var check = new GameObject("Check", typeof(RectTransform));
            check.transform.SetParent(box.transform, false);
            var checkImg = check.AddComponent<Image>();
            checkImg.color = new Color(0.2f, 0.8f, 0.3f);
            checkImg.sprite = WhiteSprite();
            var checkRt = check.GetComponent<RectTransform>();
            checkRt.sizeDelta = new Vector2(30, 30);

            toggle.targetGraphic = boxImg;
            toggle.graphic = checkImg;
            toggle.isOn = false;

            var txt = Text(go.transform, label, 24, new Vector2(620, 60), new Vector2(20, 0), TextAlignmentOptions.Left);
            txt.color = Color.white;
            return toggle;
        }

        private static Sprite cachedWhite;
        public static Sprite WhiteSprite()
        {
            if (cachedWhite != null) return cachedWhite;
            var tex = Texture2D.whiteTexture;
            cachedWhite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
            return cachedWhite;
        }

        /// <summary>Tworzy EventSystem jeśli nie istnieje (wymagany dla UI w VR ray-interaction).</summary>
        public static void EnsureEventSystem()
        {
#if UNITY_2023_1_OR_NEWER
            var existing = Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>();
#else
            var existing = Object.FindObjectOfType<UnityEngine.EventSystems.EventSystem>();
#endif
            if (existing == null)
            {
                var es = new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem),
                    typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));
            }
        }
    }
}

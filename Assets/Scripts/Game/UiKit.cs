using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RobotArena
{
    /// <summary>
    /// Shared look for every screen: generated rounded panels, glossy buttons with a
    /// press bounce and click sound, stat bars, sliders and icons. No image files needed.
    /// </summary>
    public static class UiKit
    {
        // Palette: deep navy panels, bright cyan and warm orange accents.
        public static readonly Color PanelColor = new Color(0.05f, 0.08f, 0.15f, 0.88f);
        public static readonly Color PanelEdge = new Color(0.3f, 0.75f, 1f, 0.55f);
        public static readonly Color Cyan = new Color(0.25f, 0.85f, 1f);
        public static readonly Color Orange = new Color(1f, 0.62f, 0.12f);
        public static readonly Color Yellow = new Color(1f, 0.86f, 0.25f);
        public static readonly Color Red = new Color(1f, 0.28f, 0.22f);
        public static readonly Color Green = new Color(0.3f, 1f, 0.5f);
        public static readonly Color TextDim = new Color(0.75f, 0.85f, 1f, 0.85f);

        static Font font;
        static Sprite rounded, roundedOutline, sheen, circle, ring, triangle, gear, pauseIcon;

        public static Font Font
        {
            get
            {
                if (font == null)
                {
                    try { font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); } catch { }
                    if (font == null) { try { font = Resources.GetBuiltinResource<Font>("Arial.ttf"); } catch { } }
                }
                return font;
            }
        }

        // ---------- Generated sprites ----------

        delegate float Shape(float x, float y, int size);

        static Sprite MakeSprite(string name, int size, Shape alpha, Vector4 border)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = name, filterMode = FilterMode.Bilinear };
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float a = Mathf.Clamp01(alpha(x + 0.5f, y + 0.5f, size));
                px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
        }

        static float RoundRectDist(float x, float y, int size, float radius)
        {
            float h = size * 0.5f;
            float qx = Mathf.Abs(x - h) - (h - radius);
            float qy = Mathf.Abs(y - h) - (h - radius);
            float outside = new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude;
            return outside + Mathf.Min(Mathf.Max(qx, qy), 0f) - radius;
        }

        public static Sprite Rounded => rounded != null ? rounded : rounded =
            MakeSprite("Rounded", 96, (x, y, s) => 0.5f - RoundRectDist(x, y, s, 30f), new Vector4(34, 34, 34, 34));

        public static Sprite RoundedOutline => roundedOutline != null ? roundedOutline : roundedOutline =
            MakeSprite("RoundedOutline", 96, (x, y, s) =>
            {
                float d = RoundRectDist(x, y, s, 30f);
                return Mathf.Min(0.5f - d, d + 4.5f);
            }, new Vector4(34, 34, 34, 34));

        /// <summary>Soft white at the top fading out: a glossy highlight over buttons and panels.</summary>
        public static Sprite Sheen => sheen != null ? sheen : sheen =
            MakeSprite("Sheen", 96, (x, y, s) => (0.5f - RoundRectDist(x, y, s, 30f)) * Mathf.Clamp01((y / s - 0.45f) * 1.6f) * 0.35f, new Vector4(34, 34, 34, 34));

        public static Sprite Circle => circle != null ? circle : circle =
            MakeSprite("Circle", 128, (x, y, s) => s * 0.5f - Vector2.Distance(new Vector2(x, y), Vector2.one * s * 0.5f), Vector4.zero);

        public static Sprite Ring => ring != null ? ring : ring =
            MakeSprite("Ring", 128, (x, y, s) =>
            {
                float d = Vector2.Distance(new Vector2(x, y), Vector2.one * s * 0.5f);
                return Mathf.Min(s * 0.5f - d, d - (s * 0.5f - 7f));
            }, Vector4.zero);

        public static Sprite Triangle => triangle != null ? triangle : triangle =
            MakeSprite("Triangle", 64, (x, y, s) =>
            {
                // Arrow pointing up.
                float u = x / s, v = y / s;
                float half = (1f - v) * 0.42f;
                float edge = Mathf.Min(half - Mathf.Abs(u - 0.5f), v - 0.08f);
                if (v > 0.92f) edge = -1f;
                return edge * s;
            }, Vector4.zero);

        public static Sprite Gear => gear != null ? gear : gear =
            MakeSprite("Gear", 128, (x, y, s) =>
            {
                Vector2 p = new Vector2(x, y) - Vector2.one * s * 0.5f;
                float r = p.magnitude;
                float ang = Mathf.Atan2(p.y, p.x);
                float teeth = Mathf.Clamp01(Mathf.Cos(ang * 8f) * 3f + 0.5f);
                float outer = Mathf.Lerp(s * 0.36f, s * 0.47f, teeth);
                return Mathf.Min(outer - r, r - s * 0.17f);
            }, Vector4.zero);

        public static Sprite PauseIcon => pauseIcon != null ? pauseIcon : pauseIcon =
            MakeSprite("Pause", 64, (x, y, s) =>
            {
                float u = x / s, v = y / s;
                bool bar = (u > 0.26f && u < 0.42f) || (u > 0.58f && u < 0.74f);
                return bar && v > 0.2f && v < 0.8f ? 1f : 0f;
            }, Vector4.zero);

        // ---------- Layout helpers ----------

        public static RectTransform Rect(string name, Transform parent, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            return Rect(name, parent, anchor, anchor, pos, size);
        }

        public static RectTransform Rect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return rt;
        }

        public static RectTransform Fill(string name, Transform parent)
        {
            var rt = Rect(name, parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            return rt;
        }

        public static void Stretch(RectTransform rt, float inset = 0f)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(inset, inset);
            rt.offsetMax = new Vector2(-inset, -inset);
        }

        public static Image Image(Transform parent, Sprite sprite, Color color, bool raycast = false)
        {
            var img = parent.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.type = sprite != null && sprite.border != Vector4.zero ? UnityEngine.UI.Image.Type.Sliced : UnityEngine.UI.Image.Type.Simple;
            img.color = color;
            img.raycastTarget = raycast;
            return img;
        }

        public static Image Child(Transform parent, string name, Sprite sprite, Color color, float inset = 0f)
        {
            var rt = Fill(name, parent);
            Stretch(rt, inset);
            return Image(rt, sprite, color);
        }

        public static Text Label(Transform parent, string text, int size, Color color, TextAnchor anchor = TextAnchor.MiddleCenter, bool bold = false)
        {
            var go = new GameObject("Text", typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<Text>();
            t.font = Font;
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.alignment = anchor;
            t.raycastTarget = false;
            t.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            var shadow = go.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.6f);
            shadow.effectDistance = new Vector2(0f, -3f);
            Stretch((RectTransform)go.transform);
            return t;
        }

        /// <summary>A bold title with a thick dark outline.</summary>
        public static Text Title(Transform parent, string text, int size, Color color)
        {
            var t = Label(parent, text, size, color, TextAnchor.MiddleCenter, true);
            var o = t.gameObject.AddComponent<Outline>();
            o.effectColor = new Color(0.02f, 0.04f, 0.1f, 0.95f);
            o.effectDistance = new Vector2(4f, -4f);
            return t;
        }

        /// <summary>Rounded glassy panel with a thin glowing edge.</summary>
        public static RectTransform Panel(string name, Transform parent, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            var rt = Rect(name, parent, anchor, pos, size);
            Image(rt, Rounded, PanelColor, true);
            Child(rt, "Sheen", Sheen, new Color(1f, 1f, 1f, 0.5f));
            Child(rt, "Edge", RoundedOutline, PanelEdge);
            return rt;
        }

        public enum ButtonStyle { Primary, Secondary, Danger }

        /// <summary>Chunky rounded button with gloss, outline, bounce and click sound.</summary>
        public static Button Button(Transform parent, string label, Vector2 anchor, Vector2 pos, Vector2 size, ButtonStyle style, UnityEngine.Events.UnityAction onClick, int fontSize = 0)
        {
            var rt = Rect(label, parent, anchor, pos, size);
            Color fill, text, edge;
            switch (style)
            {
                case ButtonStyle.Primary: fill = Orange; text = new Color(0.12f, 0.06f, 0f); edge = Yellow; break;
                case ButtonStyle.Danger: fill = new Color(0.75f, 0.16f, 0.14f); text = Color.white; edge = new Color(1f, 0.5f, 0.4f); break;
                default: fill = new Color(0.1f, 0.2f, 0.36f, 0.95f); text = Color.white; edge = Cyan; break;
            }
            // Drop shadow
            var shadow = Rect("Shadow", rt, new Vector2(0.5f, 0.5f), new Vector2(0f, -8f), size);
            Image(shadow, Rounded, new Color(0f, 0f, 0.05f, 0.45f));
            var img = Image(rt, Rounded, fill, true);
            Child(rt, "Sheen", Sheen, Color.white);
            Child(rt, "Edge", RoundedOutline, edge);
            var t = Label(rt, label, fontSize > 0 ? fontSize : Mathf.RoundToInt(size.y * 0.38f), text, TextAnchor.MiddleCenter, true);
            if (style == ButtonStyle.Primary) t.GetComponent<Shadow>().effectColor = new Color(1f, 1f, 1f, 0.25f);

            var btn = rt.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            var c = btn.colors;
            c.normalColor = Color.white;
            c.highlightedColor = new Color(1.1f, 1.1f, 1.1f);
            c.pressedColor = new Color(0.8f, 0.8f, 0.8f);
            c.fadeDuration = 0.05f;
            btn.colors = c;
            btn.onClick.AddListener(() => GameAudio.Play2D(Sfx.UiClick));
            if (onClick != null) btn.onClick.AddListener(onClick);
            rt.gameObject.AddComponent<PressBounce>();
            shadow.SetAsFirstSibling();
            return btn;
        }

        /// <summary>Round icon button (pause, arrows).</summary>
        public static Button IconButton(Transform parent, Sprite icon, string fallbackText, Vector2 anchor, Vector2 pos, float size, UnityEngine.Events.UnityAction onClick)
        {
            var rt = Rect("IconButton", parent, anchor, pos, new Vector2(size, size));
            var img = Image(rt, Circle, new Color(0.07f, 0.12f, 0.22f, 0.85f), true);
            Child(rt, "Ring", Ring, Cyan);
            if (icon != null) Child(rt, "Icon", icon, Color.white, size * 0.24f);
            else Label(rt, fallbackText, Mathf.RoundToInt(size * 0.5f), Color.white, TextAnchor.MiddleCenter, true);
            var btn = rt.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(() => GameAudio.Play2D(Sfx.UiClick));
            if (onClick != null) btn.onClick.AddListener(onClick);
            rt.gameObject.AddComponent<PressBounce>();
            return btn;
        }

        /// <summary>Thin stat bar made of 10 segments.</summary>
        public static Image[] SegmentBar(Transform parent, Vector2 anchor, Vector2 pos, Vector2 size, Color color)
        {
            var rt = Rect("Segments", parent, anchor, pos, size);
            var segs = new Image[10];
            float w = size.x / 10f;
            for (int i = 0; i < 10; i++)
            {
                var s = Rect("Seg", rt, new Vector2(0f, 0.5f), new Vector2(w * i + w * 0.5f, 0f), new Vector2(w - 6f, size.y));
                segs[i] = Image(s, Rounded, color);
            }
            return segs;
        }

        public static void SetSegments(Image[] segs, float value, Color on)
        {
            int lit = Mathf.RoundToInt(Mathf.Clamp01(value) * segs.Length);
            for (int i = 0; i < segs.Length; i++)
                segs[i].color = i < lit ? on : new Color(1f, 1f, 1f, 0.1f);
        }

        /// <summary>Rounded progress bar; returns the fill rect (set its anchorMax.x).</summary>
        public static RectTransform Bar(Transform parent, Vector2 anchor, Vector2 pos, Vector2 size, Color color, out Image fillImage)
        {
            var bg = Rect("Bar", parent, anchor, pos, size);
            Image(bg, Rounded, new Color(0f, 0f, 0f, 0.45f));
            var fill = Fill("Fill", bg);
            fill.offsetMin = new Vector2(3f, 3f);
            fill.offsetMax = new Vector2(-3f, -3f);
            fillImage = Image(fill, Rounded, color);
            Child(fill, "Sheen", Sheen, new Color(1f, 1f, 1f, 0.8f));
            return fill;
        }
    }

    /// <summary>Squashes a button a little while it's held, for a satisfying tap.</summary>
    public class PressBounce : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        float target = 1f;
        float scale = 1f;

        public void OnPointerDown(PointerEventData e) => target = 0.92f;
        public void OnPointerUp(PointerEventData e) => target = 1f;
        public void OnPointerExit(PointerEventData e) => target = 1f;

        void OnDisable()
        {
            target = scale = 1f;
            transform.localScale = Vector3.one;
        }

        void Update()
        {
            if (Mathf.Approximately(scale, target)) return;
            scale = Mathf.MoveTowards(scale, target, Time.unscaledDeltaTime * 2.5f);
            transform.localScale = new Vector3(scale, scale, 1f);
        }
    }

    /// <summary>Simple horizontal slider: drag or tap along the track.</summary>
    public class UiSlider : MonoBehaviour, IPointerDownHandler, IDragHandler
    {
        public RectTransform track;
        public RectTransform fill;
        public RectTransform knob;
        public float min = 0f, max = 1f;
        public System.Action<float> onChanged;
        float value;

        public float Value
        {
            get => value;
            set
            {
                this.value = Mathf.Clamp(value, min, max);
                float t = Mathf.InverseLerp(min, max, this.value);
                fill.anchorMax = new Vector2(t, 1f);
                knob.anchorMin = knob.anchorMax = new Vector2(t, 0.5f);
                knob.anchoredPosition = Vector2.zero;
            }
        }

        public void OnPointerDown(PointerEventData e) => Set(e);
        public void OnDrag(PointerEventData e) => Set(e);

        void Set(PointerEventData e)
        {
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(track, e.position, e.pressEventCamera, out Vector2 local)) return;
            float t = Mathf.Clamp01((local.x + track.rect.width * 0.5f) / Mathf.Max(1f, track.rect.width));
            Value = Mathf.Lerp(min, max, t);
            onChanged?.Invoke(Value);
        }

        public static UiSlider Create(Transform parent, Vector2 anchor, Vector2 pos, float width, float min, float max, float value, System.Action<float> onChanged)
        {
            var area = UiKit.Rect("Slider", parent, anchor, pos, new Vector2(width, 70f));
            UiKit.Image(area, null, new Color(0f, 0f, 0f, 0f), true);
            var track = UiKit.Rect("Track", area, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(width - 40f, 16f));
            UiKit.Image(track, UiKit.Rounded, new Color(0f, 0f, 0f, 0.5f));
            var fill = UiKit.Fill("Fill", track);
            UiKit.Image(fill, UiKit.Rounded, UiKit.Cyan);
            var knob = UiKit.Rect("Knob", track, new Vector2(0f, 0.5f), Vector2.zero, new Vector2(50f, 50f));
            UiKit.Image(knob, UiKit.Circle, Color.white);
            UiKit.Child(knob, "Ring", UiKit.Ring, UiKit.Cyan);
            var s = area.gameObject.AddComponent<UiSlider>();
            s.track = track;
            s.fill = fill;
            s.knob = knob;
            s.min = min;
            s.max = max;
            s.Value = value;
            s.onChanged = onChanged;
            return s;
        }
    }
}

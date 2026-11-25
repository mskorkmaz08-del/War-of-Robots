using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RobotArena
{
    /// <summary>
    /// Shared look for every screen: a dark gunmetal "military HUD" style with cut-corner
    /// panels and buttons, amber accents, crisp thin outlines and real game fonts
    /// (Rajdhani for text, Orbitron for titles, both OFL from Google Fonts).
    /// </summary>
    public static class UiKit
    {
        // Palette: gunmetal panels, amber primary, steel-blue secondary accent.
        public static readonly Color PanelColor = new Color(0.055f, 0.065f, 0.08f, 0.9f);
        public static readonly Color PanelEdge = new Color(0.62f, 0.68f, 0.76f, 0.35f);
        public static readonly Color Cyan = new Color(0.42f, 0.78f, 0.95f);
        public static readonly Color Orange = new Color(1f, 0.64f, 0.12f);
        public static readonly Color Yellow = new Color(1f, 0.8f, 0.3f);
        public static readonly Color Red = new Color(0.95f, 0.27f, 0.2f);
        public static readonly Color Green = new Color(0.45f, 0.9f, 0.45f);
        public static readonly Color TextDim = new Color(0.68f, 0.72f, 0.78f, 0.9f);
        public static readonly Color TextBright = new Color(0.95f, 0.96f, 0.97f);
        public static readonly Color Steel = new Color(0.11f, 0.13f, 0.16f, 0.94f);

        static Font font, fontStrong, titleFont;
        static Sprite rounded, roundedOutline, sheen, circle, ring, triangle, gear, pauseIcon, chamfer, chamferOutline, vgrad, corner;

        static Font Builtin()
        {
            Font f = null;
            try { f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); } catch { }
            if (f == null) { try { f = Resources.GetBuiltinResource<Font>("Arial.ttf"); } catch { } }
            return f;
        }

        /// <summary>Body text (Rajdhani SemiBold).</summary>
        public static Font Font => font != null ? font : font = Resources.Load<Font>("Fonts/Rajdhani-SemiBold") ?? Builtin();
        /// <summary>Labels and buttons (Rajdhani Bold).</summary>
        public static Font FontStrong => fontStrong != null ? fontStrong : fontStrong = Resources.Load<Font>("Fonts/Rajdhani-Bold") ?? Font;
        /// <summary>Big titles (Orbitron Black).</summary>
        public static Font TitleFont => titleFont != null ? titleFont : titleFont = Resources.Load<Font>("Fonts/Orbitron-Black") ?? FontStrong;

        // ---------- Generated sprites ----------

        delegate float Shape(float x, float y, int size);

        static Sprite MakeSprite(string name, int size, Shape alpha, Vector4 border, float vScale = 1f)
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

        /// <summary>Signed distance to a box whose top-left and bottom-right corners are cut at 45 degrees.</summary>
        static float ChamferDist(float x, float y, int size, float cut)
        {
            float box = Mathf.Max(Mathf.Max(-x, x - size), Mathf.Max(-y, y - size));
            // Top-left cut: x + (size - y) >= cut ; bottom-right: (size - x) + y >= cut
            float tl = (cut - (x + (size - y))) * 0.7071f;
            float br = (cut - ((size - x) + y)) * 0.7071f;
            return Mathf.Max(box, Mathf.Max(tl, br));
        }

        /// <summary>Small-radius rounded box (bars, segments, chips).</summary>
        public static Sprite Rounded => rounded != null ? rounded : rounded =
            MakeSprite("Rounded", 48, (x, y, s) => 0.5f - RoundRectDist(x, y, s, 6f), new Vector4(10, 10, 10, 10));

        public static Sprite RoundedOutline => roundedOutline != null ? roundedOutline : roundedOutline =
            MakeSprite("RoundedOutline", 48, (x, y, s) =>
            {
                float d = RoundRectDist(x, y, s, 6f);
                return Mathf.Min(0.5f - d, d + 2.5f);
            }, new Vector4(10, 10, 10, 10));

        /// <summary>Cut-corner plate used by panels and buttons.</summary>
        public static Sprite Chamfer => chamfer != null ? chamfer : chamfer =
            MakeSprite("Chamfer", 96, (x, y, s) => 0.5f - ChamferDist(x, y, s, 28f), new Vector4(32, 32, 32, 32));

        /// <summary>Thin 2px outline of the cut-corner plate.</summary>
        public static Sprite ChamferOutline => chamferOutline != null ? chamferOutline : chamferOutline =
            MakeSprite("ChamferOutline", 96, (x, y, s) =>
            {
                float d = ChamferDist(x, y, s, 28f);
                return Mathf.Min(0.5f - d, d + 2.5f);
            }, new Vector4(32, 32, 32, 32));

        /// <summary>Faint lighter band along the top edge (replaces the old glossy blob).</summary>
        public static Sprite Sheen => sheen != null ? sheen : sheen =
            MakeSprite("Sheen", 96, (x, y, s) => Mathf.Clamp01(0.5f - ChamferDist(x, y, s, 28f)) * Mathf.Clamp01((y / s - 0.5f) * 2f) * 0.1f, new Vector4(32, 32, 32, 32));

        /// <summary>Vertical fade: opaque at the bottom, clear at the top.</summary>
        public static Sprite VGrad => vgrad != null ? vgrad : vgrad =
            MakeSprite("VGrad", 64, (x, y, s) => 1f - y / s, Vector4.zero);

        /// <summary>L-shaped corner bracket (decorates panels and the crosshair).</summary>
        public static Sprite Corner => corner != null ? corner : corner =
            MakeSprite("Corner", 64, (x, y, s) => (x < 6f || y > s - 6f) && x < s * 0.75f + 0.5f && y > s * 0.25f ? 1f : 0f, Vector4.zero);

        public static Sprite Circle => circle != null ? circle : circle =
            MakeSprite("Circle", 128, (x, y, s) => s * 0.5f - Vector2.Distance(new Vector2(x, y), Vector2.one * s * 0.5f), Vector4.zero);

        public static Sprite Ring => ring != null ? ring : ring =
            MakeSprite("Ring", 128, (x, y, s) =>
            {
                float d = Vector2.Distance(new Vector2(x, y), Vector2.one * s * 0.5f);
                return Mathf.Min(s * 0.5f - d, d - (s * 0.5f - 4f));
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
                bool bar = (u > 0.28f && u < 0.42f) || (u > 0.58f && u < 0.72f);
                return bar && v > 0.22f && v < 0.78f ? 1f : 0f;
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

        /// <summary>Spreads capitals out ("RADAR" -> "R A D A R") for small captions.</summary>
        public static string Spaced(string s) => string.Join(" ", s.ToCharArray());

        public static Text Label(Transform parent, string text, int size, Color color, TextAnchor anchor = TextAnchor.MiddleCenter, bool bold = false)
        {
            var go = new GameObject("Text", typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<Text>();
            t.font = bold ? FontStrong : Font;
            t.text = text;
            // Rajdhani is a little small for its size; compensate so layouts stay the same.
            t.fontSize = Mathf.RoundToInt(size * 1.12f);
            t.color = color;
            t.alignment = anchor;
            t.raycastTarget = false;
            t.fontStyle = FontStyle.Normal;
            t.lineSpacing = 0.9f;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            var shadow = go.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.55f);
            shadow.effectDistance = new Vector2(0f, -2f);
            Stretch((RectTransform)go.transform);
            return t;
        }

        /// <summary>Wide techno title (Orbitron) with a soft drop shadow.</summary>
        public static Text Title(Transform parent, string text, int size, Color color)
        {
            var t = Label(parent, text, size, color, TextAnchor.MiddleCenter, true);
            t.font = TitleFont;
            // Orbitron is very wide: scale it so existing layouts still fit.
            t.fontSize = Mathf.RoundToInt(size * 0.74f);
            var sh = t.GetComponent<Shadow>();
            sh.effectColor = new Color(0f, 0f, 0f, 0.65f);
            sh.effectDistance = new Vector2(0f, -4f);
            return t;
        }

        /// <summary>Dark gunmetal plate with cut corners, a faint top light and a thin edge.</summary>
        public static RectTransform Panel(string name, Transform parent, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            var rt = Rect(name, parent, anchor, pos, size);
            Image(rt, Chamfer, PanelColor, true);
            Child(rt, "Sheen", Sheen, Color.white);
            Child(rt, "Edge", ChamferOutline, PanelEdge);
            // Amber corner brackets
            foreach (var (a, rot) in new[] { (new Vector2(1f, 1f), -90f), (new Vector2(0f, 0f), 90f) })
            {
                var c = Rect("Bracket", rt, a, new Vector2(a.x > 0.5f ? -22f : 22f, a.y > 0.5f ? -22f : 22f), new Vector2(36f, 36f));
                Image(c, Corner, new Color(Orange.r, Orange.g, Orange.b, 0.85f));
                c.localRotation = Quaternion.Euler(0f, 0f, rot);
            }
            return rt;
        }

        public enum ButtonStyle { Primary, Secondary, Danger }

        /// <summary>Cut-corner button: solid amber (primary), dark steel with an amber tab (secondary) or dark red.</summary>
        public static Button Button(Transform parent, string label, Vector2 anchor, Vector2 pos, Vector2 size, ButtonStyle style, UnityEngine.Events.UnityAction onClick, int fontSize = 0)
        {
            var rt = Rect(label, parent, anchor, pos, size);
            Color fill, text, edge;
            switch (style)
            {
                case ButtonStyle.Primary: fill = Orange; text = new Color(0.07f, 0.05f, 0.02f); edge = new Color(1f, 0.85f, 0.5f, 0.9f); break;
                case ButtonStyle.Danger: fill = new Color(0.42f, 0.09f, 0.08f, 0.95f); text = TextBright; edge = new Color(0.95f, 0.4f, 0.32f, 0.8f); break;
                default: fill = Steel; text = TextBright; edge = PanelEdge; break;
            }
            var img = Image(rt, Chamfer, fill, true);
            // Darker lower half gives the plate some depth.
            var shade = Child(rt, "Shade", VGrad, new Color(0f, 0f, 0f, style == ButtonStyle.Primary ? 0.18f : 0.3f), 2f);
            shade.rectTransform.anchorMax = new Vector2(1f, 0.6f);
            Child(rt, "Sheen", Sheen, Color.white);
            Child(rt, "Edge", ChamferOutline, edge);
            if (style != ButtonStyle.Primary)
            {
                // Accent tab on the left edge.
                var tab = Rect("Tab", rt, new Vector2(0f, 0.5f), new Vector2(14f, 0f), new Vector2(5f, size.y * 0.46f));
                Image(tab, null, style == ButtonStyle.Danger ? Red : Orange);
            }
            var t = Label(rt, label, fontSize > 0 ? fontSize : Mathf.RoundToInt(size.y * 0.38f), text, TextAnchor.MiddleCenter, true);
            if (style == ButtonStyle.Primary) t.GetComponent<Shadow>().effectColor = new Color(1f, 1f, 1f, 0.2f);

            var btn = rt.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            var c = btn.colors;
            c.normalColor = Color.white;
            c.highlightedColor = new Color(1.12f, 1.12f, 1.12f);
            c.pressedColor = new Color(0.78f, 0.78f, 0.78f);
            c.fadeDuration = 0.06f;
            btn.colors = c;
            btn.onClick.AddListener(() => GameAudio.Play2D(Sfx.UiClick));
            if (onClick != null) btn.onClick.AddListener(onClick);
            rt.gameObject.AddComponent<PressBounce>();
            return btn;
        }

        /// <summary>Square cut-corner icon button (pause, arrows).</summary>
        public static Button IconButton(Transform parent, Sprite icon, string fallbackText, Vector2 anchor, Vector2 pos, float size, UnityEngine.Events.UnityAction onClick)
        {
            var rt = Rect("IconButton", parent, anchor, pos, new Vector2(size, size));
            var img = Image(rt, Chamfer, Steel, true);
            Child(rt, "Sheen", Sheen, Color.white);
            Child(rt, "Edge", ChamferOutline, PanelEdge);
            if (icon != null) Child(rt, "Icon", icon, TextBright, size * 0.26f);
            else Label(rt, fallbackText, Mathf.RoundToInt(size * 0.5f), TextBright, TextAnchor.MiddleCenter, true);
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
                var s = Rect("Seg", rt, new Vector2(0f, 0.5f), new Vector2(w * i + w * 0.5f, 0f), new Vector2(w - 5f, size.y * 0.6f));
                segs[i] = Image(s, null, color);
            }
            return segs;
        }

        public static void SetSegments(Image[] segs, float value, Color on)
        {
            int lit = Mathf.RoundToInt(Mathf.Clamp01(value) * segs.Length);
            for (int i = 0; i < segs.Length; i++)
                segs[i].color = i < lit ? on : new Color(1f, 1f, 1f, 0.08f);
        }

        /// <summary>Flat progress bar with a dark track; returns the fill rect (set its anchorMax.x).</summary>
        public static RectTransform Bar(Transform parent, Vector2 anchor, Vector2 pos, Vector2 size, Color color, out Image fillImage)
        {
            var bg = Rect("Bar", parent, anchor, pos, size);
            Image(bg, null, new Color(0f, 0f, 0f, 0.5f));
            Child(bg, "Edge", RoundedOutline, new Color(1f, 1f, 1f, 0.08f));
            var fill = Fill("Fill", bg);
            fill.offsetMin = new Vector2(2f, 2f);
            fill.offsetMax = new Vector2(-2f, -2f);
            fillImage = Image(fill, null, color);
            var hi = Child(fill, "Light", VGrad, new Color(1f, 1f, 1f, 0.18f));
            hi.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 180f);
            return fill;
        }
    }

    /// <summary>Squashes a button a little while it's held, for a satisfying tap.</summary>
    public class PressBounce : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        float target = 1f;
        float scale = 1f;

        public void OnPointerDown(PointerEventData e) => target = 0.95f;
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
            var track = UiKit.Rect("Track", area, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(width - 40f, 8f));
            UiKit.Image(track, null, new Color(0f, 0f, 0f, 0.55f));
            var fill = UiKit.Fill("Fill", track);
            UiKit.Image(fill, null, UiKit.Orange);
            var knob = UiKit.Rect("Knob", track, new Vector2(0f, 0.5f), Vector2.zero, new Vector2(30f, 54f));
            UiKit.Image(knob, UiKit.Chamfer, UiKit.TextBright);
            UiKit.Child(knob, "Edge", UiKit.ChamferOutline, new Color(0f, 0f, 0f, 0.4f));
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

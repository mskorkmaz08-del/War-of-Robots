using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace RobotArena
{
    /// <summary>
    /// Round radar in the corner. It turns with the camera, so "up" on the map is
    /// the way you're looking. Enemies are red dots, teammates blue, capture points rings; things out of range sit on the rim.
    /// </summary>
    public class Minimap : MonoBehaviour
    {
        public float worldRadius = 170f;
        public float arenaHalfSize = Frontier.Half;

        RectTransform rotor;
        RectTransform arenaRect;
        RectTransform playerArrow;
        float mapRadius;
        readonly Dictionary<Damageable, RectTransform> dots = new Dictionary<Damageable, RectTransform>();
        readonly List<Damageable> stale = new List<Damageable>();
        readonly Dictionary<CaptureZone, (RectTransform rt, Image img, Text label)> zones = new Dictionary<CaptureZone, (RectTransform, Image, Text)>();

        public static Minimap Create(Transform parent, Vector2 anchor, Vector2 pos, float size)
        {
            var rt = UiKit.Rect("Minimap", parent, anchor, pos, new Vector2(size, size));
            var map = rt.gameObject.AddComponent<Minimap>();
            map.Build(rt, size);
            return map;
        }

        void Build(RectTransform rt, float size)
        {
            mapRadius = size * 0.5f - 10f;
            var bg = UiKit.Image(rt, UiKit.Circle, new Color(0.03f, 0.04f, 0.05f, 0.8f));
            var mask = rt.gameObject.AddComponent<Mask>();
            mask.showMaskGraphic = true;

            rotor = UiKit.Rect("Rotor", rt, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(size, size));

            // Range rings and cross lines for a radar look.
            foreach (float k in new[] { 0.33f, 0.66f })
            {
                var r = UiKit.Rect("RangeRing", rt, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.one * size * k);
                UiKit.Image(r, UiKit.Ring, new Color(0.3f, 0.85f, 1f, 0.18f));
            }
            var h = UiKit.Rect("LineH", rt, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(size, 2f));
            UiKit.Image(h, null, new Color(0.3f, 0.85f, 1f, 0.12f));
            var v = UiKit.Rect("LineV", rt, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(2f, size));
            UiKit.Image(v, null, new Color(0.3f, 0.85f, 1f, 0.12f));

            // Arena walls as a square that turns with the view.
            float scale = mapRadius / worldRadius;
            arenaRect = UiKit.Rect("Arena", rotor, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.one * arenaHalfSize * 2f * scale);
            foreach (var (anchor, sz) in new[]
            {
                (new Vector2(0.5f, 1f), new Vector2(arenaHalfSize * 2f * scale, 4f)),
                (new Vector2(0.5f, 0f), new Vector2(arenaHalfSize * 2f * scale, 4f)),
                (new Vector2(0f, 0.5f), new Vector2(4f, arenaHalfSize * 2f * scale)),
                (new Vector2(1f, 0.5f), new Vector2(4f, arenaHalfSize * 2f * scale)),
            })
            {
                var wall = UiKit.Rect("Wall", arenaRect, anchor, Vector2.zero, sz);
                UiKit.Image(wall, null, new Color(0.6f, 0.8f, 1f, 0.5f));
            }

            var rim = UiKit.Rect("Rim", rt.parent, rt.anchorMin, rt.anchoredPosition, Vector2.one * size);
            UiKit.Image(rim, UiKit.Ring, UiKit.Cyan);
            var label = UiKit.Rect("Label", rim, new Vector2(0.5f, 0f), new Vector2(0f, -22f), new Vector2(200f, 34f));
            UiKit.Label(label, UiKit.Spaced("RADAR"), 20, UiKit.TextDim, TextAnchor.MiddleCenter, true);

            playerArrow = UiKit.Rect("Player", rt, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(34f, 40f));
            UiKit.Image(playerArrow, UiKit.Triangle, UiKit.Cyan);
        }

        void LateUpdate()
        {
            var match = MatchManager.Instance;
            var cam = FollowCamera.Main;
            if (match == null || match.Player == null || cam == null) return;

            Vector3 center = match.Player.transform.position;
            float camYaw = cam.transform.eulerAngles.y;
            float scale = mapRadius / worldRadius;

            // Rotate the world so the camera's forward points up on the map.
            rotor.localRotation = Quaternion.Euler(0f, 0f, camYaw);
            arenaRect.anchoredPosition = new Vector2(-center.x, -center.z) * scale;

            float playerYaw = match.Player.transform.eulerAngles.y;
            playerArrow.localRotation = Quaternion.Euler(0f, 0f, camYaw - playerYaw);

            // Capture points: coloured rings with their letter.
            foreach (var z in CaptureZone.All)
            {
                if (z == null) continue;
                if (!zones.TryGetValue(z, out var zr))
                {
                    var ring = UiKit.Rect("Zone", rotor, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.one * z.radius * 2f * scale);
                    ring.SetAsFirstSibling();
                    UiKit.Image(ring, UiKit.Ring, Color.white);
                    var t = UiKit.Label(ring, z.label.Length > 1 ? z.label.Substring(0, 1) : z.label, 22, Color.white, TextAnchor.MiddleCenter, true);
                    zr = (ring, ring.GetComponent<Image>(), t);
                    zones[z] = zr;
                }
                Vector3 zo = z.transform.position - center;
                zr.rt.anchoredPosition = new Vector2(zo.x, zo.z) * scale;
                Color zc = CaptureZone.TeamColor(z.Owner);
                zr.img.color = zc;
                zr.label.color = zc;
                zr.label.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -camYaw);
            }

            foreach (var d in Damageable.All)
            {
                if (d == null || d == match.Player) continue;
                bool ally = d.team == match.Player.team;
                bool building = d.GetComponent<Locomotion>() == null;
                if (!dots.TryGetValue(d, out var dot))
                {
                    dot = UiKit.Rect(ally ? "Ally" : "Enemy", rotor, new Vector2(0.5f, 0.5f), Vector2.zero, building ? new Vector2(30f, 30f) : new Vector2(ally ? 20f : 26f, ally ? 20f : 26f));
                    UiKit.Image(dot, building ? UiKit.Chamfer : UiKit.Circle, ally ? UiKit.Cyan : UiKit.Red);
                    if (!ally && !building) UiKit.Child(dot, "Ring", UiKit.Ring, new Color(1f, 0.8f, 0.7f, 0.9f));
                    dots[d] = dot;
                }
                Vector3 off = d.transform.position - center;
                Vector2 p = new Vector2(off.x, off.z) * scale;
                bool outside = p.magnitude > mapRadius - 12f;
                if (outside) p = p.normalized * (mapRadius - 12f);
                dot.anchoredPosition = p;
                float pulse = ally || building ? 1f : 1f + Mathf.Sin(Time.unscaledTime * 6f) * 0.12f;
                dot.localScale = Vector3.one * (outside ? 0.7f : pulse) * (d.IsDead ? 0.6f : 1f);
                dot.GetComponent<Image>().color = d.IsDead ? new Color(0.4f, 0.4f, 0.4f, 0.8f) : ally ? UiKit.Cyan : UiKit.Red;
            }

            // Remove dots for robots that no longer exist.
            stale.Clear();
            foreach (var kv in dots) if (kv.Key == null) stale.Add(kv.Key);
            foreach (var k in stale)
            {
                if (dots[k] != null) Destroy(dots[k].gameObject);
                dots.Remove(k);
            }
        }
    }
}

// Colour silhouettes. One shader, as many colours as anyone wants.
//
// Ported from the Green Hell mod, where the ask was stated once and settles the design here too:
// "one shader, multiple colours". A game has exactly one outline colour in its own post-process
// stack, so tinting that tints EVERY outlined thing - and a colour everything wears means nothing.
// So this draws its own, and the game keeps whatever it does.
//
// HOW, AND WHY THIS WAY
//
// A shader cannot be compiled from source at runtime in a built game. What can be used is
// Hidden/Internal-Colored, a built-in Unity always ships and never strips, which exposes exactly
// the knobs needed as MATERIAL properties: _Color, _Cull, _ZTest, _ZWrite, _SrcBlend, _DstBlend.
// That is what makes "one shader, many colours" real rather than aspirational - a new colour costs
// one material and no new rendering code.
//
// The outline itself is the inverted hull: draw the mesh again slightly larger with FRONT faces
// culled, so only its inside surface shows, and let the real object draw over the middle. What is
// left is a rim.
//
// TWO HONEST LIMITS, stated rather than discovered later:
//
//   The rim scales with the object, because the hull is grown by SCALING rather than by pushing
//   vertices along their normals - normals need a vertex shader. A log gets a fatter rim than a
//   match. Thickness is a setting for exactly that reason.
//
//   It grows about the RENDERER'S BOUNDS CENTRE, not the object's pivot. Dropped items often have
//   their pivot at one end, and scaling about a pivot swings the hull off the object entirely. That
//   one is worth knowing because it looks like a bug and is not.
//
// Marks carry a TAG so a later feature can light things in its own colour and clear its own marks
// without knowing this one exists.

using System.Collections.Generic;
using Il2Cpp;
using UnityEngine;

namespace LDPickupDoctor
{
    internal class Mark
    {
        public GameObject Go;
        public Color Colour;
        public string Tag;
        public Renderer[] Renderers;
        public Mesh[] Meshes;
        public Transform[] Owners;
    }

    internal static class Silhouette
    {
        private static readonly List<Mark> _marks = new List<Mark>();
        private static readonly Dictionary<int, Material> _materials = new Dictionary<int, Material>();
        private static Shader _shader;
        private static bool _shaderMissing;

        public static int Count { get { return _marks.Count; } }
        public static bool ShaderMissing { get { return _shaderMissing; } }

        /// <summary>
        /// Light this object in this colour until something clears it.
        ///
        /// Meshes are resolved ONCE, here, not every frame: GetComponentsInChildren allocates, and
        /// this runs per marked object per frame otherwise. Re-marking an object replaces its colour
        /// rather than stacking a second hull, which would double the rim and halve the frame rate.
        /// </summary>
        public static void Add(GameObject go, Color colour, string tag)
        {
            if (go == null) return;

            for (int i = 0; i < _marks.Count; i++)
            {
                if (_marks[i].Go == go) { _marks[i].Colour = colour; _marks[i].Tag = tag; return; }
            }

            Mark m = new Mark();
            m.Go = go;
            m.Colour = colour;
            m.Tag = tag;

            List<Renderer> rs = new List<Renderer>();
            List<Mesh> ms = new List<Mesh>();
            List<Transform> ts = new List<Transform>();
            try
            {
                Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppArrayBase<MeshFilter> filters =
                    go.GetComponentsInChildren<MeshFilter>(false);
                for (int i = 0; i < filters.Length; i++)
                {
                    MeshFilter f = filters[i];
                    if (f == null || f.sharedMesh == null) continue;
                    Renderer r = f.GetComponent<Renderer>();
                    if (r == null) continue;
                    rs.Add(r); ms.Add(f.sharedMesh); ts.Add(f.transform);
                }
                // Skinned meshes are not baked. Nothing lit here is animated - sticks, cloth, cans -
                // and baking one per frame would be the expensive thing in this whole file. If an
                // animated thing ever needs lighting, this is the line to change.
            }
            catch (System.Exception) { }

            if (rs.Count == 0) return;
            m.Renderers = rs.ToArray();
            m.Meshes = ms.ToArray();
            m.Owners = ts.ToArray();
            _marks.Add(m);
        }

        public static void Remove(GameObject go)
        {
            for (int i = _marks.Count - 1; i >= 0; i--) if (_marks[i].Go == go) _marks.RemoveAt(i);
        }

        /// <summary>Clear everything one feature put up, without touching anyone else's.</summary>
        public static void Clear(string tag)
        {
            for (int i = _marks.Count - 1; i >= 0; i--) if (_marks[i].Tag == tag) _marks.RemoveAt(i);
        }

        public static void ClearAll() { _marks.Clear(); }

        public static bool Has(GameObject go)
        {
            for (int i = 0; i < _marks.Count; i++) if (_marks[i].Go == go) return true;
            return false;
        }

        /// <summary>One material per colour, made once and kept.</summary>
        private static Material MaterialFor(Color c, bool seeThrough)
        {
            if (_shaderMissing) return null;
            if (_shader == null)
            {
                _shader = Shader.Find("Hidden/Internal-Colored");
                if (_shader == null)
                {
                    _shaderMissing = true;
                    Log.Warn("Hidden/Internal-Colored is not in this build, so coloured outlines "
                        + "cannot be drawn. Everything else in the mod is unaffected, and the "
                        + "Highlight tab will say so rather than looking broken.");
                    return null;
                }
            }

            // Keyed on the quantised colour plus the depth mode, so a hundred near-identical greens
            // do not become a hundred materials.
            int key = (Mathf.RoundToInt(Mathf.Clamp01(c.r) * 255f) << 16)
                    | (Mathf.RoundToInt(Mathf.Clamp01(c.g) * 255f) << 8)
                    | Mathf.RoundToInt(Mathf.Clamp01(c.b) * 255f);
            if (seeThrough) key |= 1 << 24;

            Material m;
            if (_materials.TryGetValue(key, out m) && m != null) return m;

            m = new Material(_shader);
            m.hideFlags = HideFlags.HideAndDontSave;
            m.SetColor("_Color", c);
            m.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Front);   // inside out: the rim
            m.SetInt("_ZWrite", 0);                                          // never occlude the item
            m.SetInt("_ZTest", (int)(seeThrough
                ? UnityEngine.Rendering.CompareFunction.Always
                : UnityEngine.Rendering.CompareFunction.LessEqual));
            m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            _materials[key] = m;
            return m;
        }

        /// <summary>
        /// Draw every mark. Called once a frame from Update, never from OnGUI.
        ///
        /// Graphics.DrawMesh is given ONE camera on purpose. Passing null queues it for every camera
        /// in the scene, and this game has several - feeding our hull back into the game's own
        /// outline and inspect passes is exactly the bug that produces a white halo on everything.
        /// </summary>
        public static void Draw()
        {
            if (_marks.Count == 0) return;

            Camera cam = null;
            try { cam = GameManager.GetMainCamera(); } catch (System.Exception) { }
            if (cam == null) cam = Camera.main;
            if (cam == null) return;

            bool seeThrough = Settings.HighlightSeeThrough.Value;
            float grow = 1f + Mathf.Clamp(Settings.HighlightThickness.Value, 0.005f, 0.5f);

            for (int i = _marks.Count - 1; i >= 0; i--)
            {
                Mark m = _marks[i];
                if (m.Go == null) { _marks.RemoveAt(i); continue; }

                Material mat = MaterialFor(m.Colour, seeThrough);
                if (mat == null) return;                 // shader missing - nothing to do, ever

                for (int j = 0; j < m.Renderers.Length; j++)
                {
                    Renderer r = m.Renderers[j];
                    if (r == null || !r.enabled || !r.gameObject.activeInHierarchy) continue;
                    Mesh mesh = m.Meshes[j];
                    if (mesh == null) continue;

                    Vector3 c = r.bounds.center;
                    Matrix4x4 about = Matrix4x4.TRS(c, Quaternion.identity, Vector3.one * grow)
                                    * Matrix4x4.TRS(-c, Quaternion.identity, Vector3.one);

                    Graphics.DrawMesh(mesh, about * m.Owners[j].localToWorldMatrix, mat,
                                      m.Owners[j].gameObject.layer, cam, 0, null, false, false, false);
                }
            }
        }
    }
}

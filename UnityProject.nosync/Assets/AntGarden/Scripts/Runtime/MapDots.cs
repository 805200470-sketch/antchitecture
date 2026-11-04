using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace AntGarden
{
    /// <summary>Round dots drawn as one mesh (the honeycomb map's doors and the dotted jumps between its nests).</summary>
    public class MapDots : MaskableGraphic
    {
        Texture tex;
        readonly List<Vector3> dots = new List<Vector3>();   // x, y, radius (reference pixels of its RectTransform)
        readonly List<Color32> colours = new List<Color32>();
        public override Texture mainTexture => tex != null ? tex : s_WhiteTexture;

        public void Init(Texture t)
        {
            tex = t;
            raycastTarget = false;
        }

        public void Add(Vector2 p, float r, Color c)
        {
            dots.Add(new Vector3(p.x, p.y, r));
            colours.Add(c);
        }

        /// <summary>All added: its rectangle round them all (a RectMask2D culls a graphic whose rectangle lies outside it).</summary>
        public void Done()
        {
            Vector2 lo = Vector2.zero, hi = Vector2.zero;
            foreach (Vector3 d in dots)
            {
                lo = Vector2.Min(lo, new Vector2(d.x - d.z, d.y - d.z));
                hi = Vector2.Max(hi, new Vector2(d.x + d.z, d.y + d.z));
            }
            float half = Mathf.Max(Mathf.Max(-lo.x, hi.x), Mathf.Max(-lo.y, hi.y)) + 2f;
            rectTransform.sizeDelta = new Vector2(half, half) * 2f;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            for (int i = 0; i < dots.Count; i++)
            {
                Vector3 d = dots[i];
                Color32 c = colours[i];
                int v = vh.currentVertCount;
                vh.AddVert(new Vector3(d.x - d.z, d.y - d.z), c, new Vector2(0f, 0f));
                vh.AddVert(new Vector3(d.x - d.z, d.y + d.z), c, new Vector2(0f, 1f));
                vh.AddVert(new Vector3(d.x + d.z, d.y + d.z), c, new Vector2(1f, 1f));
                vh.AddVert(new Vector3(d.x + d.z, d.y - d.z), c, new Vector2(1f, 0f));
                vh.AddTriangle(v, v + 1, v + 2);
                vh.AddTriangle(v + 2, v + 3, v);
            }
        }
    }
}

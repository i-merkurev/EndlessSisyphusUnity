using System.Collections.Generic;
using UnityEngine;

namespace EndlessSisyphus
{
    /// <summary>
    /// Мини-растеризатор в стиле Canvas 2D: буфер Color32[] (координаты y-вниз, как в web),
    /// набор примитивов, которые реально использует game.js, и выгрузка в Texture2D
    /// (с переворотом строк, т.к. в Unity текстуры y-вверх).
    /// </summary>
    public class PixelCanvas
    {
        public readonly int W, H;
        readonly Color32[] buf;
        Texture2D tex;

        // прямоугольный клип (по умолчанию — весь буфер)
        int clipX0, clipY0, clipX1, clipY1;

        public PixelCanvas(int w, int h)
        {
            W = w; H = h;
            buf = new Color32[w * h];
            ResetClip();
        }

        public void ResetClip() { clipX0 = 0; clipY0 = 0; clipX1 = W; clipY1 = H; }
        public void SetClip(int x0, int y0, int x1, int y1)
        {
            clipX0 = Mathf.Max(0, Mathf.Min(x0, x1));
            clipY0 = Mathf.Max(0, Mathf.Min(y0, y1));
            clipX1 = Mathf.Min(W, Mathf.Max(x0, x1));
            clipY1 = Mathf.Min(H, Mathf.Max(y0, y1));
        }

        // ---- пиксель ----
        public void Plot(int x, int y, Color32 c)
        {
            if (x < clipX0 || x >= clipX1 || y < clipY0 || y >= clipY1) return;
            buf[y * W + x] = c;
        }

        public void PlotBlend(int x, int y, Color32 c, float a)
        {
            if (a <= 0f) return;
            if (a >= 1f) { Plot(x, y, c); return; }
            if (x < clipX0 || x >= clipX1 || y < clipY0 || y >= clipY1) return;
            int i = y * W + x;
            Color32 d = buf[i];
            buf[i] = new Color32(
                (byte)(c.r * a + d.r * (1 - a)),
                (byte)(c.g * a + d.g * (1 - a)),
                (byte)(c.b * a + d.b * (1 - a)),
                255);
        }

        // ---- заливки ----
        public void Clear(Color32 c) { for (int i = 0; i < buf.Length; i++) buf[i] = c; }

        public void FillRect(int x, int y, int w, int h, Color32 c)
        {
            int xe = x + w, ye = y + h;
            for (int yy = y; yy < ye; yy++)
                for (int xx = x; xx < xe; xx++)
                    Plot(xx, yy, c);
        }

        public void FillRectF(float x, float y, float w, float h, Color32 c)
            => FillRect(Mathf.RoundToInt(x), Mathf.RoundToInt(y), Mathf.RoundToInt(w), Mathf.RoundToInt(h), c);

        public void FillRectBlend(int x, int y, int w, int h, Color32 c, float a)
        {
            int xe = x + w, ye = y + h;
            for (int yy = y; yy < ye; yy++)
                for (int xx = x; xx < xe; xx++)
                    PlotBlend(xx, yy, c, a);
        }

        public void FillCircle(float cx, float cy, float r, Color32 c, float a = 1f)
        {
            int r2 = Mathf.CeilToInt(r);
            float rr = r * r;
            for (int dy = -r2; dy <= r2; dy++)
                for (int dx = -r2; dx <= r2; dx++)
                    if (dx * dx + dy * dy <= rr)
                        PlotBlend(Mathf.RoundToInt(cx) + dx, Mathf.RoundToInt(cy) + dy, c, a);
        }

        public void FillEllipse(float cx, float cy, float rx, float ry, Color32 c, float a = 1f)
        {
            int ix = Mathf.CeilToInt(rx), iy = Mathf.CeilToInt(ry);
            for (int dy = -iy; dy <= iy; dy++)
                for (int dx = -ix; dx <= ix; dx++)
                {
                    float nx = dx / Mathf.Max(0.001f, rx), ny = dy / Mathf.Max(0.001f, ry);
                    if (nx * nx + ny * ny <= 1f)
                        PlotBlend(Mathf.RoundToInt(cx) + dx, Mathf.RoundToInt(cy) + dy, c, a);
                }
        }

        // ---- линии ----
        public void Line(float x0f, float y0f, float x1f, float y1f, Color32 c, int width = 1, float a = 1f)
        {
            int x0 = Mathf.RoundToInt(x0f), y0 = Mathf.RoundToInt(y0f);
            int x1 = Mathf.RoundToInt(x1f), y1 = Mathf.RoundToInt(y1f);
            int dx = Mathf.Abs(x1 - x0), sx = x0 < x1 ? 1 : -1;
            int dy = -Mathf.Abs(y1 - y0), sy = y0 < y1 ? 1 : -1;
            int err = dx + dy;
            int half = width / 2;
            while (true)
            {
                if (width <= 1) PlotBlend(x0, y0, c, a);
                else for (int oy = -half; oy <= half; oy++) for (int ox = -half; ox <= half; ox++) PlotBlend(x0 + ox, y0 + oy, c, a);
                if (x0 == x1 && y0 == y1) break;
                int e2 = 2 * err;
                if (e2 >= dy) { err += dy; x0 += sx; }
                if (e2 <= dx) { err += dx; y0 += sy; }
            }
        }

        // ---- полигон (scanline, выпуклый/невыпуклый) ----
        static readonly List<float> xs = new List<float>(16);
        public void FillPolygon(Vector2[] pts, Color32 c, float a = 1f)
        {
            int n = pts.Length;
            if (n < 3) return;
            float minY = float.MaxValue, maxY = float.MinValue;
            for (int i = 0; i < n; i++) { minY = Mathf.Min(minY, pts[i].y); maxY = Mathf.Max(maxY, pts[i].y); }
            int y0 = Mathf.Max(clipY0, Mathf.FloorToInt(minY));
            int y1 = Mathf.Min(clipY1 - 1, Mathf.CeilToInt(maxY));
            for (int y = y0; y <= y1; y++)
            {
                float sy = y + 0.5f;
                xs.Clear();
                for (int i = 0; i < n; i++)
                {
                    Vector2 p = pts[i], q = pts[(i + 1) % n];
                    if ((p.y <= sy && q.y > sy) || (q.y <= sy && p.y > sy))
                    {
                        float t = (sy - p.y) / (q.y - p.y);
                        xs.Add(p.x + t * (q.x - p.x));
                    }
                }
                if (xs.Count < 2) continue;
                xs.Sort();
                for (int i = 0; i + 1 < xs.Count; i += 2)
                {
                    int xa = Mathf.RoundToInt(xs[i]), xb = Mathf.RoundToInt(xs[i + 1]);
                    for (int x = xa; x < xb; x++) PlotBlend(x, y, c, a);
                }
            }
        }

        public void StrokePolyline(Vector2[] pts, Color32 c, int width = 1, float a = 1f)
        {
            for (int i = 0; i + 1 < pts.Length; i++)
                Line(pts[i].x, pts[i].y, pts[i + 1].x, pts[i + 1].y, c, width, a);
        }

        /// <summary>Квадратичная кривая Безье → набор точек (порт quadraticCurveTo).</summary>
        public static Vector2[] Quadratic(Vector2 p0, Vector2 ctrl, Vector2 p1, int seg = 10)
        {
            var r = new Vector2[seg + 1];
            for (int i = 0; i <= seg; i++)
            {
                float t = i / (float)seg, u = 1 - t;
                r[i] = u * u * p0 + 2 * u * t * ctrl + t * t * p1;
            }
            return r;
        }

        // ---- выгрузка ----
        public Texture2D ToTexture()
        {
            if (tex == null)
            {
                tex = new Texture2D(W, H, TextureFormat.RGBA32, false);
                tex.filterMode = FilterMode.Point;
                tex.wrapMode = TextureWrapMode.Clamp;
            }
            // переворот по Y: буфер y-вниз → текстура y-вверх
            var flipped = new Color32[buf.Length];
            for (int y = 0; y < H; y++)
                System.Array.Copy(buf, y * W, flipped, (H - 1 - y) * W, W);
            tex.SetPixels32(flipped);
            tex.Apply(false);
            return tex;
        }
    }
}

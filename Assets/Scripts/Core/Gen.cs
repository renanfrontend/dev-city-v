using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace DevCity
{
    /// <summary>
    /// Junta muitas caixas/planos numa malha só (1 draw call por material).
    /// </summary>
    public class MeshBuilder
    {
        readonly List<Vector3> v = new List<Vector3>();
        readonly List<Vector3> n = new List<Vector3>();
        readonly List<Vector2> uv = new List<Vector2>();
        readonly List<int> t = new List<int>();

        public int VertexCount => v.Count;

        public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 normal, Vector2 uvA, Vector2 uvC)
        {
            int i = v.Count;
            v.Add(a); v.Add(b); v.Add(c); v.Add(d);
            n.Add(normal); n.Add(normal); n.Add(normal); n.Add(normal);
            uv.Add(uvA); uv.Add(new Vector2(uvC.x, uvA.y)); uv.Add(uvC); uv.Add(new Vector2(uvA.x, uvC.y));
            t.Add(i); t.Add(i + 2); t.Add(i + 1);
            t.Add(i); t.Add(i + 3); t.Add(i + 2);
        }

        /// <summary>Plano horizontal (virado para cima). UV em metros/uvSize.</summary>
        public void Flat(Vector3 center, float sx, float sz, float uvSize = 4f, float rotY = 0f)
        {
            var r = Quaternion.Euler(0, rotY, 0);
            Vector3 a = center + r * new Vector3(-sx / 2, 0, -sz / 2);
            Vector3 b = center + r * new Vector3(sx / 2, 0, -sz / 2);
            Vector3 c = center + r * new Vector3(sx / 2, 0, sz / 2);
            Vector3 d = center + r * new Vector3(-sx / 2, 0, sz / 2);
            Vector2 ua = new Vector2(a.x, a.z) / uvSize;
            Vector2 uc = new Vector2(a.x + sx, a.z + sz) / uvSize;
            // a ordem (a, d, c, b) deixa a face virada para cima
            int i = v.Count;
            v.Add(a); v.Add(b); v.Add(c); v.Add(d);
            for (int k = 0; k < 4; k++) n.Add(Vector3.up);
            uv.Add(ua); uv.Add(new Vector2(uc.x, ua.y)); uv.Add(uc); uv.Add(new Vector2(ua.x, uc.y));
            t.Add(i); t.Add(i + 2); t.Add(i + 1);
            t.Add(i); t.Add(i + 3); t.Add(i + 2);
        }

        /// <summary>Caixa alinhada (com rotação Y opcional). UV das laterais em metros/uvSize: janelas no tamanho certo.</summary>
        public void Box(Vector3 center, Vector3 size, float uvSize = 16f, float rotY = 0f, bool bottom = false)
        {
            var r = Quaternion.Euler(0, rotY, 0);
            Vector3 h = size * 0.5f;
            Vector3 P(float x, float y, float z) => center + r * new Vector3(x * h.x, y * h.y, z * h.z);
            Vector3 N(Vector3 d) => r * d;
            float sx = size.x / uvSize, sy = size.y / uvSize, sz = size.z / uvSize;
            // frente (+z)
            Quad(P(1, -1, 1), P(-1, -1, 1), P(-1, 1, 1), P(1, 1, 1), N(Vector3.forward), Vector2.zero, new Vector2(sx, sy));
            // trás (-z)
            Quad(P(-1, -1, -1), P(1, -1, -1), P(1, 1, -1), P(-1, 1, -1), N(Vector3.back), Vector2.zero, new Vector2(sx, sy));
            // direita (+x)
            Quad(P(1, -1, -1), P(1, -1, 1), P(1, 1, 1), P(1, 1, -1), N(Vector3.right), Vector2.zero, new Vector2(sz, sy));
            // esquerda (-x)
            Quad(P(-1, -1, 1), P(-1, -1, -1), P(-1, 1, -1), P(-1, 1, 1), N(Vector3.left), Vector2.zero, new Vector2(sz, sy));
            // topo: usa um pedaço de "parede" bem pequeno (fica com a cor média da fachada)
            Quad(P(-1, 1, -1), P(1, 1, -1), P(1, 1, 1), P(-1, 1, 1), N(Vector3.up), new Vector2(0.01f, 0.01f), new Vector2(0.03f, 0.03f));
            if (bottom)
                Quad(P(-1, -1, 1), P(1, -1, 1), P(1, -1, -1), P(-1, -1, -1), N(Vector3.down), Vector2.zero, new Vector2(sx, sz));
        }

        public Mesh Build(string name)
        {
            var m = new Mesh { name = name };
            if (v.Count > 65000) m.indexFormat = IndexFormat.UInt32;
            m.SetVertices(v);
            m.SetNormals(n);
            m.SetUVs(0, uv);
            m.SetTriangles(t, 0);
            m.RecalculateBounds();
            m.RecalculateTangents();
            return m;
        }

        public GameObject Create(string name, Transform parent, Material mat, bool shadows = true)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = Build(name);
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            return go;
        }
    }

    /// <summary>Atalhos para criar peças com primitivas do Unity.</summary>
    public static class Gen
    {
        public static GameObject Prim(PrimitiveType type, Transform parent, Vector3 localPos, Vector3 scale, Material mat, bool collider = false, Vector3? euler = null)
        {
            var go = GameObject.CreatePrimitive(type);
            if (!collider) Object.Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            if (euler.HasValue) go.transform.localEulerAngles = euler.Value;
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            return go;
        }

        public static GameObject Cube(Transform parent, Vector3 localPos, Vector3 scale, Material mat, bool collider = false)
            => Prim(PrimitiveType.Cube, parent, localPos, scale, mat, collider);

        public static BoxCollider BoxCol(Transform parent, Vector3 center, Vector3 size, string name = "Col")
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = center;
            var bc = go.AddComponent<BoxCollider>();
            bc.size = size;
            go.isStatic = true;
            return bc;
        }

        public static void SetLayerRecursive(GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform c in go.transform) SetLayerRecursive(c.gameObject, layer);
        }

        public static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

        public static float FlatDist(Vector3 a, Vector3 b)
        {
            float dx = a.x - b.x, dz = a.z - b.z;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }
    }

    /// <summary>Sprites de interface desenhados por código.</summary>
    public static class Sprites
    {
        static Sprite circle, ring, star, arrow, rounded, square, cone, diamond;

        public static Sprite Circle => circle ??= Make(64, (x, y) => Mathf.Clamp01((1f - D(x, y)) * 32f));
        public static Sprite Ring => ring ??= Make(64, (x, y) => { float d = D(x, y); return Mathf.Clamp01((1f - d) * 32f) * Mathf.Clamp01((d - 0.72f) * 32f); });
        public static Sprite Square => square ??= Make(8, (x, y) => 1f);
        public static Sprite Diamond => diamond ??= Make(64, (x, y) => Mathf.Clamp01((1f - (Mathf.Abs(x - 0.5f) + Mathf.Abs(y - 0.5f)) * 2f) * 24f));

        public static Sprite Arrow => arrow ??= Make(64, (x, y) =>
        {
            // seta apontando para cima (jogador no radar)
            float px = x - 0.5f, py = y;
            float halfW = 0.42f * (1f - py);
            bool inside = py > 0.08f && py < 0.95f && Mathf.Abs(px) < halfW;
            bool notch = py < 0.35f && Mathf.Abs(px) < (0.35f - py) * 0.9f;
            return inside && !notch ? 1f : 0f;
        });

        public static Sprite Star => star ??= Make(64, (x, y) =>
        {
            Vector2 p = new Vector2(x - 0.5f, y - 0.5f);
            float a = Mathf.Atan2(p.y, p.x) + Mathf.PI / 2f;
            float k = Mathf.Repeat(a, 2f * Mathf.PI / 5f) - Mathf.PI / 5f;
            float r = Mathf.Lerp(0.2f, 0.48f, Mathf.Pow(Mathf.Abs(Mathf.Cos(k * 2.5f)), 3f));
            float inner = 0.19f / Mathf.Cos(k);
            float lim = Mathf.Max(inner, r);
            return Mathf.Clamp01((lim - p.magnitude) * 40f);
        });

        /// <summary>Cone de visão da polícia no radar (vértice embaixo, abre para cima).</summary>
        public static Sprite Cone => cone ??= MakePivot(64, (x, y) =>
        {
            Vector2 p = new Vector2(x - 0.5f, y);
            float ang = Mathf.Abs(Mathf.Atan2(p.x, p.y)) * Mathf.Rad2Deg;
            if (ang > 32f || p.magnitude > 1f) return 0f;
            return 0.55f * (1f - p.magnitude);
        }, new Vector2(0.5f, 0f));

        public static Sprite Rounded => rounded ??= MakeRounded(64, 16);

        static float D(float x, float y) => Vector2.Distance(new Vector2(x, y), new Vector2(0.5f, 0.5f)) * 2f;

        static Sprite Make(int size, System.Func<float, float, float> alpha) => MakePivot(size, alpha, new Vector2(0.5f, 0.5f));

        static Sprite MakePivot(int size, System.Func<float, float, float> alpha, Vector2 pivot)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    tex.SetPixel(x, y, new Color(1, 1, 1, alpha((x + 0.5f) / size, (y + 0.5f) / size)));
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), pivot, 100f);
        }

        static Sprite MakeRounded(int size, int radius)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float cx = Mathf.Clamp(x + 0.5f, radius, size - radius);
                    float cy = Mathf.Clamp(y + 0.5f, radius, size - radius);
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(cx, cy));
                    tex.SetPixel(x, y, new Color(1, 1, 1, Mathf.Clamp01(radius - d)));
                }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
        }
    }
}

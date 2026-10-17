using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DevCity
{
    /// <summary>
    /// Cores variadas para os carros civis (desafio 1 do TUTORIAL-VEICULOS.md).
    ///
    /// Os carros da Kenney usam uma textura "paleta" (colormap.png): colunas de 32 px com
    /// degradês, e a lataria de cada carro usa UMA dessas colunas. Multiplicar uma cor por cima
    /// escurece tudo (verde × azul = quase preto); então criamos cópias da paleta em que
    /// só as colunas da lataria trocam de cor, mantendo o degradê de luz e sombra.
    /// </summary>
    public static class CarPaint
    {
        static readonly Color[] Palette =
        {
            new Color(0.80f, 0.12f, 0.12f), new Color(0.12f, 0.30f, 0.75f), new Color(0.95f, 0.95f, 0.95f),
            new Color(0.12f, 0.12f, 0.14f), new Color(0.55f, 0.57f, 0.60f), new Color(0.15f, 0.55f, 0.30f),
            new Color(0.95f, 0.60f, 0.10f), new Color(0.45f, 0.20f, 0.60f), new Color(0.30f, 0.70f, 0.85f),
            new Color(0.85f, 0.75f, 0.55f),
        };

        static readonly VehicleKind[] Civilian = { VehicleKind.Sedan, VehicleKind.Sports, VehicleKind.SUV, VehicleKind.Van };

        static Material[] painted;
        static bool ready;

        public static bool IsCivilian(VehicleKind k) => Civilian.Contains(k);

        /// <summary>Troca o material da lataria por uma versão de cor aleatória (só carros civis).</summary>
        public static void Apply(VehicleKind kind, IEnumerable<Renderer> bodyRenderers)
        {
            if (!IsCivilian(kind)) return;
            Init();
            if (painted == null || painted.Length == 0) return;
            var mat = painted[Random.Range(0, painted.Length)];
            foreach (var r in bodyRenderers)
            {
                var mats = r.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < mats.Length; i++)
                    if (mats[i] != null && mats[i].mainTexture != null && mats[i].mainTexture.name == "colormap")
                    {
                        mats[i] = mat;
                        changed = true;
                    }
                if (changed) r.sharedMaterials = mats;
            }
        }

        static void Init()
        {
            if (ready) return;
            ready = true;
            var tex = Resources.Load<Texture2D>("Veiculos/Textures/colormap");
            if (tex == null || !tex.isReadable)
            {
                Debug.LogWarning("[Veículos] colormap não está legível (Read/Write). Os carros ficam com a cor original.");
                return;
            }

            // 1) descobre qual coluna da paleta é a lataria de cada carro civil
            const int Cols = 16, Rows = 4; // a paleta tem 16 colunas x 4 faixas
            var blocks = new HashSet<Vector2Int>();
            Material baseMat = null;
            foreach (var kind in Civilian)
            {
                var prefab = Resources.Load<GameObject>("Veiculos/" + VehicleFactory.ModelName(kind));
                if (prefab == null) continue;
                var mf = prefab.GetComponentsInChildren<MeshFilter>(true).FirstOrDefault(m => m.name.ToLowerInvariant().Contains("body"));
                if (mf == null || mf.sharedMesh == null || !mf.sharedMesh.isReadable) continue;
                if (baseMat == null) baseMat = mf.GetComponent<Renderer>().sharedMaterials.FirstOrDefault(m => m != null && m.mainTexture != null && m.mainTexture.name == "colormap");
                var count = new Dictionary<Vector2Int, int>();
                foreach (var uv in mf.sharedMesh.uv)
                {
                    var b = new Vector2Int(Mathf.Clamp((int)(Mathf.Repeat(uv.x, 1f) * Cols), 0, Cols - 1), Mathf.Clamp((int)(Mathf.Repeat(uv.y, 1f) * Rows), 0, Rows - 1));
                    count[b] = count.TryGetValue(b, out var c) ? c + 1 : 1;
                }
                if (count.Count > 0) blocks.Add(count.OrderByDescending(kv => kv.Value).First().Key); // a cor mais usada = lataria
            }
            if (baseMat == null || blocks.Count == 0) return;

            // 2) cópias da paleta em meia resolução (economiza memória de vídeo), uma por cor
            int S = tex.width / 2;
            var src = tex.GetPixels32();
            var small = new Color32[S * S];
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                    small[y * S + x] = src[(y * 2) * tex.width + x * 2];
            int bw = S / Cols, bh = S / Rows;

            painted = new Material[Palette.Length];
            for (int p = 0; p < Palette.Length; p++)
            {
                var px = (Color32[])small.Clone();
                foreach (var b in blocks)
                {
                    // brilho médio da coluna, para manter o degradê relativo
                    float avg = 0f;
                    for (int y = 0; y < bh; y++)
                        for (int x = 0; x < bw; x++) avg += Lum(small[(b.y * bh + y) * S + b.x * bw + x]);
                    avg = Mathf.Max(0.05f, avg / (bw * bh));
                    for (int y = 0; y < bh; y++)
                        for (int x = 0; x < bw; x++)
                        {
                            int i = (b.y * bh + y) * S + b.x * bw + x;
                            float k = Lum(small[i]) / avg;
                            Color c = Palette[p] * Mathf.Clamp(k, 0.6f, 1.4f);
                            px[i] = new Color(Mathf.Clamp01(c.r), Mathf.Clamp01(c.g), Mathf.Clamp01(c.b), 1f);
                        }
                }
                var t = new Texture2D(S, S, TextureFormat.RGBA32, true) { name = "colormap", filterMode = tex.filterMode, wrapMode = tex.wrapMode };
                t.SetPixels32(px);
                t.Apply(true, true); // true, true = gera mipmaps e libera a cópia na memória RAM
                painted[p] = new Material(baseMat) { mainTexture = t, name = "Lataria_" + p };
            }
        }

        static float Lum(Color32 c) => (c.r * 0.3f + c.g * 0.59f + c.b * 0.11f) / 255f;
    }
}

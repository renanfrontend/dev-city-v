using System.Collections.Generic;
using UnityEngine;

namespace DevCity
{
    /// <summary>
    /// Todos os materiais e texturas do jogo, gerados por código (o projeto não depende de assets).
    /// </summary>
    public static class Mats
    {
        static Shader standard;
        static readonly Dictionary<int, Material> cache = new Dictionary<int, Material>();

        public static Material Asphalt, Sidewalk, Curb, Grass, Sand, Water, Pool, Concrete, Dirt;
        public static Material MarkWhite, MarkYellow;
        public static Material Glass, Tire, Rim, Chrome, Black, Wreck;
        public static Material HeadOn, HeadOff, TailOn, TailOff;
        public static Material SirenRed, SirenBlue, SirenOff;
        public static Material LampOn, LampOff, Corona, CoronaBlue, CoronaRed;
        public static Material Trunk, Leaves, Palm;
        public static Material[] Buildings;      // fachadas com janelas
        public static Material[] TL;             // semáforos: [eixo*3 + cor] (0 verm, 1 amarelo, 2 verde)
        public static Material ParticleMat;
        public static Texture2D SoftCircle;

        static readonly List<Material> nightEmissive = new List<Material>();
        static readonly List<Color> nightEmissiveColor = new List<Color>();

        public static void Init()
        {
            standard = Shader.Find("Standard");
            SoftCircle = MakeSoftCircle(64);

            Asphalt = Textured(Noise(128, new Color(0.17f, 0.17f, 0.19f), 0.05f, 1), 0.15f, new Vector2(1, 1));
            Sidewalk = Textured(Tiles(128, new Color(0.5f, 0.49f, 0.47f), new Color(0.4f, 0.39f, 0.37f), 4), 0.1f, Vector2.one);
            Curb = Get(new Color(0.58f, 0.57f, 0.55f));
            Grass = Textured(Noise(128, new Color(0.29f, 0.42f, 0.2f), 0.07f, 2), 0f, Vector2.one);
            Sand = Textured(Noise(128, new Color(0.86f, 0.78f, 0.58f), 0.05f, 3), 0f, Vector2.one);
            Dirt = Get(new Color(0.42f, 0.33f, 0.24f));
            Water = Get(new Color(0.1f, 0.33f, 0.45f), 0.95f);
            Pool = Get(new Color(0.25f, 0.75f, 0.85f), 0.95f);
            Concrete = Textured(Noise(64, new Color(0.44f, 0.44f, 0.45f), 0.06f, 4), 0.1f, Vector2.one);

            MarkWhite = Get(new Color(0.9f, 0.9f, 0.88f), 0.2f);
            MarkYellow = Get(new Color(0.95f, 0.75f, 0.15f), 0.2f);

            Glass = Get(new Color(0.08f, 0.1f, 0.13f), 0.95f, 0.3f);
            Tire = Get(new Color(0.06f, 0.06f, 0.06f), 0.1f);
            Rim = Get(new Color(0.65f, 0.65f, 0.68f), 0.8f, 0.9f);
            Chrome = Get(new Color(0.8f, 0.8f, 0.82f), 0.9f, 1f);
            Black = Get(new Color(0.05f, 0.05f, 0.06f), 0.3f);
            Wreck = Get(new Color(0.1f, 0.09f, 0.08f), 0.05f);

            HeadOn = Emit(new Color(1f, 0.95f, 0.8f), 2.5f);
            HeadOff = Get(new Color(0.85f, 0.85f, 0.8f), 0.9f);
            TailOn = Emit(new Color(1f, 0.05f, 0.05f), 3f);
            TailOff = Emit(new Color(0.5f, 0.02f, 0.02f), 0.6f);
            SirenRed = Emit(new Color(1f, 0.05f, 0.1f), 5f);
            SirenBlue = Emit(new Color(0.1f, 0.25f, 1f), 5f);
            SirenOff = Get(new Color(0.15f, 0.15f, 0.2f), 0.8f);

            LampOn = Emit(new Color(1f, 0.85f, 0.55f), 3f);
            LampOff = Get(new Color(0.8f, 0.8f, 0.75f), 0.6f);
            Corona = Emit(new Color(1f, 0.82f, 0.15f), 2.2f);
            CoronaBlue = Emit(new Color(0.3f, 0.6f, 1f), 2.2f);
            CoronaRed = Emit(new Color(1f, 0.25f, 0.2f), 2.2f);

            Trunk = Get(new Color(0.35f, 0.25f, 0.17f));
            Leaves = Get(new Color(0.2f, 0.4f, 0.16f));
            Palm = Get(new Color(0.23f, 0.5f, 0.2f));

            // semáforos: cada intersecção usa os mesmos 6 materiais (a cidade inteira fica sincronizada)
            TL = new Material[6];
            for (int i = 0; i < 6; i++) TL[i] = Emit(Color.black, 0f, true);

            BuildBuildingMaterials();

            ParticleMat = new Material(Shader.Find("Sprites/Default"));
            ParticleMat.mainTexture = SoftCircle;
        }

        // ---------- fábricas ----------

        public static Material Get(Color c, float smooth = 0.2f, float metal = 0f)
        {
            int key = c.GetHashCode() ^ (smooth.GetHashCode() * 31) ^ (metal.GetHashCode() * 17);
            if (cache.TryGetValue(key, out var m)) return m;
            m = new Material(standard);
            m.color = c;
            m.SetFloat("_Glossiness", smooth);
            m.SetFloat("_Metallic", metal);
            cache[key] = m;
            return m;
        }

        /// <summary>Material novo (não compartilhado) para quem precisa mudar a cor depois.</summary>
        public static Material Unique(Color c, float smooth = 0.5f, float metal = 0.2f)
        {
            var m = new Material(standard);
            m.color = c;
            m.SetFloat("_Glossiness", smooth);
            m.SetFloat("_Metallic", metal);
            return m;
        }

        public static Material Emit(Color c, float intensity, bool unique = false)
        {
            var m = new Material(standard);
            m.color = c * 0.5f;
            m.EnableKeyword("_EMISSION");
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            m.SetColor("_EmissionColor", c * intensity);
            return m;
        }

        /// <summary>Standard em modo "Fade" (transparente), usado nas coronas dos marcadores.</summary>
        public static Material Transparent(Color c, float alpha)
        {
            var m = new Material(standard);
            c.a = alpha;
            m.color = c;
            m.SetFloat("_Mode", 2f);
            m.SetOverrideTag("RenderType", "Transparent");
            m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetInt("_ZWrite", 0);
            m.DisableKeyword("_ALPHATEST_ON");
            m.EnableKeyword("_ALPHABLEND_ON");
            m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", new Color(c.r, c.g, c.b) * 0.8f);
            m.renderQueue = 3000;
            return m;
        }

        public static void SetEmission(Material m, Color c, float intensity)
        {
            m.SetColor("_EmissionColor", c * intensity);
        }

        static Material Textured(Texture2D tex, float smooth, Vector2 tiling)
        {
            var m = new Material(standard);
            m.mainTexture = tex;
            m.mainTextureScale = tiling;
            m.SetFloat("_Glossiness", smooth);
            return m;
        }

        // ---------- prédios: fachada com janelas + emissão noturna ----------

        static void BuildBuildingMaterials()
        {
            var specs = new (Color wall, Color glass, int style)[]
            {
                (new Color(0.32f, 0.45f, 0.55f), new Color(0.15f, 0.3f, 0.42f), 0), // vidro azul (centro)
                (new Color(0.25f, 0.42f, 0.42f), new Color(0.1f, 0.28f, 0.3f), 0),  // vidro verde
                (new Color(0.75f, 0.73f, 0.68f), new Color(0.12f, 0.14f, 0.18f), 1), // concreto claro
                (new Color(0.62f, 0.38f, 0.3f), new Color(0.1f, 0.1f, 0.12f), 1),    // tijolo
                (new Color(0.86f, 0.8f, 0.68f), new Color(0.14f, 0.15f, 0.2f), 1),   // bege
                (new Color(0.93f, 0.62f, 0.55f), new Color(0.12f, 0.16f, 0.2f), 2),  // rosa (Miami)
                (new Color(0.62f, 0.85f, 0.82f), new Color(0.12f, 0.16f, 0.2f), 2),  // menta
                (new Color(0.95f, 0.9f, 0.78f), new Color(0.12f, 0.14f, 0.18f), 2),  // creme
                (new Color(0.88f, 0.86f, 0.84f), new Color(0.15f, 0.17f, 0.2f), 2),  // branco
                (new Color(0.4f, 0.4f, 0.44f), new Color(0.1f, 0.12f, 0.15f), 1),    // cinza escuro
            };
            Buildings = new Material[specs.Length];
            for (int i = 0; i < specs.Length; i++)
            {
                var (wall, glass, style) = specs[i];
                Texture2D emission;
                var tex = WindowTexture(wall, glass, style, 100 + i, out emission);
                var m = new Material(standard);
                m.mainTexture = tex;
                m.SetFloat("_Glossiness", style == 0 ? 0.75f : 0.25f);
                m.SetFloat("_Metallic", style == 0 ? 0.35f : 0f);
                m.EnableKeyword("_EMISSION");
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
                m.SetTexture("_EmissionMap", emission);
                m.SetColor("_EmissionColor", Color.black);
                Buildings[i] = m;
                nightEmissive.Add(m);
                nightEmissiveColor.Add(new Color(1f, 0.85f, 0.6f) * 1.6f);
            }
        }

        /// <summary>Uma "célula" de 4x4 m da fachada: parede + janela. A malha repete a cada 4 m.</summary>
        static Texture2D WindowTexture(Color wall, Color glass, int style, int seed, out Texture2D emission)
        {
            const int S = 64;
            var rnd = new System.Random(seed);
            var tex = new Texture2D(S * 4, S * 4, TextureFormat.RGBA32, true);
            emission = new Texture2D(S * 4, S * 4, TextureFormat.RGBA32, true);
            tex.wrapMode = TextureWrapMode.Repeat;
            emission.wrapMode = TextureWrapMode.Repeat;
            var px = new Color[S * 4 * S * 4];
            var em = new Color[px.Length];
            for (int cy = 0; cy < 4; cy++)
                for (int cx = 0; cx < 4; cx++)
                {
                    bool lit = rnd.NextDouble() < 0.45;
                    float warm = (float)rnd.NextDouble();
                    for (int y = 0; y < S; y++)
                        for (int x = 0; x < S; x++)
                        {
                            Color c = wall;
                            bool win;
                            if (style == 0) win = y > 6 && y < S - 4;                       // faixa de vidro contínua
                            else if (style == 1) win = x > 12 && x < S - 12 && y > 14 && y < S - 10;
                            else win = x > 18 && x < S - 18 && y > 18 && y < S - 14;
                            if (win)
                            {
                                float shade = 0.85f + 0.3f * (y / (float)S);
                                c = glass * shade;
                                if (style == 0 && (x % 32) < 2) c = wall * 0.7f; // montantes
                            }
                            float n = ((x * 7 + y * 13 + cx * 31 + cy * 17) % 11) / 110f;
                            c = new Color(c.r + n, c.g + n, c.b + n, 1f);
                            int i = (cy * S + y) * S * 4 + cx * S + x;
                            px[i] = c;
                            em[i] = (win && lit) ? Color.Lerp(new Color(1f, 0.8f, 0.5f), new Color(0.75f, 0.85f, 1f), warm) : Color.black;
                        }
                }
            tex.SetPixels(px); tex.Apply(true);
            emission.SetPixels(em); emission.Apply(true);
            return tex;
        }

        public static void SetNight(float night01)
        {
            for (int i = 0; i < nightEmissive.Count; i++)
                nightEmissive[i].SetColor("_EmissionColor", nightEmissiveColor[i] * night01);
            SetEmission(LampOn, new Color(1f, 0.85f, 0.55f), Mathf.Lerp(0.3f, 4f, night01));
        }

        /// <summary>fase 0..3: 0 = NS verde, 1 = NS amarelo, 2 = LO verde, 3 = LO amarelo.</summary>
        public static void SetTrafficLights(int phase)
        {
            Color[] on = { new Color(1f, 0.1f, 0.05f), new Color(1f, 0.7f, 0.05f), new Color(0.1f, 1f, 0.35f) };
            for (int axis = 0; axis < 2; axis++)
            {
                int state; // 0 vermelho, 1 amarelo, 2 verde
                if (axis == 0) state = phase == 0 ? 2 : phase == 1 ? 1 : 0;
                else state = phase == 2 ? 2 : phase == 3 ? 1 : 0;
                for (int c = 0; c < 3; c++)
                {
                    var m = TL[axis * 3 + c];
                    bool lit = c == state;
                    m.color = lit ? on[c] * 0.6f : on[c] * 0.12f;
                    m.SetColor("_EmissionColor", lit ? on[c] * 3f : Color.black);
                }
            }
        }

        // ---------- texturas procedurais ----------

        public static Texture2D Noise(int size, Color baseColor, float amount, int seed)
        {
            var rnd = new System.Random(seed);
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, true);
            tex.wrapMode = TextureWrapMode.Repeat;
            var px = new Color[size * size];
            for (int i = 0; i < px.Length; i++)
            {
                float n = ((float)rnd.NextDouble() - 0.5f) * 2f * amount;
                px[i] = new Color(baseColor.r + n, baseColor.g + n, baseColor.b + n, 1f);
            }
            tex.SetPixels(px);
            tex.Apply(true);
            return tex;
        }

        static Texture2D Tiles(int size, Color a, Color line, int count)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, true);
            tex.wrapMode = TextureWrapMode.Repeat;
            int step = size / count;
            var rnd = new System.Random(9);
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    bool l = x % step == 0 || y % step == 0;
                    float n = ((float)rnd.NextDouble() - 0.5f) * 0.05f;
                    var c = l ? line : a;
                    tex.SetPixel(x, y, new Color(c.r + n, c.g + n, c.b + n));
                }
            tex.Apply(true);
            return tex;
        }

        static Texture2D MakeSoftCircle(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float r = size / 2f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r)) / r;
                    float a = Mathf.Clamp01(1f - d);
                    tex.SetPixel(x, y, new Color(1, 1, 1, a * a));
                }
            tex.Apply();
            return tex;
        }
    }
}

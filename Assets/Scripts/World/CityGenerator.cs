using System.Collections.Generic;
using UnityEngine;

namespace DevCity
{
    public enum PlaceType { Safehouse, Hospital, Police, GunShop, Store, Clothes, PaySpray, Garage, Bar, GangPark, Race, MissionD, MissionN, MissionG, MissionP, SafehouseJacob }

    public class Place
    {
        public PlaceType Type;
        public string Name;
        public Vector3 Door;        // ponto na calçada, em frente à entrada
        public Vector3 Inside;      // ponto dentro do terreno (balcão, vaga da oficina...)
        public Vector2Int Block;
        public string Letter;       // letra no radar
        public Color BlipColor;
    }

    /// <summary>
    /// Gera Dev City: uma grade de quadras (como Los Santos, só que bem menor), com centro de
    /// arranha-céus, prédios médios, bairro de casas, praças, praia, mar e os lugares especiais.
    /// Também guarda o "grafo de ruas" usado pelo trânsito, pela polícia e pelo GPS.
    /// </summary>
    public class CityGenerator : MonoBehaviour
    {
        public const int N = 10;            // quadras por lado
        public const float P = 64f;         // distância entre ruas
        public const float ROAD = 12f;      // largura da rua (2 faixas de 6 m)
        public const float WALK = 4f;       // largura da calçada
        public const float LANE = 3f;       // deslocamento da faixa a partir do centro da rua
        public const float CURB = 0.2f;     // altura da calçada
        public const float WATER_Z = -60f;  // abaixo disso é mar
        public const float WATER_Y = -0.6f;

        public static float Size => N * P + ROAD;

        public readonly Dictionary<PlaceType, Place> Places = new Dictionary<PlaceType, Place>();
        public Texture2D MapTexture;
        public Vector2 MapMin = new Vector2(-170f, -230f);
        public float MapSize = 1040f;
        public const int MapRes = 1024;

        readonly bool[,] park = new bool[N, N];
        readonly Dictionary<Vector2Int, PlaceType> special = new Dictionary<Vector2Int, PlaceType>();

        MeshBuilder[] bld;
        MeshBuilder pools, roofs, poles, lampHeads, curbs, walkTops, lotGrass, lotConcrete, marksW, marksY, trunks, leaves, palms, sand, darkTrim;
        MeshBuilder[] tl;
        Transform colRoot;

        // ===================== grafo de ruas =====================

        public static float RoadCenter(int i) => i * P + ROAD * 0.5f;
        public static Vector3 NodePos(Vector2Int n) => new Vector3(RoadCenter(n.x), 0f, RoadCenter(n.y));
        public static bool ValidNode(Vector2Int n) => n.x >= 0 && n.y >= 0 && n.x <= N && n.y <= N;

        static readonly Vector2Int[] dirs = { Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left };

        public List<Vector2Int> Neighbors(Vector2Int n)
        {
            var list = new List<Vector2Int>(4);
            foreach (var d in dirs) if (ValidNode(n + d)) list.Add(n + d);
            return list;
        }

        public Vector2Int NearestNode(Vector3 p)
        {
            int x = Mathf.Clamp(Mathf.RoundToInt((p.x - ROAD * 0.5f) / P), 0, N);
            int y = Mathf.Clamp(Mathf.RoundToInt((p.z - ROAD * 0.5f) / P), 0, N);
            return new Vector2Int(x, y);
        }

        /// <summary>Caminho mais curto entre dois cruzamentos (BFS na grade).</summary>
        public List<Vector2Int> FindPath(Vector2Int a, Vector2Int b)
        {
            var prev = new Dictionary<Vector2Int, Vector2Int>();
            var q = new Queue<Vector2Int>();
            q.Enqueue(a);
            prev[a] = a;
            while (q.Count > 0)
            {
                var c = q.Dequeue();
                if (c == b) break;
                foreach (var nb in Neighbors(c))
                    if (!prev.ContainsKey(nb)) { prev[nb] = c; q.Enqueue(nb); }
            }
            var path = new List<Vector2Int>();
            if (!prev.ContainsKey(b)) return path;
            for (var c = b; ; c = prev[c]) { path.Add(c); if (c == a) break; }
            path.Reverse();
            return path;
        }

        /// <summary>Lado direito de quem anda na direção dir (mão direita, como no Brasil e nos EUA).</summary>
        public static Vector3 RightOf(Vector3 dir) => Vector3.Cross(Vector3.up, dir.normalized);

        public static Vector3 LaneStart(Vector2Int from, Vector2Int to)
        {
            Vector3 a = NodePos(from), b = NodePos(to);
            Vector3 d = (b - a).normalized;
            return a + RightOf(d) * LANE + d * (ROAD * 0.5f + 1f);
        }

        public static Vector3 LaneEnd(Vector2Int from, Vector2Int to)
        {
            Vector3 a = NodePos(from), b = NodePos(to);
            Vector3 d = (b - a).normalized;
            return b + RightOf(d) * LANE - d * (ROAD * 0.5f + 1f);
        }

        /// <summary>Um ponto aleatório numa faixa, perto de 'center', entre minD e maxD de distância.</summary>
        public bool RandomLanePoint(Vector3 center, float minD, float maxD, out Vector3 pos, out Vector2Int from, out Vector2Int to)
        {
            for (int tries = 0; tries < 30; tries++)
            {
                var n = NearestNode(center + new Vector3(Random.Range(-maxD, maxD), 0, Random.Range(-maxD, maxD)));
                var nbs = Neighbors(n);
                var m = nbs[Random.Range(0, nbs.Count)];
                float t = Random.Range(0.15f, 0.85f);
                Vector3 p = Vector3.Lerp(LaneStart(n, m), LaneEnd(n, m), t);
                float d = Gen.FlatDist(p, center);
                if (d >= minD && d <= maxD) { pos = p; from = n; to = m; return true; }
            }
            pos = Vector3.zero; from = to = Vector2Int.zero;
            return false;
        }

        // ===================== calçadas (pedestres) =====================

        public static Vector3 Corner(int bx, int by, int k)
        {
            float x0 = bx * P + ROAD + 2f, x1 = (bx + 1) * P - 2f;
            float z0 = by * P + ROAD + 2f, z1 = (by + 1) * P - 2f;
            switch (k)
            {
                case 0: return new Vector3(x0, CURB, z0);
                case 1: return new Vector3(x1, CURB, z0);
                case 2: return new Vector3(x1, CURB, z1);
                default: return new Vector3(x0, CURB, z1);
            }
        }

        /// <summary>Próximos cantos possíveis: andar pela calçada ou atravessar a rua.</summary>
        public static List<Vector3Int> CornerNeighbors(Vector3Int c)
        {
            int bx = c.x, by = c.y, k = c.z;
            var l = new List<Vector3Int> { new Vector3Int(bx, by, (k + 1) % 4), new Vector3Int(bx, by, (k + 3) % 4) };
            void Add(int x, int y, int kk) { if (x >= 0 && y >= 0 && x < N && y < N) l.Add(new Vector3Int(x, y, kk)); }
            switch (k)
            {
                case 0: Add(bx - 1, by, 1); Add(bx, by - 1, 3); break;
                case 1: Add(bx + 1, by, 0); Add(bx, by - 1, 2); break;
                case 2: Add(bx + 1, by, 3); Add(bx, by + 1, 1); break;
                case 3: Add(bx - 1, by, 2); Add(bx, by + 1, 0); break;
            }
            return l;
        }

        public static Vector3Int NearestCorner(Vector3 p)
        {
            int bx = Mathf.Clamp(Mathf.FloorToInt(p.x / P), 0, N - 1);
            int by = Mathf.Clamp(Mathf.FloorToInt(p.z / P), 0, N - 1);
            int best = 0; float bd = float.MaxValue;
            for (int k = 0; k < 4; k++)
            {
                float d = Gen.FlatDist(Corner(bx, by, k), p);
                if (d < bd) { bd = d; best = k; }
            }
            return new Vector3Int(bx, by, best);
        }

        // ===================== consultas =====================

        public bool IsWater(Vector3 p) => p.z < WATER_Z;

        public bool InsideCity(Vector3 p) => p.x > 0 && p.z > 0 && p.x < Size && p.z < Size;

        public bool OnRoad(Vector3 p)
        {
            if (!InsideCity(p)) return false;
            return Mathf.Repeat(p.x, P) < ROAD || Mathf.Repeat(p.z, P) < ROAD;
        }

        public string AreaName(Vector3 p)
        {
            if (p.z < WATER_Z) return "Oceano Pacífico Dev";
            if (p.z < 0) return "Praia del Código";
            if (!InsideCity(p)) return p.z > Size ? "Colinas do Norte" : "Arredores";
            float bx = p.x / P, by = p.z / P;
            float d = Vector2.Distance(new Vector2(bx, by), new Vector2(N / 2f, N / 2f));
            if (d < 2.2f) return "Centro";
            if (by < 1.5f) return "Orla";
            if (by > 8f) return "Morro do Python";
            if (bx < 3f) return "Vila Rosa";
            if (bx > 7f) return by < 4.5f ? "Porto Dev" : "Jardim Java";
            return "Bairro Novo";
        }

        public Vector3 SpawnPoint(PlaceType t) => Places[t].Door + Vector3.right * 4f + Vector3.up * 0.1f; // ao lado da porta, fora do marcador

        // ===================== construção =====================

        public void Build()
        {
            bld = new MeshBuilder[Mats.Buildings.Length];
            for (int i = 0; i < bld.Length; i++) bld[i] = new MeshBuilder();
            roofs = new MeshBuilder(); poles = new MeshBuilder(); lampHeads = new MeshBuilder();
            curbs = new MeshBuilder(); walkTops = new MeshBuilder(); lotGrass = new MeshBuilder(); lotConcrete = new MeshBuilder();
            marksW = new MeshBuilder(); marksY = new MeshBuilder(); trunks = new MeshBuilder(); leaves = new MeshBuilder();
            palms = new MeshBuilder(); sand = new MeshBuilder(); darkTrim = new MeshBuilder(); pools = new MeshBuilder();
            tl = new MeshBuilder[6];
            for (int i = 0; i < 6; i++) tl[i] = new MeshBuilder();
            colRoot = new GameObject("Colisores").transform;
            colRoot.SetParent(transform, false);

            DefineSpecialLots();
            BuildGround();
            BuildRoadMarkings();
            BuildIntersections();

            var rnd = new System.Random(2013);
            for (int by = 0; by < N; by++)
                for (int bx = 0; bx < N; bx++)
                    BuildBlock(bx, by, rnd);

            BuildBeachAndOutskirts(rnd);
            BuildBounds();

            for (int i = 0; i < bld.Length; i++)
                if (bld[i].VertexCount > 0) bld[i].Create("Predios_" + i, transform, Mats.Buildings[i]);
            roofs.Create("Telhados", transform, Mats.Concrete);
            darkTrim.Create("Detalhes", transform, Mats.Black);
            poles.Create("Postes", transform, Mats.Black);
            lampHeads.Create("Lampadas", transform, Mats.LampOn, false);
            curbs.Create("MeioFio", transform, Mats.Curb);
            walkTops.Create("Calcadas", transform, Mats.Sidewalk, false);
            lotGrass.Create("Gramados", transform, Mats.Grass, false);
            lotConcrete.Create("Pisos", transform, Mats.Concrete, false);
            marksW.Create("FaixasBrancas", transform, Mats.MarkWhite, false);
            marksY.Create("FaixasAmarelas", transform, Mats.MarkYellow, false);
            trunks.Create("Troncos", transform, Mats.Trunk);
            leaves.Create("Copas", transform, Mats.Leaves);
            palms.Create("Palmeiras", transform, Mats.Palm);
            sand.Create("Areia", transform, Mats.Sand, false);
            pools.Create("Piscinas", transform, Mats.Pool, false);
            for (int i = 0; i < 6; i++) tl[i].Create("Semaforo_" + i, transform, Mats.TL[i], false);

            BuildMapTexture();
        }

        void DefineSpecialLots()
        {
            void S(int x, int y, PlaceType t) => special[new Vector2Int(x, y)] = t;
            S(1, 7, PlaceType.Safehouse);
            S(8, 3, PlaceType.SafehouseJacob);
            S(6, 1, PlaceType.Hospital);
            S(2, 4, PlaceType.Police);
            S(7, 4, PlaceType.GunShop);
            S(4, 2, PlaceType.Store);
            S(5, 6, PlaceType.Clothes);
            S(8, 7, PlaceType.PaySpray);
            S(1, 2, PlaceType.Garage);
            S(8, 1, PlaceType.Bar);
            S(7, 8, PlaceType.GangPark);
            S(0, 5, PlaceType.Race);
            S(3, 7, PlaceType.MissionD);
            S(6, 3, PlaceType.MissionN);
            S(2, 1, PlaceType.MissionG);
            S(8, 5, PlaceType.MissionP);

            var rnd = new System.Random(77);
            for (int by = 0; by < N; by++)
                for (int bx = 0; bx < N; bx++)
                {
                    float d = Vector2.Distance(new Vector2(bx + 0.5f, by + 0.5f), new Vector2(N / 2f, N / 2f));
                    park[bx, by] = d > 2.2f && rnd.NextDouble() < 0.11;
                }
            park[7, 8] = true; // praça da gangue
            foreach (var k in special.Keys) if (special[k] != PlaceType.GangPark) park[k.x, k.y] = false;
        }

        void BuildGround()
        {
            // chão de grama enorme e o asfalto da cidade
            var g = new MeshBuilder();
            g.Flat(new Vector3(Size / 2f, -0.03f, 750f), 2200f, 1500f, 6f); // do z=0 para cima (o sul é praia/mar)
            g.Create("Grama", transform, Mats.Grass, false);
            var a = new MeshBuilder();
            a.Flat(new Vector3(Size / 2f, 0f, Size / 2f), Size, Size, 8f);
            a.Create("Asfalto", transform, Mats.Asphalt, false);

            Gen.BoxCol(colRoot, new Vector3(Size / 2f, -0.5f, (WATER_Z + 1900f) / 2f), new Vector3(2600f, 1f, 1900f - WATER_Z), "Chao");
            // fundo do mar: carros afundam até aqui
            Gen.BoxCol(colRoot, new Vector3(Size / 2f, -4.5f, WATER_Z - 600f), new Vector3(2600f, 1f, 1200f), "FundoMar");
        }

        void BuildRoadMarkings()
        {
            // faixa amarela tracejada no meio de cada trecho de rua
            for (int i = 0; i <= N; i++)
                for (int j = 0; j < N; j++)
                {
                    float c = RoadCenter(i);
                    float start = j * P + ROAD + 4f, end = (j + 1) * P - 4f;
                    for (float z = start; z < end - 1f; z += 6f)
                    {
                        marksY.Flat(new Vector3(c, 0.012f, z + 1.5f), 0.18f, 3f, 1f);   // rua vertical
                        marksY.Flat(new Vector3(z + 1.5f, 0.012f, c), 3f, 0.18f, 1f);   // rua horizontal
                    }
                    // linhas brancas da borda
                    float len = end - start + 8f, mid = (start + end) / 2f;
                    marksW.Flat(new Vector3(c - ROAD / 2f + 0.5f, 0.011f, mid), 0.15f, len, 1f);
                    marksW.Flat(new Vector3(c + ROAD / 2f - 0.5f, 0.011f, mid), 0.15f, len, 1f);
                    marksW.Flat(new Vector3(mid, 0.011f, c - ROAD / 2f + 0.5f), len, 0.15f, 1f);
                    marksW.Flat(new Vector3(mid, 0.011f, c + ROAD / 2f - 0.5f), len, 0.15f, 1f);
                }
        }

        void BuildIntersections()
        {
            for (int i = 0; i <= N; i++)
                for (int j = 0; j <= N; j++)
                {
                    Vector3 c = NodePos(new Vector2Int(i, j));
                    // faixas de pedestre nos 4 lados
                    for (int s = 0; s < 8; s++)
                    {
                        float off = -ROAD / 2f + 1f + s * 1.45f;
                        float edge = ROAD / 2f + 1.6f;
                        if (j < N) marksW.Flat(c + new Vector3(off, 0.013f, edge), 0.7f, 2.6f, 1f);
                        if (j > 0) marksW.Flat(c + new Vector3(off, 0.013f, -edge), 0.7f, 2.6f, 1f);
                        if (i < N) marksW.Flat(c + new Vector3(edge, 0.013f, off), 2.6f, 0.7f, 1f);
                        if (i > 0) marksW.Flat(c + new Vector3(-edge, 0.013f, off), 2.6f, 0.7f, 1f);
                    }
                    // semáforos em 2 esquinas opostas
                    if (i > 0 && j > 0 && i < N && j < N)
                    {
                        TrafficPole(c + new Vector3(ROAD / 2f + 0.8f, CURB, ROAD / 2f + 0.8f), 1);
                        TrafficPole(c + new Vector3(-ROAD / 2f - 0.8f, CURB, -ROAD / 2f - 0.8f), -1);
                    }
                }
        }

        void TrafficPole(Vector3 basePos, int side)
        {
            poles.Box(basePos + Vector3.up * 2.6f, new Vector3(0.18f, 5.2f, 0.18f), 4f);
            Gen.BoxCol(colRoot, basePos + Vector3.up * 1.5f, new Vector3(0.3f, 3f, 0.3f), "Semaforo");
            // cabeça para o trânsito norte-sul (eixo 0) e leste-oeste (eixo 1)
            Vector3 h0 = basePos + new Vector3(-side * 1.2f, 4.6f, 0f);
            Vector3 h1 = basePos + new Vector3(0f, 4.6f, -side * 1.2f);
            darkTrim.Box(h0, new Vector3(0.45f, 1.3f, 0.35f), 4f);
            darkTrim.Box(h1, new Vector3(0.35f, 1.3f, 0.45f), 4f);
            poles.Box(basePos + new Vector3(-side * 0.6f, 5.1f, 0), new Vector3(1.2f, 0.1f, 0.1f), 4f);
            poles.Box(basePos + new Vector3(0, 5.1f, -side * 0.6f), new Vector3(0.1f, 0.1f, 1.2f), 4f);
            for (int c = 0; c < 3; c++)
            {
                float y = 0.4f - c * 0.4f;
                tl[0 * 3 + c].Box(h0 + new Vector3(0, y, side * 0.19f), new Vector3(0.28f, 0.28f, 0.04f), 4f);
                tl[0 * 3 + c].Box(h0 + new Vector3(0, y, -side * 0.19f), new Vector3(0.28f, 0.28f, 0.04f), 4f);
                tl[1 * 3 + c].Box(h1 + new Vector3(side * 0.19f, y, 0), new Vector3(0.04f, 0.28f, 0.28f), 4f);
                tl[1 * 3 + c].Box(h1 + new Vector3(-side * 0.19f, y, 0), new Vector3(0.04f, 0.28f, 0.28f), 4f);
            }
        }

        void StreetLamp(Vector3 p, Vector3 towardRoad)
        {
            poles.Box(p + Vector3.up * 3.5f, new Vector3(0.16f, 7f, 0.16f), 4f);
            Vector3 arm = p + Vector3.up * 6.9f + towardRoad * 1f;
            poles.Box(arm, new Vector3(Mathf.Abs(towardRoad.x) * 2f + 0.1f, 0.1f, Mathf.Abs(towardRoad.z) * 2f + 0.1f), 4f);
            lampHeads.Box(p + Vector3.up * 6.8f + towardRoad * 1.9f, new Vector3(0.6f, 0.15f, 0.6f), 4f);
            Gen.BoxCol(colRoot, p + Vector3.up * 2f, new Vector3(0.25f, 4f, 0.25f), "Poste");
        }

        // ===================== quadras =====================

        void BuildBlock(int bx, int by, System.Random rnd)
        {
            float x0 = bx * P + ROAD, z0 = by * P + ROAD;
            float bs = P - ROAD; // 52
            Vector3 center = new Vector3(x0 + bs / 2f, 0f, z0 + bs / 2f);

            // calçada (laje com meio-fio)
            curbs.Box(center + Vector3.up * (CURB / 2f - 0.002f), new Vector3(bs, CURB - 0.004f, bs), 4f);
            walkTops.Flat(center + Vector3.up * CURB, bs, bs, 2f);
            Gen.BoxCol(colRoot, center + Vector3.up * (CURB / 2f), new Vector3(bs, CURB, bs), "Calcada");

            // postes de luz nos 4 cantos
            StreetLamp(new Vector3(x0 + 0.7f, CURB, z0 + 0.7f), Vector3.left);
            StreetLamp(new Vector3(x0 + bs - 0.7f, CURB, z0 + 0.7f), Vector3.back);
            StreetLamp(new Vector3(x0 + bs - 0.7f, CURB, z0 + bs - 0.7f), Vector3.right);
            StreetLamp(new Vector3(x0 + 0.7f, CURB, z0 + bs - 0.7f), Vector3.forward);

            float lx0 = x0 + WALK, lz0 = z0 + WALK, ls = bs - 2 * WALK; // terreno 44x44
            Vector3 lotC = new Vector3(lx0 + ls / 2f, CURB, lz0 + ls / 2f);
            var key = new Vector2Int(bx, by);

            if (special.TryGetValue(key, out var type))
            {
                BuildSpecial(type, bx, by, lx0, lz0, ls, rnd);
                return;
            }
            if (park[bx, by]) { BuildPark(lotC, ls, rnd); return; }

            float d = Vector2.Distance(new Vector2(bx + 0.5f, by + 0.5f), new Vector2(N / 2f, N / 2f));
            if (d < 2.2f) BuildDowntown(lx0, lz0, ls, rnd);
            else if (d < 3.7f) BuildMidrise(lx0, lz0, ls, rnd);
            else BuildHouses(lx0, lz0, ls, rnd);
        }

        void Building(Vector3 basePos, Vector3 size, int mat, System.Random rnd, bool roofDetails = true)
        {
            bld[mat].Box(basePos + Vector3.up * size.y / 2f, size, 16f);
            Gen.BoxCol(colRoot, basePos + Vector3.up * size.y / 2f, size, "Predio");
            // marquise/beiral escuro no topo
            darkTrim.Box(basePos + Vector3.up * (size.y + 0.25f), new Vector3(size.x + 0.4f, 0.5f, size.z + 0.4f), 4f);
            if (!roofDetails) return;
            int n = 1 + rnd.Next(3);
            for (int i = 0; i < n; i++)
            {
                float w = 2f + (float)rnd.NextDouble() * 3f;
                Vector3 p = basePos + new Vector3(((float)rnd.NextDouble() - 0.5f) * (size.x - w - 2f), size.y + 0.9f, ((float)rnd.NextDouble() - 0.5f) * (size.z - w - 2f));
                roofs.Box(p, new Vector3(w, 1.4f, w * 0.8f), 4f);
            }
            if (size.y > 60f) // antena nos mais altos
                poles.Box(basePos + Vector3.up * (size.y + 8f), new Vector3(0.3f, 16f, 0.3f), 4f);
        }

        void BuildDowntown(float x, float z, float s, System.Random rnd)
        {
            int[] mats = { 0, 1, 9, 0, 2 };
            if (rnd.NextDouble() < 0.55)
            {
                // torre única sobre um pódio
                lotConcrete.Flat(new Vector3(x + s / 2, CURB + 0.01f, z + s / 2), s, s, 4f);
                float w = 26f + (float)rnd.NextDouble() * 10f;
                float h = 55f + (float)rnd.NextDouble() * 85f;
                Building(new Vector3(x + s / 2, CURB, z + s / 2), new Vector3(s - 2f, 9f, s - 2f), 2, rnd, false);
                Building(new Vector3(x + s / 2, CURB + 9f, z + s / 2), new Vector3(w, h, w), mats[rnd.Next(mats.Length)], rnd);
            }
            else
            {
                lotConcrete.Flat(new Vector3(x + s / 2, CURB + 0.01f, z + s / 2), s, s, 4f);
                float h1 = 35f + (float)rnd.NextDouble() * 60f, h2 = 30f + (float)rnd.NextDouble() * 50f;
                Building(new Vector3(x + s * 0.27f, CURB, z + s / 2), new Vector3(s * 0.48f, h1, s - 4f), mats[rnd.Next(mats.Length)], rnd);
                Building(new Vector3(x + s * 0.75f, CURB, z + s / 2), new Vector3(s * 0.44f, h2, s * 0.7f), mats[rnd.Next(mats.Length)], rnd);
            }
        }

        void BuildMidrise(float x, float z, float s, System.Random rnd)
        {
            int[] mats = { 2, 3, 4, 9, 5, 6 };
            lotConcrete.Flat(new Vector3(x + s / 2, CURB + 0.01f, z + s / 2), s, s, 4f);
            float half = (s - 2f) / 2f;
            for (int i = 0; i < 2; i++)
                for (int j = 0; j < 2; j++)
                {
                    if (rnd.NextDouble() < 0.15) continue; // estacionamento vazio
                    float h = 9f + (float)rnd.NextDouble() * 26f;
                    float w = half - 1f - (float)rnd.NextDouble() * 3f;
                    Vector3 c = new Vector3(x + 1f + half * (i + 0.5f), CURB, z + 1f + half * (j + 0.5f));
                    Building(c, new Vector3(w, h, w), mats[rnd.Next(mats.Length)], rnd);
                }
        }

        void BuildHouses(float x, float z, float s, System.Random rnd)
        {
            lotGrass.Flat(new Vector3(x + s / 2, CURB + 0.01f, z + s / 2), s, s, 4f);
            float half = s / 2f;
            for (int i = 0; i < 2; i++)
                for (int j = 0; j < 2; j++)
                {
                    Vector3 c = new Vector3(x + half * (i + 0.5f), CURB, z + half * (j + 0.5f));
                    float w = 12f + (float)rnd.NextDouble() * 4f, d = 10f + (float)rnd.NextDouble() * 3f;
                    float h = rnd.NextDouble() < 0.4 ? 7f : 4.2f;
                    int mat = 5 + rnd.Next(4);
                    Building(c, new Vector3(w, h, d), mat, rnd, false);
                    roofs.Box(c + Vector3.up * (h + 0.6f), new Vector3(w + 1f, 0.8f, d + 1f), 4f);
                    // piscina no quintal (é Dev City, né)
                    if (rnd.NextDouble() < 0.35)
                    {
                        var pool = c + new Vector3(0, 0.02f, (j == 0 ? -1 : 1) * (d / 2f + 3f));
                        lotConcrete.Flat(pool + Vector3.up * 0.01f, 7f, 4.5f, 2f);
                        pools.Flat(pool + Vector3.up * 0.02f, 6f, 3.5f, 2f);
                    }
                    if (rnd.NextDouble() < 0.6) Tree(c + new Vector3((i == 0 ? -1 : 1) * (w / 2f + 2.5f), 0, 0), rnd);
                }
        }

        void BuildPark(Vector3 c, float s, System.Random rnd)
        {
            lotGrass.Flat(c + Vector3.up * 0.01f, s, s, 4f);
            lotConcrete.Flat(c + Vector3.up * 0.02f, 3f, s, 2f);
            lotConcrete.Flat(c + Vector3.up * 0.02f, s, 3f, 2f);
            // fonte no meio
            curbs.Box(c + Vector3.up * 0.4f, new Vector3(6f, 0.8f, 6f), 4f);
            Gen.BoxCol(colRoot, c + Vector3.up * 0.4f, new Vector3(6f, 0.8f, 6f), "Fonte");
            pools.Flat(c + Vector3.up * 0.81f, 5.2f, 5.2f, 2f);
            for (int i = 0; i < 9; i++)
            {
                var p = c + new Vector3(((float)rnd.NextDouble() - 0.5f) * (s - 6f), 0, ((float)rnd.NextDouble() - 0.5f) * (s - 6f));
                if (Mathf.Abs(p.x - c.x) < 3f || Mathf.Abs(p.z - c.z) < 3f) continue;
                if (rnd.NextDouble() < 0.5) Palm(p, rnd); else Tree(p, rnd);
            }
            // bancos
            for (int k = 0; k < 4; k++)
            {
                float a = k * 90f;
                var p = c + Quaternion.Euler(0, a, 0) * new Vector3(0, 0.45f, 7f) + Quaternion.Euler(0, a, 0) * Vector3.right * 2.5f;
                darkTrim.Box(p, new Vector3(Mathf.Abs(Mathf.Sin(a * Mathf.Deg2Rad)) * 0.0f + 1.8f, 0.15f, 0.6f), 4f, a);
            }
        }

        void Tree(Vector3 p, System.Random rnd)
        {
            float h = 3f + (float)rnd.NextDouble() * 2f;
            trunks.Box(p + Vector3.up * h / 2f, new Vector3(0.4f, h, 0.4f), 4f);
            float r = 2.2f + (float)rnd.NextDouble() * 1.4f;
            leaves.Box(p + Vector3.up * (h + r * 0.4f), new Vector3(r * 1.6f, r * 1.3f, r * 1.6f), 4f, (float)rnd.NextDouble() * 90f);
            leaves.Box(p + Vector3.up * (h + r * 0.9f), new Vector3(r * 1.1f, r, r * 1.1f), 4f, 45f);
            Gen.BoxCol(colRoot, p + Vector3.up * 1.5f, new Vector3(0.6f, 3f, 0.6f), "Arvore");
        }

        void Palm(Vector3 p, System.Random rnd)
        {
            float h = 7f + (float)rnd.NextDouble() * 4f;
            float lean = ((float)rnd.NextDouble() - 0.5f) * 1.2f;
            for (int i = 0; i < 6; i++)
            {
                float t0 = i / 6f;
                trunks.Box(p + new Vector3(lean * t0 * t0, h * (t0 + 1f / 12f), 0), new Vector3(0.35f, h / 6f + 0.05f, 0.35f), 4f);
            }
            Vector3 top = p + new Vector3(lean, h, 0);
            for (int k = 0; k < 7; k++)
            {
                float a = k * 360f / 7f + (float)rnd.NextDouble() * 20f;
                Vector3 dir = Quaternion.Euler(0, a, 0) * Vector3.forward;
                palms.Box(top + dir * 1.6f + Vector3.down * 0.35f, new Vector3(0.7f, 0.12f, 3.4f), 4f, a);
            }
            Gen.BoxCol(colRoot, p + Vector3.up * 1.5f, new Vector3(0.5f, 3f, 0.5f), "Palmeira");
        }

        // ===================== lugares especiais =====================

        void BuildSpecial(PlaceType type, int bx, int by, float x, float z, float s, System.Random rnd)
        {
            Vector3 lotC = new Vector3(x + s / 2f, CURB, z + s / 2f);
            Vector3 door = new Vector3(x + s / 2f, CURB, z - WALK + 1.6f); // calçada da frente (lado sul)
            var place = new Place { Type = type, Block = new Vector2Int(bx, by), Door = door, Inside = lotC };

            switch (type)
            {
                case PlaceType.Safehouse:
                    place.Name = "Casa"; place.Letter = "C"; place.BlipColor = new Color(0.3f, 0.85f, 0.4f);
                    lotGrass.Flat(lotC + Vector3.up * 0.01f, s, s, 4f);
                    Building(lotC + new Vector3(0, 0, 6f), new Vector3(20f, 7.5f, 14f), 7, rnd, false);
                    roofs.Box(lotC + new Vector3(0, 8.3f, 6f), new Vector3(21f, 0.8f, 15f), 4f);
                    lotConcrete.Flat(lotC + new Vector3(-6f, 0.02f, -8f), 6f, 12f, 2f); // entrada da garagem
                    Palm(lotC + new Vector3(8f, 0, -12f), rnd);
                    Palm(lotC + new Vector3(-14f, 0, -12f), rnd);
                    place.Inside = lotC + new Vector3(-6f, 0, -10f);
                    break;

                case PlaceType.SafehouseJacob:
                    // casa do Jacob: sobrado no Porto Dev, com quadra de basquete na frente
                    place.Name = "Casa do Jacob"; place.Letter = "J"; place.BlipColor = new Color(0.3f, 0.75f, 0.95f);
                    lotConcrete.Flat(lotC + Vector3.up * 0.01f, s, s, 4f);
                    Building(lotC + new Vector3(0, 0, 8f), new Vector3(16f, 9f, 12f), 3, rnd, false);
                    roofs.Box(lotC + new Vector3(0, 9.6f, 8f), new Vector3(17f, 0.8f, 13f), 4f);
                    marksW.Flat(lotC + new Vector3(0, 0.03f, -9f), 14f, 0.15f, 1f);
                    marksW.Flat(lotC + new Vector3(0, 0.03f, -9f), 0.15f, 10f, 1f);
                    poles.Box(lotC + new Vector3(0, 1.6f, -14.5f), new Vector3(0.15f, 3.2f, 0.15f), 4f);
                    darkTrim.Box(lotC + new Vector3(0, 3.2f, -14.2f), new Vector3(1.8f, 1.1f, 0.1f), 4f);
                    Tree(lotC + new Vector3(12f, 0, -12f), rnd);
                    place.Inside = lotC + new Vector3(0, 0, -6f);
                    break;

                case PlaceType.Hospital:
                    place.Name = "Hospital Central"; place.Letter = "H"; place.BlipColor = new Color(0.95f, 0.3f, 0.3f);
                    lotConcrete.Flat(lotC + Vector3.up * 0.01f, s, s, 4f);
                    Building(lotC + new Vector3(0, 0, 4f), new Vector3(36f, 22f, 26f), 8, rnd);
                    Sign(lotC + new Vector3(0, 16f, 4f - 13.2f), new Vector3(5f, 5f, 0.3f), Mats.Emit(Color.white, 1.5f));
                    Sign(lotC + new Vector3(0, 16f, 4f - 13.45f), new Vector3(4f, 1.2f, 0.2f), Mats.Emit(new Color(1f, 0.1f, 0.1f), 2.5f));
                    Sign(lotC + new Vector3(0, 16f, 4f - 13.45f), new Vector3(1.2f, 4f, 0.2f), Mats.Emit(new Color(1f, 0.1f, 0.1f), 2.5f));
                    break;

                case PlaceType.Police:
                    place.Name = "Delegacia DCPD"; place.Letter = "D"; place.BlipColor = new Color(0.35f, 0.55f, 1f);
                    lotConcrete.Flat(lotC + Vector3.up * 0.01f, s, s, 4f);
                    Building(lotC + new Vector3(0, 0, 6f), new Vector3(34f, 14f, 24f), 9, rnd);
                    Sign(lotC + new Vector3(0, 11f, 6f - 12.2f), new Vector3(16f, 1.6f, 0.3f), Mats.Emit(new Color(0.2f, 0.4f, 1f), 2f));
                    break;

                case PlaceType.GunShop:
                    place.Name = "Arsenal Bala Perdida"; place.Letter = "A"; place.BlipColor = new Color(0.95f, 0.35f, 0.3f);
                    ShopFront(lotC, new Vector3(22f, 7f, 16f), 3, new Color(1f, 0.15f, 0.1f), rnd);
                    break;

                case PlaceType.Store:
                    place.Name = "Mercadinho 24h"; place.Letter = "$"; place.BlipColor = new Color(0.45f, 0.95f, 0.45f);
                    ShopFront(lotC, new Vector3(18f, 5.5f, 14f), 4, new Color(0.2f, 1f, 0.35f), rnd);
                    // balcão aberto para a rua (dá para ver o atendente)
                    curbs.Box(new Vector3(lotC.x, CURB + 0.55f, z + 2.2f), new Vector3(5f, 1.1f, 0.9f), 4f);
                    Gen.BoxCol(colRoot, new Vector3(lotC.x, CURB + 0.55f, z + 2.2f), new Vector3(5f, 1.1f, 0.9f), "Balcao");
                    roofs.Box(new Vector3(lotC.x, CURB + 3.2f, z + 1.6f), new Vector3(8f, 0.2f, 3.4f), 4f);
                    place.Inside = new Vector3(lotC.x, CURB, z + 3.4f);
                    place.Door = new Vector3(lotC.x, CURB, z + 0.6f);
                    break;

                case PlaceType.Clothes:
                    place.Name = "Moda Marginal"; place.Letter = "R"; place.BlipColor = new Color(0.95f, 0.5f, 0.85f);
                    lotConcrete.Flat(lotC + Vector3.up * 0.01f, s, s, 4f);
                    Building(lotC + new Vector3(0, 0, 4f), new Vector3(36f, 12f, 30f), 5, rnd);
                    Sign(lotC + new Vector3(0, 9f, 4f - 15.2f), new Vector3(18f, 2f, 0.3f), Mats.Emit(new Color(1f, 0.3f, 0.8f), 2.2f));
                    break;

                case PlaceType.PaySpray:
                    place.Name = "Pinta & Foge"; place.Letter = "P"; place.BlipColor = new Color(0.35f, 0.7f, 1f);
                    Garage(lotC, new Color(0.2f, 0.6f, 1f), rnd);
                    place.Inside = lotC + new Vector3(0, 0, -2f);
                    break;

                case PlaceType.Garage:
                    place.Name = "Oficina do Dudu"; place.Letter = "G"; place.BlipColor = new Color(1f, 0.6f, 0.2f);
                    Garage(lotC, new Color(1f, 0.55f, 0.1f), rnd);
                    place.Inside = lotC + new Vector3(0, 0, -2f);
                    break;

                case PlaceType.Bar:
                    place.Name = "Bar do Português"; place.Letter = "B"; place.BlipColor = new Color(0.7f, 0.5f, 1f);
                    lotConcrete.Flat(lotC + Vector3.up * 0.01f, s, s, 4f);
                    Building(lotC + new Vector3(0, 0, 6f), new Vector3(24f, 6f, 18f), 3, rnd);
                    Sign(lotC + new Vector3(0, 5f, 6f - 9.2f), new Vector3(10f, 1.4f, 0.3f), Mats.Emit(new Color(0.7f, 0.2f, 1f), 3f));
                    place.Inside = lotC + new Vector3(0, 0, -12f);
                    break;

                case PlaceType.GangPark:
                    place.Name = "Praça do Leste"; place.Letter = "!"; place.BlipColor = new Color(0.95f, 0.3f, 0.3f);
                    BuildPark(lotC, s, rnd);
                    break;

                case PlaceType.Race:
                    place.Name = "Racha da Orla"; place.Letter = "C"; place.BlipColor = new Color(1f, 0.85f, 0.2f);
                    BuildHouses(x, z, s, rnd);
                    place.Door = new Vector3(RoadCenter(0) + LANE, 0f, z + s / 2f); // na rua da esquerda
                    break;

                default: // doadores de missão: prédio comum + personagem na calçada
                    var giver = MissionSystem.GiverInfo(type);
                    place.Name = giver.name; place.Letter = giver.letter; place.BlipColor = giver.color;
                    float d = Vector2.Distance(new Vector2(bx + 0.5f, by + 0.5f), new Vector2(N / 2f, N / 2f));
                    if (d < 3.7f) BuildMidrise(x, z, s, rnd); else BuildHouses(x, z, s, rnd);
                    break;
            }
            Places[type] = place;
        }

        void ShopFront(Vector3 lotC, Vector3 size, int mat, Color neon, System.Random rnd)
        {
            float s = P - ROAD - 2 * WALK;
            lotConcrete.Flat(lotC + Vector3.up * 0.01f, s, s, 4f);
            Vector3 b = lotC + new Vector3(0, 0, -s / 2f + size.z / 2f + 1f);
            Building(b, size, mat, rnd, false);
            Sign(b + new Vector3(0, size.y - 1f, -size.z / 2f - 0.2f), new Vector3(size.x * 0.7f, 1.4f, 0.3f), Mats.Emit(neon, 2.8f));
            // prédio maior atrás
            Building(lotC + new Vector3(0, 0, s / 2f - 9f), new Vector3(s - 4f, 18f, 16f), 4, rnd);
        }

        void Garage(Vector3 lotC, Color neon, System.Random rnd)
        {
            float s = P - ROAD - 2 * WALK;
            lotConcrete.Flat(lotC + Vector3.up * 0.01f, s, s, 4f);
            // formato de U aberto para a rua: o carro entra pela frente
            Building(lotC + new Vector3(-9f, 0, 2f), new Vector3(4f, 7f, 18f), 9, rnd, false);
            Building(lotC + new Vector3(9f, 0, 2f), new Vector3(4f, 7f, 18f), 9, rnd, false);
            Building(lotC + new Vector3(0, 0, 13f), new Vector3(22f, 7f, 4f), 9, rnd, false);
            roofs.Box(lotC + new Vector3(0, 7.2f, 2f), new Vector3(22f, 0.4f, 18f), 4f);
            Gen.BoxCol(colRoot, lotC + new Vector3(0, 7.2f, 2f), new Vector3(22f, 0.4f, 18f), "TetoOficina");
            Sign(lotC + new Vector3(0, 8.3f, -7f), new Vector3(12f, 1.6f, 0.3f), Mats.Emit(neon, 2.8f));
            // rampa na calçada para o carro subir
            lotConcrete.Flat(new Vector3(lotC.x, CURB + 0.02f, lotC.z - s / 2f - WALK / 2f), 8f, WALK, 2f);
        }

        void Sign(Vector3 pos, Vector3 size, Material m)
        {
            var go = Gen.Cube(transform, pos, size, m);
            go.name = "Placa";
        }

        // ===================== praia e arredores =====================

        void BuildBeachAndOutskirts(System.Random rnd)
        {
            // areia e mar ao sul
            sand.Flat(new Vector3(Size / 2f, 0.005f, WATER_Z / 2f), 2200f, -WATER_Z, 6f);
            var water = new MeshBuilder();
            water.Flat(new Vector3(Size / 2f, WATER_Y, WATER_Z - 1500f), 5000f, 3000f, 20f);
            water.Create("Mar", transform, Mats.Water, false);
            // parede invisível embaixo d'água (ninguém atravessa o oceano)
            Gen.BoxCol(colRoot, new Vector3(Size / 2f, -5f, WATER_Z - 140f), new Vector3(3000f, 20f, 4f), "LimiteMar");

            for (float x = -100f; x < Size + 100f; x += 14f + (float)rnd.NextDouble() * 10f)
                Palm(new Vector3(x, 0, -8f - (float)rnd.NextDouble() * 30f), rnd);

            // árvores e colinas fora da cidade
            for (int i = 0; i < 260; i++)
            {
                var p = new Vector3(-140f + (float)rnd.NextDouble() * (Size + 280f), 0f, -20f + (float)rnd.NextDouble() * (Size + 160f));
                if (InsideCity(p + new Vector3(10, 0, 10)) || InsideCity(p - new Vector3(10, 0, 10)) || p.z < 10f) continue;
                Tree(p, rnd);
            }
            for (int i = 0; i < 14; i++)
            {
                float a = i / 14f * Mathf.PI + 0.1f;
                var p = new Vector3(Size / 2f + Mathf.Cos(a) * 760f, -20f, Size / 2f + Mathf.Sin(a) * 640f);
                var hill = Gen.Prim(PrimitiveType.Sphere, transform, p, new Vector3(380f, 170f + (float)rnd.NextDouble() * 140f, 300f), Mats.Grass);
                hill.name = "Colina";
            }
        }

        void BuildBounds()
        {
            float m = 140f;
            Gen.BoxCol(colRoot, new Vector3(-m, 10f, Size / 2f), new Vector3(2f, 40f, Size + 600f), "Limite");
            Gen.BoxCol(colRoot, new Vector3(Size + m, 10f, Size / 2f), new Vector3(2f, 40f, Size + 600f), "Limite");
            Gen.BoxCol(colRoot, new Vector3(Size / 2f, 10f, Size + m), new Vector3(Size + 600f, 40f, 2f), "Limite");
        }

        // ===================== textura do mapa (radar) =====================

        public Vector2 WorldToMap01(Vector3 p) => new Vector2((p.x - MapMin.x) / MapSize, (p.z - MapMin.y) / MapSize);
        public Vector3 Map01ToWorld(Vector2 uv) => new Vector3(MapMin.x + uv.x * MapSize, 0f, MapMin.y + uv.y * MapSize);

        void BuildMapTexture()
        {
            MapTexture = new Texture2D(MapRes, MapRes, TextureFormat.RGBA32, false);
            MapTexture.wrapMode = TextureWrapMode.Clamp;
            var px = new Color32[MapRes * MapRes];
            Color32 water = new Color32(46, 86, 116, 255), sandC = new Color32(150, 140, 104, 255), grass = new Color32(64, 86, 56, 255);
            Color32 road = new Color32(196, 198, 200, 255), block = new Color32(58, 61, 66, 255), parkC = new Color32(72, 110, 62, 255);
            Color32 lot = new Color32(44, 47, 52, 255);
            for (int y = 0; y < MapRes; y++)
                for (int x = 0; x < MapRes; x++)
                {
                    var w = Map01ToWorld(new Vector2((x + 0.5f) / MapRes, (y + 0.5f) / MapRes));
                    Color32 c;
                    if (w.z < WATER_Z) c = water;
                    else if (w.z < 0f) c = sandC;
                    else if (!InsideCity(w)) c = grass;
                    else
                    {
                        float lx = Mathf.Repeat(w.x, P), lz = Mathf.Repeat(w.z, P);
                        if (lx < ROAD || lz < ROAD) c = road;
                        else
                        {
                            int bx = Mathf.Clamp((int)(w.x / P), 0, N - 1), by = Mathf.Clamp((int)(w.z / P), 0, N - 1);
                            bool inLot = lx > ROAD + WALK && lx < P - WALK && lz > ROAD + WALK && lz < P - WALK;
                            if (park[bx, by]) c = inLot ? parkC : block;
                            else if (special.TryGetValue(new Vector2Int(bx, by), out var t) && inLot)
                            {
                                var bc = Places.ContainsKey(t) ? Places[t].BlipColor : Color.gray;
                                c = Color32.Lerp(lot, (Color32)bc, 0.35f);
                            }
                            else c = inLot ? lot : block;
                        }
                    }
                    px[y * MapRes + x] = c;
                }
            MapTexture.SetPixels32(px);
            MapTexture.Apply();
        }
    }
}

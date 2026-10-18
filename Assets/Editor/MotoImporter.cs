using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace DevCity.EditorTools
{
    /// <summary>
    /// Prepara motos baixadas em .glb (Poly Pizza, Sketchfab, Kenney...) para o jogo.
    ///
    /// O problema: quase todo modelo grátis vem como UMA peça só, com as rodas grudadas no resto.
    /// Para a roda girar e a suspensão funcionar, ela precisa ser um objeto separado.
    ///
    /// O que esta ferramenta faz com cada .glb de Assets/Veiculos/MotosOriginais:
    ///   1. lê o arquivo .glb direto (ele é um JSON + um bloco de bytes com os vértices);
    ///   2. junta os triângulos em "ilhas" (pedaços que se tocam) — cada pneu costuma ser uma ilha;
    ///   3. acha os 2 pneus: ilhas redondas (altura ≈ comprimento), finas e encostadas no chão;
    ///   4. separa cada roda (pneu + aro + raios dentro dele) do resto da moto;
    ///   5. vira a moto para a frente (+Z), põe no chão, ajusta o tamanho e acha o banco;
    ///   6. salva um prefab em Assets/Resources/Veiculos/Motos/&lt;nome&gt;.prefab com as peças
    ///      "Carroceria", "wheel-front", "wheel-back" e "Banco".
    ///
    /// Menu: Dev City → Preparar motos (.glb). Também roda pela linha de comando:
    ///   Unity.exe -batchmode -quit -projectPath . -executeMethod DevCity.EditorTools.MotoImporter.Run
    /// </summary>
    public static class MotoImporter
    {
        const string SrcDir = "Assets/Veiculos/MotosOriginais";
        const string GenDir = "Assets/Veiculos/MotosGeradas";
        const string OutDir = "Assets/Resources/Veiculos/Motos";

        [UnityEditor.MenuItem("Dev City/Preparar motos (.glb)", false, 30)]
        public static void Run()
        {
            Directory.CreateDirectory(SrcDir);
            Directory.CreateDirectory(OutDir);
            int ok = 0;
            var files = Directory.GetFiles(SrcDir, "*.glb");
            foreach (var f in files)
            {
                try { if (Prepare(f.Replace('\\', '/'))) ok++; }
                catch (Exception e) { Debug.LogError($"[Motos] {Path.GetFileName(f)}: {e}"); }
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[Motos] Pronto: {ok} de {files.Length} moto(s) preparada(s).");
        }


        static bool Prepare(string path)
        {
            string name = Path.GetFileNameWithoutExtension(path);
            var tris = Glb.ReadTris(path, out var json, out var bin);
            if (tris.Count == 0) { Debug.LogWarning($"[Motos] {name}: sem triângulos."); return false; }

            // 5a) deita o comprimento no eixo Z (algumas motos vêm "de lado", ao longo do X)
            var bb = Glb.Bounds(tris.SelectMany(t => new[] { t.a, t.b, t.c }));
            if (bb.size.x > bb.size.z) Glb.Transform(tris, v => new Vector3(-v.z, v.y, v.x));

            // 2) ilhas: triângulos que dividem um vértice (pela posição) ficam na mesma ilha
            var islands = Glb.Islands(tris, bb.size.magnitude * 1e-4f);
            bb = Glb.Bounds(tris.SelectMany(t => new[] { t.a, t.b, t.c }));
            float H = bb.size.y;

            // 3) pneus: ilhas redondas (altura ≈ comprimento), finas e encostadas no chão
            var ib = islands.Select(isl => Glb.Bounds(isl.SelectMany(i => new[] { tris[i].a, tris[i].b, tris[i].c }))).ToList();
            var tires = Enumerable.Range(0, islands.Count).Where(i =>
            {
                var b = ib[i];
                return b.size.y > 0.2f * H && Mathf.Abs(b.size.y - b.size.z) < 0.2f * b.size.y &&
                       b.size.x < 0.75f * b.size.y && b.min.y < bb.min.y + 0.06f * H;
            }).OrderBy(i => ib[i].center.z).ToList();
            if (tires.Count < 2 || ib[tires.Last()].center.z - ib[tires[0]].center.z < 0.35f * bb.size.z)
            {
                Debug.LogWarning($"[Motos] {name}: não achei os 2 pneus separados ({tires.Count} candidato(s) em {islands.Count} ilhas). A moto fica sem rodas girando.");
                return false;
            }
            int tireA = tires[0], tireB = tires.Last(); // A = menor Z, B = maior Z

            // 4) cada roda = o pneu + tudo que cabe dentro dele (aro, raios, cubo)
            HashSet<int> WheelOf(int tire)
            {
                var tb = ib[tire];
                var set = new HashSet<int>();
                float halfX = Mathf.Max(tb.extents.x, 0.18f * tb.size.y) * 1.05f;
                for (int i = 0; i < islands.Count; i++)
                {
                    var b = ib[i];
                    bool inside = b.min.y >= tb.min.y - 0.02f * H && b.max.y <= tb.max.y + 0.02f * H &&
                                  b.min.z >= tb.min.z - 0.02f * H && b.max.z <= tb.max.z + 0.02f * H &&
                                  Mathf.Abs(b.min.x - tb.center.x) <= halfX && Mathf.Abs(b.max.x - tb.center.x) <= halfX;
                    if (inside) foreach (var t in islands[i]) set.Add(t);
                }
                return set;
            }
            var wheelA = WheelOf(tireA);
            var wheelB = WheelOf(tireB);
            Vector3 cA = ib[tireA].center, cB = ib[tireB].center;
            float rA = ib[tireA].extents.y, rB = ib[tireB].extents.y;
            float cx = (cA.x + cB.x) / 2f;

            // 5b) qual lado é a frente? O guidão (a parte mais larga lá em cima) fica na frente.
            var upper = tris.Where((t, i) => !wheelA.Contains(i) && !wheelB.Contains(i))
                            .SelectMany(t => new[] { t.a, t.b, t.c }).Where(v => v.y > bb.min.y + 0.5f * H).ToList();
            float maxW = upper.Count > 0 ? upper.Max(v => Mathf.Abs(v.x - cx)) : 0f;
            var wide = upper.Where(v => Mathf.Abs(v.x - cx) > 0.7f * maxW).ToList();
            float midZ = (cA.z + cB.z) / 2f;
            bool frontIsB = wide.Count == 0 || wide.Average(v => v.z) >= midZ;
            int front = frontIsB ? tireB : tireA;

            // 5c) origem no chão, entre as rodas; frente para +Z; tamanho real
            float length = bb.size.z;
            float target = name.Contains("scooter") ? 1.75f : 2.15f; // metros
            float s = target / length;
            Vector3 origin = new Vector3(cx, bb.min.y, midZ);
            float flip = frontIsB ? 1f : -1f;
            Vector3 Fix(Vector3 v) { v -= origin; return new Vector3(v.x * flip, v.y, v.z * flip) * s; }
            Vector3 FixN(Vector3 n) => new Vector3(n.x * flip, n.y, n.z * flip);
            for (int i = 0; i < tris.Count; i++)
            {
                var t = tris[i];
                t.a = Fix(t.a); t.b = Fix(t.b); t.c = Fix(t.c);
                t.na = FixN(t.na); t.nb = FixN(t.nb); t.nc = FixN(t.nc);
                tris[i] = t;
            }
            Vector3 frontC = Fix(frontIsB ? cB : cA), backC = Fix(frontIsB ? cA : cB);
            float frontR = (frontIsB ? rB : rA) * s, backR = (frontIsB ? rA : rB) * s;
            var frontSet = frontIsB ? wheelB : wheelA;
            var backSet = frontIsB ? wheelA : wheelB;

            // 5d) banco: o ponto mais alto da carroceria um pouco atrás do meio, no centro
            float wb = frontC.z - backC.z;
            var seatVerts = tris.Where((t, i) => !frontSet.Contains(i) && !backSet.Contains(i))
                                .SelectMany(t => new[] { t.a, t.b, t.c })
                                .Where(v => Mathf.Abs(v.x) < 0.12f && v.z > -0.3f * wb && v.z < -0.05f * wb).ToList();
            float seatTop = seatVerts.Count > 0 ? seatVerts.Max(v => v.y) : frontR * 2.6f;

            // 6) salva malhas, materiais e o prefab
            string gen = $"{GenDir}/{name}";
            Directory.CreateDirectory(gen);
            var mats = Glb.BuildMaterials(json, bin, gen);

            var root = new GameObject(name);
            var body = Glb.MakePart(root.transform, "Carroceria", Vector3.zero, tris, i => !frontSet.Contains(i) && !backSet.Contains(i), mats, gen);
            Glb.MakePart(root.transform, "wheel-front", frontC, tris, i => frontSet.Contains(i), mats, gen);
            Glb.MakePart(root.transform, "wheel-back", backC, tris, i => backSet.Contains(i), mats, gen);
            var seat = new GameObject("Banco").transform;
            seat.SetParent(root.transform, false);
            seat.localPosition = new Vector3(0f, seatTop, -0.18f * wb);

            string outPath = $"{OutDir}/{name}.prefab";
            PrefabUtility.SaveAsPrefabAsset(root, outPath);
            UnityEngine.Object.DestroyImmediate(root);
            Debug.Log(System.FormattableString.Invariant(
                $"[Motos] {name}: {tris.Count} triângulos, {islands.Count} ilhas | rodas: frente r={frontR:0.00} m em z={frontC.z:0.00}, trás r={backR:0.00} m em z={backC.z:0.00} | entre-eixos {wb:0.00} m | banco a {seatTop:0.00} m | escala {s:0.000} | frente estava em {(frontIsB ? "+Z" : "-Z")} → {outPath}"));
            return true;
        }

    }
}

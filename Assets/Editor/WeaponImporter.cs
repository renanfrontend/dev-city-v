using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace DevCity.EditorTools
{
    /// <summary>
    /// Prepara armas baixadas em .glb (ex.: Ultimate Guns Pack da Quaternius, no Poly Pizza).
    ///
    /// Para cada arquivo em Assets/Armas/Originais:
    ///   1. deita o comprimento no eixo Z e vira o CANO para a frente (+Z) — o cano é a ponta mais
    ///      "fina" (a coronha e o cabo são mais altos);
    ///   2. ajusta o tamanho real (tabela Sizes abaixo);
    ///   3. põe a origem no CABO, onde a mão segura (tabela Grips: posição ao longo da arma);
    ///   4. cria o ponto "Cano" na ponta, de onde saem o tiro e o clarão;
    ///   5. salva Assets/Resources/Armas/&lt;nome&gt;.prefab.
    ///
    /// Menu: Dev City → Preparar armas (.glb).
    /// </summary>
    public static class WeaponImporter
    {
        const string SrcDir = "Assets/Armas/Originais";
        const string GenDir = "Assets/Armas/Geradas";
        const string OutDir = "Assets/Resources/Armas";

        // comprimento real (m) por palavra no nome do arquivo
        static readonly (string key, float length)[] Sizes =
        {
            ("pistola", 0.21f), ("revolver", 0.26f), ("submetralhadora", 0.42f),
            ("escopeta", 0.78f), ("fuzil", 0.9f), ("sniper", 1.15f),
        };

        // onde fica o cabo: fração do comprimento a partir da traseira (0) até o cano (1),
        // e a altura (0 = ponto mais baixo, 1 = mais alto)
        static readonly (string key, float along, float up)[] Grips =
        {
            ("pistola", 0.22f, 0.55f), ("revolver", 0.2f, 0.5f), ("submetralhadora", 0.42f, 0.55f),
            ("escopeta", 0.3f, 0.62f), ("fuzil", 0.36f, 0.62f), ("sniper", 0.3f, 0.62f),
        };

        [UnityEditor.MenuItem("Dev City/Preparar armas (.glb)", false, 31)]
        public static void Run()
        {
            Directory.CreateDirectory(SrcDir);
            Directory.CreateDirectory(OutDir);
            int ok = 0;
            var files = Directory.GetFiles(SrcDir, "*.glb");
            foreach (var f in files)
            {
                try { Prepare(f.Replace('\\', '/')); ok++; }
                catch (System.Exception e) { Debug.LogError($"[Armas] {Path.GetFileName(f)}: {e}"); }
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[Armas] Pronto: {ok} de {files.Length} arma(s) preparada(s).");
        }

        static void Prepare(string path)
        {
            string name = Path.GetFileNameWithoutExtension(path);
            var tris = Glb.ReadTris(path, out var json, out var bin);
            var all = (System.Func<IEnumerable<Vector3>>)(() => tris.SelectMany(t => new[] { t.a, t.b, t.c }));

            // 1) comprimento no Z
            var bb = Glb.Bounds(all());
            if (bb.size.x > bb.size.z) { Glb.Transform(tris, v => new Vector3(-v.z, v.y, v.x)); bb = Glb.Bounds(all()); }
            // a altura tem que ser o Y: se a arma veio "deitada", o Y é a menor medida
            if (bb.size.y < bb.size.x * 0.6f) { Glb.Transform(tris, v => new Vector3(v.y, -v.x, v.z)); bb = Glb.Bounds(all()); }

            // cano = ponta mais fina (compara a altura dos vértices nos 12% de cada ponta)
            float L = bb.size.z;
            float Height(bool front)
            {
                var ys = all().Where(v => front ? v.z > bb.max.z - 0.12f * L : v.z < bb.min.z + 0.12f * L).Select(v => v.y).ToList();
                return ys.Count > 0 ? ys.Max() - ys.Min() : 0f;
            }
            bool flip = Height(false) < Height(true);
            if (flip) { Glb.Transform(tris, v => new Vector3(-v.x, v.y, -v.z)); bb = Glb.Bounds(all()); }

            // 2) tamanho real
            float target = Sizes.FirstOrDefault(s => name.Contains(s.key)).length;
            if (target <= 0f) target = 0.5f;
            float scale = target / L;

            // 3) cabo
            var grip = Grips.FirstOrDefault(g => name.Contains(g.key));
            if (grip.key == null) grip = ("", 0.3f, 0.6f);
            Vector3 gripPos = new Vector3(bb.center.x, bb.min.y + grip.up * bb.size.y, bb.min.z + grip.along * L);

            // 4) cano: a ponta da frente, na altura média dos vértices de lá
            var tip = all().Where(v => v.z > bb.max.z - 0.03f * L).ToList();
            Vector3 muzzle = new Vector3(bb.center.x, tip.Average(v => v.y), bb.max.z);

            Glb.MoveAndScale(tris, -gripPos, scale); // origem no cabo, tamanho real

            string gen = $"{GenDir}/{name}";
            Directory.CreateDirectory(gen);
            var mats = Glb.BuildMaterials(json, bin, gen);
            var root = new GameObject(name);
            Glb.MakePart(root.transform, "Modelo", Vector3.zero, tris, i => true, mats, gen);
            var cano = new GameObject("Cano").transform;
            cano.SetParent(root.transform, false);
            cano.localPosition = (muzzle - gripPos) * scale;
            foreach (var r in root.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            string outPath = $"{OutDir}/{name}.prefab";
            PrefabUtility.SaveAsPrefabAsset(root, outPath);
            Object.DestroyImmediate(root);
            Debug.Log(System.FormattableString.Invariant(
                $"[Armas] {name}: {tris.Count} triângulos | {target:0.00} m | cano {(flip ? "estava atrás (virei)" : "na frente")} | ponta do cano a {(muzzle - gripPos).z * scale:0.00} m do cabo → {outPath}"));
        }
    }
}

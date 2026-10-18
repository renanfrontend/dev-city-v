using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DevCity
{
    /// <summary>
    /// Coloca modelos 3D prontos (os City Kits da Kenney, em Resources/Cidade/&lt;kit&gt;) nos terrenos
    /// da cidade. O gerador da cidade continua decidindo ONDE vai cada prédio; aqui só escolhemos
    /// um modelo, ajustamos o tamanho para caber no terreno e viramos a frente para a rua.
    ///
    /// Desempenho (importante na Intel HD 4000):
    /// - todos os modelos de um kit passam a usar UM material só (a paleta colormap.png);
    /// - no fim, CombineAll junta tudo com "static batching": centenas de prédios viram poucas
    ///   chamadas de desenho para a placa de vídeo.
    /// </summary>
    public static class KitModels
    {
        public class Model
        {
            public GameObject Prefab;
            public string Kit;
            public Bounds Bounds;   // caixa do modelo (sem escala), com a origem do prefab
            public string Name => Prefab.name;
        }

        /// <summary>
        /// Para que lado fica a FRENTE (porta, vitrine) dos modelos da Kenney, em graus.
        /// 0 = +Z. Se as portas aparecerem viradas para o fundo do terreno, troque para 180.
        /// </summary>
        public const float FrontYaw = 180f;

        static readonly Dictionary<string, List<Model>> kits = new Dictionary<string, List<Model>>();
        static readonly Dictionary<string, Material> kitMaterial = new Dictionary<string, Material>();

        static List<Model> Kit(string kit)
        {
            if (kits.TryGetValue(kit, out var list)) return list;
            list = new List<Model>();
            foreach (var go in Resources.LoadAll<GameObject>("Cidade/" + kit).OrderBy(g => g.name))
            {
                var b = MeasureBounds(go);
                if (b.size.sqrMagnitude > 0f) list.Add(new Model { Prefab = go, Bounds = b, Kit = kit });
            }
            kits[kit] = list;
            return list;
        }

        /// <summary>Modelos do kit cujo nome começa com um dos prefixos (ex.: "building-skyscraper").</summary>
        public static List<Model> Find(string kit, params string[] prefixes) =>
            Kit(kit).Where(m => prefixes.Any(p => m.Name.StartsWith(p))).ToList();

        public static bool Has(string kit) => Kit(kit).Count > 0;

        static Bounds MeasureBounds(GameObject prefab)
        {
            bool first = true;
            var b = new Bounds();
            var root = prefab.transform.worldToLocalMatrix;
            foreach (var mf in prefab.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh == null) continue;
                var m = root * mf.transform.localToWorldMatrix;
                var mb = mf.sharedMesh.bounds;
                for (int i = 0; i < 8; i++)
                {
                    var c = mb.center + Vector3.Scale(mb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    var p = m.MultiplyPoint3x4(c);
                    if (first) { b = new Bounds(p, Vector3.zero); first = false; } else b.Encapsulate(p);
                }
            }
            return b;
        }

        /// <summary>
        /// Coloca um modelo com o centro da base em "ground", virado para "faceYaw" (a frente olha
        /// para esse ângulo), no maior tamanho que caiba em "footprint" (x = medida em X, y = medida em Z)
        /// sem passar de "maxHeight". Devolve a caixa ocupada (para o colisor), em coordenadas do mundo.
        /// </summary>
        public static Bounds Place(Transform parent, Model m, Vector3 ground, Vector2 footprint, float faceYaw, float maxHeight = 999f, float scaleBoost = 1f)
        {
            float yaw = faceYaw - FrontYaw;
            bool swap = Mathf.Abs(Mathf.DeltaAngle(yaw, 90f)) < 1f || Mathf.Abs(Mathf.DeltaAngle(yaw, 270f)) < 1f;
            // medidas do modelo depois de girado
            float sx = swap ? m.Bounds.size.z : m.Bounds.size.x;
            float sz = swap ? m.Bounds.size.x : m.Bounds.size.z;
            // footprint em eixos do mundo: x = ao longo de X, y = ao longo de Z
            float s = Mathf.Min(footprint.x / sx, footprint.y / sz) * scaleBoost;
            if (m.Bounds.size.y * s > maxHeight) s = maxHeight / m.Bounds.size.y;

            var rot = Quaternion.Euler(0f, yaw, 0f);
            var go = Object.Instantiate(m.Prefab, parent);
            go.name = m.Name;
            go.transform.rotation = rot;
            go.transform.localScale = Vector3.one * s;
            Vector3 c = m.Bounds.center;
            go.transform.position = ground - rot * new Vector3(c.x, 0f, c.z) * s + Vector3.up * (-m.Bounds.min.y * s);

            ShareMaterial(go, m.Kit);
            go.isStatic = true;
            foreach (Transform t in go.GetComponentsInChildren<Transform>()) t.gameObject.isStatic = true;

            return new Bounds(ground + Vector3.up * (m.Bounds.size.y * s / 2f), new Vector3(sx * s, m.Bounds.size.y * s, sz * s));
        }

        /// <summary>Todos os modelos do kit passam a usar o mesmo material (a mesma paleta).</summary>
        static void ShareMaterial(GameObject go, string kit)
        {
            foreach (var r in go.GetComponentsInChildren<Renderer>())
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    if (mats[i] == null) continue;
                    string key = kit + "/" + mats[i].name;
                    if (!kitMaterial.TryGetValue(key, out var shared)) kitMaterial[key] = shared = mats[i];
                    mats[i] = shared;
                }
                r.sharedMaterials = mats;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            }
        }

        /// <summary>
        /// Sorteia um modelo da lista que caiba no terreno numa escala entre minScale e maxScale
        /// (assim os andares não ficam gigantes nem minúsculos) e coloca. Devolve false se nenhum couber.
        /// </summary>
        public static bool PlaceFitting(Transform parent, List<Model> list, System.Random rnd, Vector3 ground, Vector2 footprint, float faceYaw,
                                        float minScale, float maxScale, out Bounds placed, float maxHeight = 999f)
        {
            placed = default;
            if (list.Count == 0) return false;
            for (int tries = 0; tries < 6; tries++)
            {
                var m = list[rnd.Next(list.Count)];
                float yaw = faceYaw - FrontYaw;
                bool swap = Mathf.Abs(Mathf.DeltaAngle(yaw, 90f)) < 1f || Mathf.Abs(Mathf.DeltaAngle(yaw, 270f)) < 1f;
                float sx = swap ? m.Bounds.size.z : m.Bounds.size.x, sz = swap ? m.Bounds.size.x : m.Bounds.size.z;
                float fit = Mathf.Min(footprint.x / sx, footprint.y / sz);
                if (fit < minScale) continue; // não cabe: tenta outro modelo
                float s = Mathf.Min(fit, maxScale) * (0.92f + (float)rnd.NextDouble() * 0.08f);
                // "Place" calcula a escala pelo footprint: passamos o footprint que dá exatamente "s"
                placed = Place(parent, m, ground, new Vector2(swap ? sz * s : sx * s, swap ? sx * s : sz * s) , faceYaw, maxHeight);
                return true;
            }
            return false;
        }

        /// <summary>Junta todos os modelos parados embaixo de "root" (static batching).</summary>
        public static void CombineAll(GameObject root) => StaticBatchingUtility.Combine(root);

        public static IEnumerable<string> KitNames => kits.Keys;
        public static List<Model> All(string kit) => Kit(kit);

        public static Model Pick(List<Model> list, System.Random rnd) => list.Count == 0 ? null : list[rnd.Next(list.Count)];
    }
}

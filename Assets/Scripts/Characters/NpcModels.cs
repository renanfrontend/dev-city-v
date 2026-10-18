using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DevCity
{
    /// <summary>
    /// Modelos 3D dos NPCs (Resources/NPCs, "Ultimate Modular Characters" da Quaternius).
    /// Para adicionar um NPC novo: coloque o .glb em Assets/Resources/NPCs, ponha o nome numa
    /// das listas abaixo e rode Dev City → Preparar NPCs (.glb).
    /// </summary>
    public static class NpcModels
    {
        public static readonly string[] Civilians =
        {
            "npc-executivo", "npc-casual", "npc-praia", "npc-moletom", "npc-operario", "npc-mochileiro", "npc-fazendeiro",
            "npc-mulher-vestido", "npc-mulher-casual", "npc-aventureira", "npc-executiva", "npc-operaria",
        };
        public static readonly string[] Police = { "npc-swat" };
        public static readonly string[] Gang = { "npc-punk", "npc-punk-mulher", "npc-moletom" };

        static RuntimeAnimatorController controller;
        static bool controllerLoaded;
        static readonly Dictionary<string, bool> exists = new Dictionary<string, bool>();

        /// <summary>O Animator Controller criado por Dev City → Preparar NPCs (.glb).</summary>
        public static RuntimeAnimatorController Controller
        {
            get
            {
                if (!controllerLoaded)
                {
                    controllerLoaded = true;
                    // -npcblocos na linha de comando: NPCs de blocos (para comparar o desempenho)
                    if (!System.Environment.GetCommandLineArgs().Contains("-npcblocos"))
                        controller = Resources.Load<RuntimeAnimatorController>("NPCs/NpcAnimator");
                }
                return controller;
            }
        }

        static readonly Dictionary<Material, Material> simple = new Dictionary<Material, Material>();

        /// <summary>
        /// Troca os materiais PBR do glTF pelo shader Standard do resto do jogo (mesma cor e textura).
        /// Fica igual à iluminação da cidade e é mais leve na Intel HD 4000. Cada material vira uma cópia só (cache).
        /// </summary>
        public static void SimplifyMaterials(Renderer[] renderers)
        {
            var standard = Shader.Find("Standard");
            if (standard == null) return;
            foreach (var r in renderers)
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    var m = mats[i];
                    if (m == null || m.shader == standard) continue;
                    if (!simple.TryGetValue(m, out var s))
                    {
                        Color c = m.HasProperty("baseColorFactor") ? m.GetColor("baseColorFactor") : m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor") : m.color;
                        Texture t = m.HasProperty("baseColorTexture") ? m.GetTexture("baseColorTexture") : m.HasProperty("_BaseMap") ? m.GetTexture("_BaseMap") : m.mainTexture;
                        s = new Material(standard) { name = m.name + " (simples)", color = c, mainTexture = t };
                        s.SetFloat("_Glossiness", 0.2f);
                        s.SetFloat("_Metallic", 0f);
                        simple[m] = s;
                    }
                    mats[i] = s;
                }
                r.sharedMaterials = mats;
            }
        }

        /// <summary>Sorteia um modelo da lista que exista no projeto (ou null: boneco de blocos).</summary>
        public static string Pick(string[] list)
        {
            if (Controller == null) return null; // sem as animações preparadas, nada de modelo 3D
            var ok = list.Where(Exists).ToList();
            return ok.Count == 0 ? null : ok[Random.Range(0, ok.Count)];
        }

        static bool Exists(string name)
        {
            if (!exists.TryGetValue(name, out var e))
                exists[name] = e = Resources.Load<GameObject>("NPCs/" + name) != null;
            return e;
        }
    }
}

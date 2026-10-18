using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace DevCity.EditorTools
{
    /// <summary>
    /// Prepara os NPCs (pedestres, polícia, gangue) baixados em .glb — os "Ultimate Modular
    /// Characters" da Quaternius, no Poly Pizza.
    ///
    /// Os arquivos ficam em Assets/Resources/NPCs/npc-*.glb e são importados pelo pacote glTFast.
    /// Todos usam o MESMO esqueleto (62 ossos com os mesmos nomes), então um único Animator
    /// Controller serve para todos. Esta ferramenta:
    ///   1. copia as animações que o jogo usa (de um personagem só) para Assets/Resources/NPCs/Animacoes,
    ///      com o "loop" certo (o glTFast marca todas como repetição, até a de morte);
    ///   2. cria Assets/Resources/NPCs/NpcAnimator.controller com os mesmos parâmetros do jogador
    ///      (Speed, Aim, HandsUp, Cower, Punch) — assim o Humanoid.cs funciona igual para os dois.
    ///
    /// Menu: Dev City → Preparar NPCs (.glb).
    /// </summary>
    public static class NpcImporter
    {
        const string Dir = "Assets/Resources/NPCs";
        const string AnimDir = Dir + "/Animacoes";
        const string ControllerPath = Dir + "/NpcAnimator.controller";

        // nome da animação no .glb → (nome do arquivo, repete?)
        static readonly (string src, string dst, bool loop)[] Clips =
        {
            ("Idle", "Parado", true), ("Walk", "Andar", true), ("Run", "Correr", true),
            ("Idle_Gun_Pointing", "Mirar", true), ("Punch_Right", "Soco", false),
            ("Death", "Morte", false), ("HitRecieve", "Encolhido", true), ("Wave", "MaosParaCima", true),
        };

        [UnityEditor.MenuItem("Dev City/Preparar NPCs (.glb)", false, 33)]
        public static void Run()
        {
            var glbs = Directory.GetFiles(Dir, "*.glb").Select(p => p.Replace('\\', '/')).OrderBy(p => p).ToList();
            if (glbs.Count == 0) { Debug.LogWarning($"[NPCs] Nenhum .glb em {Dir}."); return; }
            string src = glbs.FirstOrDefault(p => p.Contains("executivo")) ?? glbs[0];
            var all = AssetDatabase.LoadAllAssetsAtPath(src).OfType<AnimationClip>().ToList();
            if (all.Count == 0) { Debug.LogError($"[NPCs] {src} não tem animações. O pacote glTFast está instalado?"); return; }

            // 1) cópias das animações, com o loop certo
            Directory.CreateDirectory(AnimDir);
            var clips = new Dictionary<string, AnimationClip>();
            foreach (var (s, d, loop) in Clips)
            {
                // o nome pode vir puro ("Walk") ou com prefixo ("CharacterArmature|Walk")
                var orig = all.FirstOrDefault(c => c.name == s || c.name.EndsWith("|" + s));
                if (orig == null) { Debug.LogWarning($"[NPCs] animação \"{s}\" não encontrada."); continue; }
                var copy = Object.Instantiate(orig);
                copy.name = d;
                var settings = AnimationUtility.GetAnimationClipSettings(copy);
                settings.loopTime = loop;
                AnimationUtility.SetAnimationClipSettings(copy, settings);
                string path = $"{AnimDir}/{d}.anim";
                AssetDatabase.DeleteAsset(path);
                AssetDatabase.CreateAsset(copy, path);
                clips[d] = copy;
                Debug.Log($"[NPCs] {orig.name} → {d}{(loop ? " (repete)" : "")}");
            }

            // 2) Animator Controller (mesmos parâmetros do jogador)
            AssetDatabase.DeleteAsset(ControllerPath);
            var ac = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            foreach (var p in new[] { "Aim", "HandsUp", "Sitting", "Cower" }) ac.AddParameter(p, AnimatorControllerParameterType.Bool);
            ac.AddParameter("Speed", AnimatorControllerParameterType.Float);
            ac.AddParameter("Punch", AnimatorControllerParameterType.Trigger);
            var sm = ac.layers[0].stateMachine;
            var loco = ac.CreateBlendTreeInController("Movimento", out var tree, 0);
            tree.blendParameter = "Speed";
            tree.blendType = BlendTreeType.Simple1D;
            tree.useAutomaticThresholds = false;
            void Add(string n, float speed) { if (clips.TryGetValue(n, out var c)) tree.AddChild(c, speed); }
            Add("Parado", 0f); Add("Andar", 1.6f); Add("Correr", 4.6f); Add("Correr", 7.6f);
            sm.defaultState = loco;

            void BoolState(string state, string clip, string param)
            {
                if (!clips.TryGetValue(clip, out var c)) return;
                var st = sm.AddState(state);
                st.motion = c;
                var t = sm.AddAnyStateTransition(st);
                t.AddCondition(AnimatorConditionMode.If, 0, param);
                t.duration = 0.15f; t.hasExitTime = false; t.canTransitionToSelf = false;
                var back = st.AddTransition(loco);
                back.AddCondition(AnimatorConditionMode.IfNot, 0, param);
                back.duration = 0.2f; back.hasExitTime = false;
            }
            BoolState("Mirando", "Mirar", "Aim");
            BoolState("MaosParaCima", "MaosParaCima", "HandsUp");
            BoolState("Encolhido", "Encolhido", "Cower");
            if (clips.TryGetValue("Soco", out var soco))
            {
                var st = sm.AddState("Soco");
                st.motion = soco;
                var t = sm.AddAnyStateTransition(st);
                t.AddCondition(AnimatorConditionMode.If, 0, "Punch");
                t.duration = 0.05f; t.hasExitTime = false; t.canTransitionToSelf = false;
                var back = st.AddTransition(loco);
                back.hasExitTime = true; back.exitTime = 0.85f; back.duration = 0.15f;
            }
            // morte: o jogo toca com CrossFade("Morte") e ela fica parada no último quadro (sem saída)
            if (clips.TryGetValue("Morte", out var morte)) sm.AddState("Morte").motion = morte;

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[NPCs] Pronto: {glbs.Count} personagem(ns), {clips.Count} animações, controller em {ControllerPath}.");
        }
    }
}

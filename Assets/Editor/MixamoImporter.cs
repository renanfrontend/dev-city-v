using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace DevCity.EditorTools
{
    /// <summary>
    /// Prepara personagens baixados da Mixamo para o Dev City V.
    ///
    /// Onde colocar os arquivos:
    ///   Assets/Mixamo/Personagens/Trevor.fbx, Jacob.fbx  (com pele: "With Skin")
    ///   Assets/Mixamo/Animacoes/Idle.fbx, Walking.fbx...   (sem pele: "Without Skin")
    ///
    /// O que esta ferramenta faz:
    ///   1. marca todo FBX como Humanoid (assim qualquer animação serve em qualquer personagem);
    ///   2. descobre o papel de cada animação pelo NOME do arquivo e liga o loop;
    ///   3. cria o Animator Controller (movimento + sentado + mãos para cima + encolhido, e uma
    ///      camada só dos braços para mirar e socar);
    ///   4. gera um prefab por personagem em Assets/Resources/Personagens (o jogo carrega dali).
    ///
    /// Também roda pela linha de comando:
    ///   Unity.exe -batchmode -quit -projectPath . -executeMethod DevCity.EditorTools.MixamoImporter.Run
    /// </summary>
    public static class MixamoImporter
    {
        const string CharDir = "Assets/Mixamo/Personagens";
        const string AnimDir = "Assets/Mixamo/Animacoes";
        const string OutDir = "Assets/Resources/Personagens";
        const string ControllerPath = OutDir + "/DevCityAnimator.controller";
        const string MaskPath = OutDir + "/MascaraBracos.mask";

        // papel da animação -> palavras que podem aparecer no nome do arquivo (a ordem importa:
        // "Pistol Idle" tem que virar "mira", não "parado")
        static readonly (string role, string[] keys)[] Roles =
        {
            ("entrar", new[] { "enter", "entrar" }),
            ("sair", new[] { "exit", "sair" }),
            ("mira", new[] { "aim", "pistol", "rifle", "mira" }),
            ("sprint", new[] { "sprint", "fast run", "fastrun", "fast_run" }),
            ("maos", new[] { "hands", "surrender", "maos" }),
            ("sentado", new[] { "sit", "driv", "sentado" }),
            ("encolhido", new[] { "cower", "terrified", "fear", "medo" }),
            ("soco", new[] { "punch", "soco" }),
            ("andar", new[] { "walk", "andar" }),
            ("correr", new[] { "run", "correr" }),
            ("parado", new[] { "idle", "parado" }),
        };

        // animações que tocam uma vez só (sem loop)
        static readonly string[] OneShot = { "soco", "entrar", "sair" };

        [UnityEditor.MenuItem("Dev City/Preparar personagens Mixamo", false, 1)]
        public static void Run()
        {
            Directory.CreateDirectory(CharDir);
            Directory.CreateDirectory(AnimDir);
            Directory.CreateDirectory(OutDir);
            AssetDatabase.Refresh();

            var chars = FindFbx(CharDir);
            var anims = FindFbx(AnimDir);
            if (chars.Count == 0)
            {
                Debug.LogWarning($"[Mixamo] Nenhum personagem em {CharDir}. Baixe da Mixamo como \"FBX for Unity\" com pele (With Skin) e salve como Trevor.fbx e Jacob.fbx nessa pasta.");
                return;
            }

            // 1) personagens
            foreach (var path in chars) ConfigureCharacter(path);

            // 2) animações
            var clips = new Dictionary<string, AnimationClip>();
            foreach (var path in anims)
            {
                string file = Path.GetFileNameWithoutExtension(path);
                string role = RoleOf(file);
                if (role == null) { Debug.LogWarning($"[Mixamo] Não sei para que serve \"{file}\". Renomeie (ex.: Idle, Walking, Running, Sprint, Aim, HandsUp, Sitting, Cower, Punch)."); continue; }
                ConfigureAnimation(path, !OneShot.Contains(role));
                var clip = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().FirstOrDefault(c => !c.name.StartsWith("__preview__"));
                if (clip != null) { clips[role] = clip; Debug.Log($"[Mixamo] Animação \"{file}\" → {role}"); }
            }
            if (!clips.ContainsKey("parado"))
                Debug.LogWarning("[Mixamo] Falta a animação parada (Idle). O personagem vai ficar em T-pose quando estiver parado.");

            // 3) Animator Controller
            var controller = BuildController(clips);
            SaveCarActions(clips);

            // 4) prefabs
            foreach (var path in chars) MakePrefab(path, controller);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            string missing = string.Join(", ", Roles.Select(r => r.role).Where(r => !clips.ContainsKey(r)));
            Debug.Log($"[Mixamo] Pronto: {chars.Count} personagem(ns), {clips.Count} animação(ões). " +
                      (missing.Length > 0 ? $"Faltando (opcional): {missing}." : "Todas as animações encontradas."));
        }

        static List<string> FindFbx(string dir) =>
            AssetDatabase.FindAssets("t:Model", new[] { dir })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => p.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase))
                .ToList();

        static string RoleOf(string fileName)
        {
            string n = fileName.ToLowerInvariant();
            foreach (var (role, keys) in Roles)
                if (keys.Any(k => n.Contains(k))) return role;
            return null;
        }

        static void ConfigureCharacter(string path)
        {
            var imp = (ModelImporter)AssetImporter.GetAtPath(path);
            imp.animationType = ModelImporterAnimationType.Human;
            imp.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            imp.importAnimation = false;
            imp.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            imp.materialLocation = ModelImporterMaterialLocation.InPrefab;
            imp.SaveAndReimport();

            // a Mixamo coloca as texturas DENTRO do FBX: tira para uma pasta do projeto
            string name = Path.GetFileNameWithoutExtension(path);
            string texDir = $"{CharDir}/Texturas_{name}";
            if (!AssetDatabase.IsValidFolder(texDir) || AssetDatabase.FindAssets("t:Texture2D", new[] { texDir }).Length == 0)
            {
                imp.ExtractTextures(texDir);
                AssetDatabase.Refresh();
            }
            var textures = AssetDatabase.FindAssets("t:Texture2D", new[] { texDir })
                .Select(AssetDatabase.GUIDToAssetPath).ToList();

            // texturas "_Normal" precisam ser marcadas como normal map
            foreach (var t in textures.Where(t => t.ToLowerInvariant().Contains("normal")))
            {
                var ti = (TextureImporter)AssetImporter.GetAtPath(t);
                if (ti.textureType != TextureImporterType.NormalMap) { ti.textureType = TextureImporterType.NormalMap; ti.SaveAndReimport(); }
            }

            // materiais como arquivos .mat ao lado do FBX (o Unity liga as texturas pelo nome)
            imp.materialLocation = ModelImporterMaterialLocation.External;
            imp.materialName = ModelImporterMaterialName.BasedOnMaterialName;
            imp.materialSearch = ModelImporterMaterialSearch.Everywhere;
            imp.SaveAndReimport();

            // garantia: material ainda sem textura recebe a _Diffuse (a Mixamo numera 1001, 1002...)
            var diffuse = textures.Where(t => t.ToLowerInvariant().Contains("diffuse")).OrderBy(t => t).ToList();
            var normals = textures.Where(t => t.ToLowerInvariant().Contains("normal")).OrderBy(t => t).ToList();
            var renderers = AssetDatabase.LoadAssetAtPath<GameObject>(path).GetComponentsInChildren<Renderer>(true);
            var mats = renderers.SelectMany(r => r.sharedMaterials).Where(m => m != null).Distinct().ToList();
            for (int i = 0; i < mats.Count; i++)
            {
                var m = mats[i];
                string matPath = AssetDatabase.GetAssetPath(m);
                if (m.mainTexture == null && diffuse.Count > 0 && !matPath.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase))
                {
                    m.mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(diffuse[Mathf.Min(i, diffuse.Count - 1)]);
                    if (normals.Count > 0)
                    {
                        m.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(normals[Mathf.Min(i, normals.Count - 1)]));
                        m.EnableKeyword("_NORMALMAP");
                    }
                    EditorUtility.SetDirty(m);
                }
                Debug.Log($"[Mixamo] {name}: material \"{m.name}\" → textura {(m.mainTexture != null ? m.mainTexture.name : "NENHUMA")}");
            }
        }

        static void ConfigureAnimation(string path, bool loop)
        {
            var imp = (ModelImporter)AssetImporter.GetAtPath(path);
            imp.animationType = ModelImporterAnimationType.Human;
            imp.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            imp.importAnimation = true;
            imp.SaveAndReimport();
            var clips = imp.defaultClipAnimations;
            foreach (var c in clips)
            {
                c.loopTime = loop;
                c.loopPose = loop;
                // "assa" o movimento da raiz na pose: o personagem anima no lugar e o jogo move o corpo
                c.lockRootRotation = true;
                c.keepOriginalOrientation = true;
                c.lockRootHeightY = true;
                c.keepOriginalPositionY = true;
                c.lockRootPositionXZ = true;
            }
            imp.clipAnimations = clips;
            imp.SaveAndReimport();
        }

        static AnimatorController BuildController(Dictionary<string, AnimationClip> clips)
        {
            AssetDatabase.DeleteAsset(ControllerPath);
            AssetDatabase.DeleteAsset(MaskPath);
            var ac = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            ac.AddParameter("Speed", AnimatorControllerParameterType.Float);
            ac.AddParameter("Aim", AnimatorControllerParameterType.Bool);
            ac.AddParameter("HandsUp", AnimatorControllerParameterType.Bool);
            ac.AddParameter("Sitting", AnimatorControllerParameterType.Bool);
            ac.AddParameter("Cower", AnimatorControllerParameterType.Bool);
            ac.AddParameter("Punch", AnimatorControllerParameterType.Trigger);

            // camada 0: corpo inteiro
            var sm = ac.layers[0].stateMachine;
            var loco = ac.CreateBlendTreeInController("Movimento", out var tree, 0);
            tree.blendParameter = "Speed";
            tree.blendType = BlendTreeType.Simple1D;
            tree.useAutomaticThresholds = false;
            // velocidades do jogo: andar ~1,6 m/s, correr 4,6 m/s (padrão do GTA), disparada 7,6 m/s
            void Add(string role, float speed) { if (clips.TryGetValue(role, out var c)) tree.AddChild(c, speed); }
            Add("parado", 0f);
            Add("andar", 1.6f);
            Add("correr", 4.6f);
            Add("sprint", 7.6f);
            sm.defaultState = loco;

            BoolState(sm, "Sentado", clips, "sentado", "Sitting", loco);
            BoolState(sm, "MaosParaCima", clips, "maos", "HandsUp", loco);
            BoolState(sm, "Encolhido", clips, "encolhido", "Cower", loco);

            // entrar e sair do carro: o jogo chama anim.CrossFade("EntrandoNoCarro") direto
            // (as da Mixamo duram ~5,5 s; aceleramos para ~3,4 s, perto do tempo do GTA)
            OneShotState(sm, "EntrandoNoCarro", clips, "entrar", loco, 1.6f);
            OneShotState(sm, "SaindoDoCarro", clips, "sair", loco, 1.8f);

            // camada 1: só braços e tronco (mira e soco por cima do andar)
            var mask = new AvatarMask();
            foreach (AvatarMaskBodyPart part in System.Enum.GetValues(typeof(AvatarMaskBodyPart)))
            {
                if (part == AvatarMaskBodyPart.LastBodyPart) continue;
                bool upper = part == AvatarMaskBodyPart.Body || part == AvatarMaskBodyPart.Head ||
                             part == AvatarMaskBodyPart.LeftArm || part == AvatarMaskBodyPart.RightArm ||
                             part == AvatarMaskBodyPart.LeftFingers || part == AvatarMaskBodyPart.RightFingers;
                mask.SetHumanoidBodyPartActive(part, upper);
            }
            AssetDatabase.CreateAsset(mask, MaskPath);

            ac.AddLayer("Bracos");
            var layers = ac.layers;
            layers[1].avatarMask = mask;
            layers[1].defaultWeight = 1f;
            ac.layers = layers;
            var sm2 = ac.layers[1].stateMachine;
            var empty = sm2.AddState("Vazio");
            sm2.defaultState = empty;
            BoolState(sm2, "Mirando", clips, "mira", "Aim", empty);
            if (clips.TryGetValue("soco", out var punch))
            {
                var st = sm2.AddState("Soco");
                st.motion = punch;
                var t = sm2.AddAnyStateTransition(st);
                t.AddCondition(AnimatorConditionMode.If, 0, "Punch");
                t.duration = 0.05f;
                t.hasExitTime = false;
                t.canTransitionToSelf = false;
                var back = st.AddTransition(empty);
                back.hasExitTime = true;
                back.exitTime = 0.85f;
                back.duration = 0.1f;
            }
            return ac;
        }

        /// <summary>Estado que entra quando o parâmetro bool liga e volta quando desliga.</summary>
        static void BoolState(AnimatorStateMachine sm, string name, Dictionary<string, AnimationClip> clips, string role, string param, AnimatorState back)
        {
            if (!clips.TryGetValue(role, out var clip)) return;
            var st = sm.AddState(name);
            st.motion = clip;
            var t = sm.AddAnyStateTransition(st);
            t.AddCondition(AnimatorConditionMode.If, 0, param);
            t.duration = 0.15f;
            t.hasExitTime = false;
            t.canTransitionToSelf = false;
            var r = st.AddTransition(back);
            r.AddCondition(AnimatorConditionMode.IfNot, 0, param);
            r.duration = 0.2f;
            r.hasExitTime = false;
        }

        /// <summary>Estado tocado pelo código (CrossFade) que volta sozinho para o movimento no fim.</summary>
        static void OneShotState(AnimatorStateMachine sm, string name, Dictionary<string, AnimationClip> clips, string role, AnimatorState back, float speed)
        {
            if (!clips.TryGetValue(role, out var clip)) return;
            var st = sm.AddState(name);
            st.motion = clip;
            st.speed = speed;
            var r = st.AddTransition(back);
            r.hasExitTime = true;
            r.exitTime = 0.98f;
            r.duration = 0.1f;
        }

        /// <summary>
        /// Mede quanto o corpo anda e gira nas animações de entrar/sair do carro e grava em
        /// Resources/Personagens/AcoesCarro.json. O jogo usa isso para posicionar o personagem
        /// na porta de um jeito que, no fim da animação, ele caia certinho no banco.
        /// </summary>
        static void SaveCarActions(Dictionary<string, AnimationClip> clips)
        {
            var parts = new List<string>();
            foreach (var role in new[] { "entrar", "sair" })
            {
                if (!clips.TryGetValue(role, out var clip)) continue;
                var bindings = AnimationUtility.GetCurveBindings(clip);
                float Curve(string prop, float t)
                {
                    foreach (var b in bindings)
                        if (b.propertyName == prop) return AnimationUtility.GetEditorCurve(clip, b).Evaluate(t);
                    return 0f;
                }
                Vector3 T(float t) => new Vector3(Curve("RootT.x", t), Curve("RootT.y", t), Curve("RootT.z", t));
                Quaternion Q(float t) => new Quaternion(Curve("RootQ.x", t), Curve("RootQ.y", t), Curve("RootQ.z", t), Curve("RootQ.w", t));
                float len = clip.length;
                Vector3 t0 = T(0), t1 = T(len);
                float yaw0 = Q(0).eulerAngles.y, yaw1 = Q(len).eulerAngles.y;
                var a = new CarAction { dx = t1.x - t0.x, dz = t1.z - t0.z, y0 = t0.y, y1 = t1.y, yaw0 = yaw0, yaw1 = yaw1, len = len };
                parts.Add($"\"{role}\":{JsonUtility.ToJson(a)}");
                Debug.Log(System.FormattableString.Invariant(
                    $"[Mixamo] {role}: corpo anda ({a.dx:0.00}, {a.dz:0.00}), altura {a.y0:0.00}→{a.y1:0.00}, gira {yaw0:0}°→{yaw1:0}°, {len:0.0} s"));
            }
            File.WriteAllText(OutDir + "/AcoesCarro.json", "{" + string.Join(",", parts) + "}");
            AssetDatabase.ImportAsset(OutDir + "/AcoesCarro.json");
        }

        [System.Serializable]
        class CarAction { public float dx, dz, y0, y1, yaw0, yaw1, len; }

        static void MakePrefab(string path, AnimatorController controller)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var avatar = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Avatar>().FirstOrDefault();
            if (avatar == null || !avatar.isHuman)
            {
                Debug.LogError($"[Mixamo] {path} não virou um humanoide válido. Baixe de novo da Mixamo em \"FBX for Unity\" e T-pose.");
                return;
            }
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(model);
            var animator = inst.GetComponent<Animator>();
            if (animator == null) animator = inst.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.avatar = avatar;
            animator.applyRootMotion = false;
            string name = Path.GetFileNameWithoutExtension(path);
            string outPath = $"{OutDir}/{name}.prefab";
            PrefabUtility.SaveAsPrefabAsset(inst, outPath);
            Object.DestroyImmediate(inst);
            Debug.Log($"[Mixamo] Personagem \"{name}\" pronto em {outPath}");
        }
    }
}

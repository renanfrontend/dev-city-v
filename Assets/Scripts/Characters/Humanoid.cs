using System.Collections.Generic;
using UnityEngine;

namespace DevCity
{
    public struct Outfit
    {
        public Color Skin, Hair, Shirt, Pants, Shoes;
        public int Style; // 0 civil, 1 policial, 2 SWAT, 3 gangue, 4 terno
        public bool Cap;
        public string Model; // modelo 3D do NPC em Resources/NPCs (vazio = boneco de blocos)

        static readonly Color[] skins = { new Color(0.96f, 0.8f, 0.68f), new Color(0.85f, 0.65f, 0.5f), new Color(0.65f, 0.45f, 0.32f), new Color(0.45f, 0.3f, 0.2f), new Color(0.3f, 0.2f, 0.14f) };
        static readonly Color[] hairs = { new Color(0.1f, 0.07f, 0.05f), new Color(0.3f, 0.18f, 0.1f), new Color(0.75f, 0.6f, 0.35f), new Color(0.5f, 0.5f, 0.5f), new Color(0.05f, 0.05f, 0.05f) };
        static readonly Color[] shirts = { new Color(0.9f, 0.9f, 0.9f), new Color(0.15f, 0.2f, 0.4f), new Color(0.8f, 0.2f, 0.2f), new Color(0.2f, 0.55f, 0.3f), new Color(0.95f, 0.75f, 0.2f), new Color(0.3f, 0.3f, 0.32f), new Color(0.95f, 0.5f, 0.6f), new Color(0.35f, 0.7f, 0.85f), new Color(0.55f, 0.35f, 0.65f) };
        static readonly Color[] pants = { new Color(0.15f, 0.2f, 0.35f), new Color(0.2f, 0.2f, 0.22f), new Color(0.55f, 0.5f, 0.4f), new Color(0.1f, 0.1f, 0.1f), new Color(0.35f, 0.3f, 0.25f) };

        public static Outfit Random()
        {
            return new Outfit
            {
                Skin = skins[UnityEngine.Random.Range(0, skins.Length)],
                Hair = hairs[UnityEngine.Random.Range(0, hairs.Length)],
                Shirt = shirts[UnityEngine.Random.Range(0, shirts.Length)],
                Pants = pants[UnityEngine.Random.Range(0, pants.Length)],
                Shoes = UnityEngine.Random.value < 0.5f ? new Color(0.1f, 0.1f, 0.1f) : new Color(0.9f, 0.9f, 0.9f),
                Cap = UnityEngine.Random.value < 0.2f,
                Model = NpcModels.Pick(NpcModels.Civilians),
            };
        }

        public static Outfit Cop()
        {
            var o = Random();
            o.Shirt = new Color(0.12f, 0.17f, 0.32f); o.Pants = new Color(0.1f, 0.12f, 0.22f); o.Shoes = Color.black; o.Style = 1; o.Cap = true;
            o.Model = NpcModels.Pick(NpcModels.Police);
            return o;
        }

        public static Outfit Swat()
        {
            var o = Random();
            o.Shirt = new Color(0.12f, 0.12f, 0.13f); o.Pants = new Color(0.12f, 0.12f, 0.13f); o.Shoes = Color.black; o.Style = 2; o.Cap = true;
            o.Model = NpcModels.Pick(NpcModels.Police);
            return o;
        }

        public static Outfit Gang()
        {
            var o = Random();
            o.Shirt = new Color(0.55f, 0.1f, 0.6f); o.Pants = new Color(0.15f, 0.15f, 0.17f); o.Style = 3; o.Cap = UnityEngine.Random.value < 0.6f;
            o.Model = NpcModels.Pick(NpcModels.Gang);
            return o;
        }

        public static Outfit Player()
        {
            return new Outfit
            {
                Skin = new Color(0.62f, 0.43f, 0.3f), Hair = new Color(0.07f, 0.05f, 0.04f),
                Shirt = new Color(0.92f, 0.92f, 0.9f), Pants = new Color(0.22f, 0.28f, 0.42f), Shoes = new Color(0.95f, 0.95f, 0.95f)
            };
        }
    }

    /// <summary>
    /// Personagem do jogo. Tem dois modos:
    /// 1) Modelo da Mixamo (ou outro humanoide): se existir o prefab Resources/Personagens/&lt;nome&gt;,
    ///    ele é usado com um Animator (parâmetros Speed, Aim, HandsUp, Sitting, Cower, Punch).
    /// 2) Boneco de blocos animado por código (quando não há modelo).
    /// Os dois modos usam os mesmos campos públicos, então o resto do jogo não muda.
    /// Origem nos pés, altura ~1,8 m, olhando para +Z.
    /// </summary>
    public class Humanoid : MonoBehaviour
    {
        public float MoveSpeed;      // m/s (define a animação)
        public bool Aim;
        public float AimPitch;       // graus (positivo = para cima)
        public bool HandsUp, Sitting, Cower, Phone, Punching;
        public int WeaponModel;      // 0 nada, 1 pistola, 2 arma longa
        public string WeaponFile;    // modelo 3D da arma (Resources/Armas/<nome>); vazio = caixinhas

        Transform hips, torso, head, armL, armR, legL, legR, foreL, foreR, gunShort, gunLong;
        Renderer[] shirtR, pantsR, skinR, hairR, shoesR;
        float phase, punchT;

        public Transform Muzzle { get; private set; }
        Transform weaponMount, fallbackMuzzle;
        readonly Dictionary<string, Transform> weaponObjs = new Dictionary<string, Transform>();
        string shownWeapon; // null = ainda não mostrou nada (a primeira chamada sempre ajusta)
        public bool Rigged => anim != null;

        // modo Mixamo
        Animator anim;
        Transform chest;
        static readonly int pSpeed = Animator.StringToHash("Speed"), pAim = Animator.StringToHash("Aim"),
            pHandsUp = Animator.StringToHash("HandsUp"), pSitting = Animator.StringToHash("Sitting"),
            pCower = Animator.StringToHash("Cower"), pPunch = Animator.StringToHash("Punch");

        /// <summary>Usa o modelo Resources/Personagens/&lt;modelName&gt; se existir; senão, o boneco de blocos.</summary>
        public static Humanoid Create(Transform parent, Outfit o, string modelName)
        {
            // "Trevor" → Resources/Personagens/Trevor; "NPCs/npc-punk" → Resources/NPCs/npc-punk
            var prefab = string.IsNullOrEmpty(modelName) ? null : Resources.Load<GameObject>(modelName.Contains("/") ? modelName : "Personagens/" + modelName);
            if (prefab == null) { o.Model = null; return Create(parent, o); }
            var root = new GameObject("Modelo");
            root.transform.SetParent(parent, false);
            var h = root.AddComponent<Humanoid>();
            h.BuildRigged(prefab);
            return h;
        }

        public static Humanoid Create(Transform parent, Outfit o)
        {
            if (!string.IsNullOrEmpty(o.Model)) return Create(parent, o, "NPCs/" + o.Model);
            var root = new GameObject("Modelo");
            root.transform.SetParent(parent, false);
            var h = root.AddComponent<Humanoid>();
            h.Build(o);
            return h;
        }

        Transform Pivot(string name, Transform parent, Vector3 localPos)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.localPosition = localPos;
            return t;
        }

        void Build(Outfit o)
        {
            var skin = Mats.Get(o.Skin, 0.3f);
            var shirt = Mats.Get(o.Shirt, 0.15f);
            var pant = Mats.Get(o.Pants, 0.15f);
            var shoe = Mats.Get(o.Shoes, 0.4f);
            var hair = Mats.Get(o.Hair, 0.3f);

            hips = Pivot("Quadril", transform, new Vector3(0, 0.95f, 0));
            legL = Pivot("PernaE", hips, new Vector3(-0.12f, 0, 0));
            legR = Pivot("PernaD", hips, new Vector3(0.12f, 0, 0));
            var lL = Gen.Cube(legL, new Vector3(0, -0.45f, 0), new Vector3(0.19f, 0.9f, 0.21f), pant);
            var lR = Gen.Cube(legR, new Vector3(0, -0.45f, 0), new Vector3(0.19f, 0.9f, 0.21f), pant);
            var sL = Gen.Cube(legL, new Vector3(0, -0.89f, 0.06f), new Vector3(0.2f, 0.12f, 0.32f), shoe);
            var sR = Gen.Cube(legR, new Vector3(0, -0.89f, 0.06f), new Vector3(0.2f, 0.12f, 0.32f), shoe);

            torso = Pivot("Tronco", hips, Vector3.zero);
            var body = Gen.Cube(torso, new Vector3(0, 0.33f, 0), new Vector3(0.46f, 0.64f, 0.26f), shirt);
            var belt = Gen.Cube(torso, new Vector3(0, 0.03f, 0), new Vector3(0.44f, 0.08f, 0.25f), Mats.Black);
            Renderer extra = null;
            if (o.Style == 1) extra = Gen.Cube(torso, new Vector3(0.12f, 0.5f, 0.135f), new Vector3(0.08f, 0.08f, 0.02f), Mats.Get(new Color(0.9f, 0.75f, 0.2f), 0.8f, 0.8f)).GetComponent<Renderer>();
            if (o.Style == 2) extra = Gen.Cube(torso, new Vector3(0, 0.36f, 0), new Vector3(0.5f, 0.5f, 0.32f), Mats.Get(new Color(0.2f, 0.22f, 0.2f))).GetComponent<Renderer>();
            if (o.Style == 4) extra = Gen.Cube(torso, new Vector3(0, 0.45f, 0.135f), new Vector3(0.06f, 0.35f, 0.02f), Mats.Get(new Color(0.6f, 0.1f, 0.1f))).GetComponent<Renderer>();

            head = Pivot("Cabeca", torso, new Vector3(0, 0.72f, 0));
            var neck = Gen.Cube(head, new Vector3(0, -0.04f, 0), new Vector3(0.12f, 0.1f, 0.12f), skin);
            var face = Gen.Cube(head, new Vector3(0, 0.12f, 0), new Vector3(0.22f, 0.26f, 0.24f), skin);
            var hr = Gen.Cube(head, new Vector3(0, 0.25f, -0.01f), new Vector3(0.24f, 0.08f, 0.26f), hair);
            var eyes = Gen.Cube(head, new Vector3(0, 0.15f, 0.12f), new Vector3(0.14f, 0.03f, 0.01f), Mats.Black);
            if (o.Cap)
            {
                var capMat = o.Style == 1 ? Mats.Get(new Color(0.1f, 0.12f, 0.25f)) : o.Style == 2 ? Mats.Get(new Color(0.15f, 0.15f, 0.15f)) : shirt;
                Gen.Cube(head, new Vector3(0, 0.29f, 0), new Vector3(0.25f, 0.07f, 0.27f), capMat);
                Gen.Cube(head, new Vector3(0, 0.27f, 0.16f), new Vector3(0.22f, 0.025f, 0.12f), capMat);
            }

            armL = Pivot("BracoE", torso, new Vector3(-0.3f, 0.6f, 0));
            armR = Pivot("BracoD", torso, new Vector3(0.3f, 0.6f, 0));
            var aL = Gen.Cube(armL, new Vector3(0, -0.15f, 0), new Vector3(0.14f, 0.32f, 0.15f), shirt);
            var aR = Gen.Cube(armR, new Vector3(0, -0.15f, 0), new Vector3(0.14f, 0.32f, 0.15f), shirt);
            foreL = Pivot("AntebracoE", armL, new Vector3(0, -0.3f, 0));
            foreR = Pivot("AntebracoD", armR, new Vector3(0, -0.3f, 0));
            var fL = Gen.Cube(foreL, new Vector3(0, -0.15f, 0), new Vector3(0.12f, 0.3f, 0.13f), skin);
            var fR = Gen.Cube(foreR, new Vector3(0, -0.15f, 0), new Vector3(0.12f, 0.3f, 0.13f), skin);

            // armas na mão direita
            gunShort = Pivot("Pistola", foreR, new Vector3(0, -0.32f, 0.08f));
            Gen.Cube(gunShort, new Vector3(0, 0, 0.06f), new Vector3(0.05f, 0.08f, 0.2f), Mats.Black);
            Gen.Cube(gunShort, new Vector3(0, -0.07f, -0.01f), new Vector3(0.045f, 0.12f, 0.06f), Mats.Black);
            gunLong = Pivot("Fuzil", foreR, new Vector3(0, -0.32f, 0.1f));
            Gen.Cube(gunLong, new Vector3(0, 0, 0.25f), new Vector3(0.06f, 0.1f, 0.75f), Mats.Black);
            Gen.Cube(gunLong, new Vector3(0, -0.09f, 0.08f), new Vector3(0.05f, 0.14f, 0.07f), Mats.Black);
            Muzzle = fallbackMuzzle = Pivot("Cano", foreR, new Vector3(0, -0.32f, 0.55f));
            weaponMount = Pivot("ArmaModelo", foreR, new Vector3(0, -0.36f, 0.08f));

            shirtR = new[] { body.GetComponent<Renderer>(), aL.GetComponent<Renderer>(), aR.GetComponent<Renderer>() };
            pantsR = new[] { lL.GetComponent<Renderer>(), lR.GetComponent<Renderer>() };
            skinR = new[] { neck.GetComponent<Renderer>(), face.GetComponent<Renderer>(), fL.GetComponent<Renderer>(), fR.GetComponent<Renderer>() };
            hairR = new[] { hr.GetComponent<Renderer>() };
            shoesR = new[] { sL.GetComponent<Renderer>(), sR.GetComponent<Renderer>() };
            phase = Random.value * 10f;
        }

        void BuildRigged(GameObject prefab)
        {
            var model = Instantiate(prefab, transform);
            model.name = prefab.name;
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;
            anim = model.GetComponentInChildren<Animator>();
            bool npc = anim == null || anim.runtimeAnimatorController == null;
            if (npc)
            {
                // NPC importado pelo glTFast: o Animator vai na raiz (as animações apontam para os ossos a partir dela)
                if (anim == null) anim = model.AddComponent<Animator>();
                anim.runtimeAnimatorController = NpcModels.Controller;
            }
            anim.applyRootMotion = false;
            // o jogador anima sempre; NPC fora da tela não precisa mexer os ossos
            anim.cullingMode = npc ? AnimatorCullingMode.CullUpdateTransforms : AnimatorCullingMode.AlwaysAnimate;

            // ajusta a altura para ~1,8 m (modelos da Mixamo às vezes vêm em outra escala)
            var renderers = model.GetComponentsInChildren<Renderer>();
            if (renderers.Length > 0)
            {
                var b = renderers[0].bounds;
                foreach (var r in renderers) b.Encapsulate(r.bounds);
                if (b.size.y > 0.1f) model.transform.localScale *= 1.8f / b.size.y;
            }
            foreach (var r in renderers) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            if (npc)
            {
                NpcModels.SimplifyMaterials(renderers);
                // NPC sem sombra: num boneco pequeno quase não aparece e custa o dobro de desenho
                foreach (var r in renderers) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                npcRenderers = renderers;
            }
            isNpc = npc;
            animPhase = Random.Range(0, 4);

            // arma presa no osso da mão direita
            Transform hand;
            if (anim.isHuman)
            {
                hand = anim.GetBoneTransform(HumanBodyBones.RightHand);
                chest = anim.GetBoneTransform(HumanBodyBones.Chest) ?? anim.GetBoneTransform(HumanBodyBones.Spine);
            }
            else
            {
                // esqueleto "genérico": acha os ossos pelo nome (padrão da Quaternius)
                hand = FindBone(model.transform, "Wrist.R");
                chest = FindBone(model.transform, "Chest");
            }
            var gunParent = hand != null ? hand : transform;
            var mount = Pivot("Arma", gunParent, Vector3.zero);
            mount.localScale = new Vector3(1f / gunParent.lossyScale.x, 1f / gunParent.lossyScale.y, 1f / gunParent.lossyScale.z);
            gunShort = Pivot("Pistola", mount, new Vector3(0.02f, 0.03f, 0.08f));
            Gen.Cube(gunShort, new Vector3(0, 0, 0.06f), new Vector3(0.05f, 0.08f, 0.2f), Mats.Black);
            gunLong = Pivot("Fuzil", mount, new Vector3(0.02f, 0.03f, 0.1f));
            Gen.Cube(gunLong, new Vector3(0, 0, 0.25f), new Vector3(0.06f, 0.1f, 0.75f), Mats.Black);
            Muzzle = fallbackMuzzle = Pivot("Cano", mount, new Vector3(0.02f, 0.03f, 0.5f));
            weaponMount = Pivot("ArmaModelo", mount, new Vector3(0.02f, 0f, 0.07f));
        }

        static Transform FindBone(Transform t, string name)
        {
            if (t.name == name) return t;
            foreach (Transform c in t) { var r = FindBone(c, name); if (r != null) return r; }
            return null;
        }

        /// <summary>Animação de morte (NPCs da Quaternius). True se tocou: aí o corpo não precisa "tombar" por código.</summary>
        public bool DeathAnimated { get; private set; }
        public bool PlayDeath()
        {
            if (DeathAnimated) return true;
            DeathAnimated = PlayAction("Morte", 0.1f);
            return DeathAnimated;
        }

        // ===== ações tocadas pelo código (entrar e sair do carro) =====

        /// <summary>Toca um estado do Animator (ex.: "EntrandoNoCarro"). False se o modelo não tem essa animação.</summary>
        public bool PlayAction(string state, float fade = 0.15f)
        {
            if (!Rigged || !anim.HasState(0, Animator.StringToHash(state))) return false;
            anim.CrossFadeInFixedTime(state, fade, 0, 0f);
            return true;
        }

        /// <summary>Progresso da ação (0 a 1) ou -1 se ela não está tocando.</summary>
        public float ActionProgress(string state)
        {
            if (!Rigged) return -1f;
            if (anim.IsInTransition(0) && anim.GetNextAnimatorStateInfo(0).IsName(state)) return 0f;
            var info = anim.GetCurrentAnimatorStateInfo(0);
            return info.IsName(state) ? info.normalizedTime : -1f;
        }

        /// <summary>Corta direto para outro estado, sem mistura (usado quando o jogador é reposicionado).</summary>
        public void SnapTo(string state)
        {
            if (!Rigged) return;
            anim.Play(state, 0, 0f);
            anim.Update(0f); // aplica a pose já neste quadro (sem "piscar")
        }

        public Transform Hips => Rigged ? anim.GetBoneTransform(HumanBodyBones.Hips) : hips;

        /// <summary>Escala das medidas "humanas" das animações para metros neste modelo.</summary>
        public float BodyScale => Rigged ? anim.humanScale * anim.transform.lossyScale.y : 1f;

        bool isNpc, npcHidden;
        Renderer[] npcRenderers;
        float animSkip;
        int animPhase; // cada NPC atualiza num quadro diferente (sorteado no BuildRigged)

        /// <summary>
        /// NPC longe da câmera (mais de 30 m) atualiza a animação só 1 vez a cada 4 quadros —
        /// ninguém percebe de longe, e a CPU fraca agradece.
        /// </summary>
        void ThrottleFarAnimation()
        {
            var cam = Camera.main;
            float d2 = cam != null ? (cam.transform.position - transform.position).sqrMagnitude : 0f;
            // muito longe (mais de 70 m): some (vira poucos pixels) e o Animator para sozinho (CullUpdateTransforms)
            bool hide = d2 > 70f * 70f;
            if (hide != npcHidden) { npcHidden = hide; foreach (var r in npcRenderers) if (r != null) r.enabled = !hide; }
            if (hide) { if (anim.enabled) anim.enabled = false; return; } // escondido: nem anima
            bool far = d2 > 30f * 30f;
            if (!far) { if (!anim.enabled) anim.enabled = true; animSkip = 0f; return; }
            if (anim.enabled) anim.enabled = false;
            animSkip += Time.deltaTime;
            if ((Time.frameCount + animPhase) % 4 == 0) { anim.Update(animSkip); animSkip = 0f; }
        }

        void RiggedUpdate()
        {
            if (isNpc) ThrottleFarAnimation();
            ShowWeapon();
            // a arma aponta para onde o personagem olha
            gunShort.parent.rotation = transform.rotation;
            anim.SetFloat(pSpeed, MoveSpeed, 0.1f, Time.deltaTime);
            anim.SetBool(pAim, Aim && WeaponModel > 0);
            anim.SetBool(pHandsUp, HandsUp || Phone);
            anim.SetBool(pSitting, Sitting);
            anim.SetBool(pCower, Cower);
            if (Punching) { anim.SetTrigger(pPunch); Punching = false; }
            transform.localScale = Sitting ? Vector3.one * 0.9f : Vector3.one;
        }

        /// <summary>
        /// Mostra o modelo 3D da arma atual (Resources/Armas/WeaponFile). Cada arma é criada uma vez
        /// e depois só liga/desliga. Sem modelo, usa as caixinhas antigas.
        /// </summary>
        void ShowWeapon()
        {
            string want = WeaponModel > 0 ? (WeaponFile ?? "") : "";
            if (want == shownWeapon) return;
            shownWeapon = want;
            foreach (var w in weaponObjs.Values) if (w != null) w.gameObject.SetActive(false);
            Transform obj = null;
            if (want.Length > 0 && !weaponObjs.TryGetValue(want, out obj))
            {
                var prefab = Resources.Load<GameObject>("Armas/" + want);
                if (prefab != null)
                {
                    obj = Instantiate(prefab, weaponMount).transform;
                    obj.localPosition = Vector3.zero;
                    obj.localRotation = Quaternion.identity;
                }
                weaponObjs[want] = obj;
            }
            if (obj != null) obj.gameObject.SetActive(true);
            gunShort.gameObject.SetActive(obj == null && WeaponModel == 1);
            gunLong.gameObject.SetActive(obj == null && WeaponModel == 2);
            Muzzle = obj != null && obj.Find("Cano") != null ? obj.Find("Cano") : fallbackMuzzle;
        }

        void OnAnimatorAimPitch()
        {
            // inclina o peito para mirar para cima/baixo (depois que o Animator posicionou o corpo)
            if (chest != null && Aim && WeaponModel > 0)
                chest.Rotate(transform.right, -AimPitch * 0.6f, Space.World);
        }

        public void SetOutfit(Outfit o)
        {
            if (Rigged) return; // o modelo da Mixamo já tem a própria roupa (textura)
            foreach (var r in shirtR) r.sharedMaterial = Mats.Get(o.Shirt, 0.15f);
            foreach (var r in pantsR) r.sharedMaterial = Mats.Get(o.Pants, 0.15f);
            foreach (var r in skinR) r.sharedMaterial = Mats.Get(o.Skin, 0.3f);
            foreach (var r in hairR) r.sharedMaterial = Mats.Get(o.Hair, 0.3f);
            foreach (var r in shoesR) r.sharedMaterial = Mats.Get(o.Shoes, 0.4f);
        }

        public void Punch() { punchT = 0.3f; if (Rigged) Punching = true; }

        void LateUpdate()
        {
            if (Rigged) { RiggedUpdate(); OnAnimatorAimPitch(); return; }
            float dt = Time.deltaTime;
            ShowWeapon();

            // sentado no carro: um pouco menor, para a cabeça não atravessar o teto
            transform.localScale = Sitting ? Vector3.one * 0.85f : Vector3.one;
            if (Sitting)
            {
                hips.localPosition = new Vector3(0, 0.4f, 0);
                legL.localRotation = legR.localRotation = Quaternion.Euler(-80f, 0, 0);
                armL.localRotation = armR.localRotation = Quaternion.Euler(-50f, 0, 0);
                foreL.localRotation = foreR.localRotation = Quaternion.Euler(-30f, 0, 0);
                torso.localRotation = Quaternion.Euler(-8f, 0, 0);
                head.localRotation = Quaternion.identity;
                return;
            }
            hips.localPosition = new Vector3(0, 0.95f, 0);

            if (Cower)
            {
                hips.localPosition = new Vector3(0, 0.55f, 0);
                legL.localRotation = Quaternion.Euler(-70f, 0, 0);
                legR.localRotation = Quaternion.Euler(-70f, 0, 0);
                torso.localRotation = Quaternion.Euler(35f, 0, 0);
                armL.localRotation = Quaternion.Euler(-150f, 0, 25f);
                armR.localRotation = Quaternion.Euler(-150f, 0, -25f);
                foreL.localRotation = foreR.localRotation = Quaternion.Euler(-60f, 0, 0);
                head.localRotation = Quaternion.Euler(20f, 0, 0);
                return;
            }

            // ciclo de passada proporcional à velocidade
            float speed = MoveSpeed;
            phase += dt * Mathf.Lerp(0f, 9f, Mathf.Clamp01(speed / 6f)) + dt * Mathf.Min(speed, 1f) * 2f;
            float amp = Mathf.Clamp01(speed / 2.5f) * Mathf.Lerp(28f, 50f, Mathf.Clamp01((speed - 3f) / 4f));
            float s = Mathf.Sin(phase);
            legL.localRotation = Quaternion.Euler(s * amp, 0, 0);
            legR.localRotation = Quaternion.Euler(-s * amp, 0, 0);
            float bob = Mathf.Abs(Mathf.Cos(phase)) * 0.05f * Mathf.Clamp01(speed / 3f);
            hips.localPosition = new Vector3(0, 0.95f + bob, 0);
            torso.localRotation = Quaternion.Euler(Mathf.Clamp(speed * 2f, 0f, 12f), 0, 0);
            head.localRotation = Quaternion.identity;

            // braços
            Quaternion aLRot = Quaternion.Euler(-s * amp * 0.8f, 0, 4f);
            Quaternion aRRot = Quaternion.Euler(s * amp * 0.8f, 0, -4f);
            Quaternion fLRot = Quaternion.Euler(-Mathf.Lerp(10f, 60f, Mathf.Clamp01(speed / 6f)), 0, 0);
            Quaternion fRRot = fLRot;

            if (HandsUp)
            {
                aLRot = Quaternion.Euler(-170f, 0, 15f);
                aRRot = Quaternion.Euler(-170f, 0, -15f);
                fLRot = fRRot = Quaternion.Euler(-20f, 0, 0);
            }
            else if (Phone)
            {
                aRRot = Quaternion.Euler(-30f, 0, -30f);
                fRRot = Quaternion.Euler(-140f, 0, 0);
            }
            else if (Aim && WeaponModel > 0)
            {
                torso.localRotation = Quaternion.Euler(-AimPitch * 0.3f, 0, 0);
                aRRot = Quaternion.Euler(-90f - AimPitch * 0.7f, 0, 0);
                fRRot = Quaternion.identity;
                if (WeaponModel == 2)
                {
                    aLRot = Quaternion.Euler(-80f - AimPitch * 0.7f, 30f, 0);
                    fLRot = Quaternion.Euler(-10f, 0, 0);
                }
            }
            if (punchT > 0f)
            {
                punchT -= dt;
                float k = Mathf.Sin((1f - punchT / 0.3f) * Mathf.PI);
                aRRot = Quaternion.Euler(-90f * k - 20f, 0, 0);
                fRRot = Quaternion.Euler(-60f * (1f - k), 0, 0);
            }
            armL.localRotation = Quaternion.Slerp(armL.localRotation, aLRot, dt * 14f);
            armR.localRotation = Quaternion.Slerp(armR.localRotation, aRRot, dt * 18f);
            foreL.localRotation = Quaternion.Slerp(foreL.localRotation, fLRot, dt * 14f);
            foreR.localRotation = Quaternion.Slerp(foreR.localRotation, fRRot, dt * 18f);
        }
    }
}

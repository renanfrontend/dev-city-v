using UnityEngine;

namespace DevCity
{
    public class WeaponDef
    {
        public string Name;
        public int Slot;
        public float Damage, Rate, Spread, Range = 120f;
        public int Mag, Pellets = 1, Price, AmmoPrice, AmmoPack, StartAmmo;
        public bool Auto, Melee, Thrown;
        public int Model;          // 0 nada, 1 curta, 2 longa (define a pose: mirar com uma ou duas mãos)
        public string File;        // modelo 3D em Resources/Armas/<File> (Dev City → Preparar armas)
        public float Zoom;         // FOV ao mirar (0 = padrão)
        public AudioClip Sound;
    }

    /// <summary>Armas fictícias, inspiradas nas categorias da roda de armas do GTA V.</summary>
    public static class Weapons
    {
        public static WeaponDef[] All;
        public static WeaponDef Fists => All[0];
        public static WeaponDef Pistol => All[1];
        public static WeaponDef Smg => All[2];
        public static WeaponDef Shotgun => All[3];
        public static WeaponDef Rifle => All[4];
        public static WeaponDef Sniper => All[5];
        public static WeaponDef Grenade => All[6];

        public static void Init()
        {
            if (All != null) return;
            All = new[]
            {
                new WeaponDef { Name = "Punhos", Slot = 0, Damage = 14f, Rate = 0.45f, Range = 1.8f, Melee = true, Model = 0, Sound = Sfx.Punch },
                new WeaponDef { Name = "Pistola Hawk .45", Slot = 1, Damage = 34f, Rate = 0.22f, Spread = 1.2f, Mag = 12, Price = 600, AmmoPrice = 60, AmmoPack = 36, StartAmmo = 48, Model = 1, File = "arma-pistola", Sound = Sfx.Pistol },
                new WeaponDef { Name = "Submetralhadora Vespa", Slot = 2, Damage = 22f, Rate = 0.075f, Spread = 3f, Mag = 30, Auto = true, Price = 1800, AmmoPrice = 90, AmmoPack = 90, StartAmmo = 120, Model = 1, File = "arma-submetralhadora", Sound = Sfx.Smg },
                new WeaponDef { Name = "Escopeta Bulldog", Slot = 3, Damage = 16f, Rate = 0.85f, Spread = 6.5f, Range = 45f, Pellets = 8, Mag = 6, Price = 2600, AmmoPrice = 80, AmmoPack = 18, StartAmmo = 24, Model = 2, File = "arma-escopeta", Sound = Sfx.Shotgun },
                new WeaponDef { Name = "Fuzil AR-7 Falcão", Slot = 4, Damage = 30f, Rate = 0.105f, Spread = 1.6f, Range = 200f, Mag = 30, Auto = true, Price = 4500, AmmoPrice = 120, AmmoPack = 90, StartAmmo = 120, Model = 2, File = "arma-fuzil", Sound = Sfx.Rifle, Zoom = 45f },
                new WeaponDef { Name = "Rifle Longshot", Slot = 5, Damage = 150f, Rate = 1.2f, Spread = 0.05f, Range = 500f, Mag = 5, Price = 9000, AmmoPrice = 150, AmmoPack = 15, StartAmmo = 20, Model = 2, File = "arma-sniper", Sound = Sfx.Sniper, Zoom = 12f },
                new WeaponDef { Name = "Granada", Slot = 6, Damage = 220f, Rate = 1f, Range = 7f, Mag = 1, Thrown = true, Price = 350, AmmoPrice = 350, AmmoPack = 1, StartAmmo = 3, Model = 0, Sound = Sfx.Click },
            };
        }
    }

    /// <summary>Arsenal do jogador: munição, recarga, tiro, mira assistida e granadas.</summary>
    public class PlayerWeapons : MonoBehaviour
    {
        public bool[] Owned;
        public int[] Ammo;   // reserva (fora do pente)
        public int[] Clip;
        public int Current;
        public bool Aiming { get; private set; }
        public static bool ScriptedAim; // AutoTest: mira sem o mouse
        public bool TargetUnderCrosshair { get; private set; }
        public WeaponDef Def => Weapons.All[Current];

        PlayerController player;
        float cooldown, reloadT, assistT, regenBlock;
        IDamageable assistTarget;
        Transform assistTransform;

        public void Init(PlayerController p)
        {
            Weapons.Init();
            player = p;
            int n = Weapons.All.Length;
            Owned = new bool[n]; Ammo = new int[n]; Clip = new int[n];
            Owned[0] = true;
            Give(Weapons.Pistol, 48);
            Current = 0;
        }

        public void Give(WeaponDef w, int ammo)
        {
            Owned[w.Slot] = true;
            if (w.Melee) return;
            if (w.Thrown) { Clip[w.Slot] += ammo; return; }
            Ammo[w.Slot] += ammo;
            if (Clip[w.Slot] == 0) Refill(w.Slot);
        }

        public void LoseAll()
        {
            for (int i = 1; i < Owned.Length; i++) { Owned[i] = false; Ammo[i] = 0; Clip[i] = 0; }
            Current = 0;
        }

        void Refill(int slot)
        {
            var w = Weapons.All[slot];
            int need = w.Mag - Clip[slot];
            int take = Mathf.Min(need, Ammo[slot]);
            Clip[slot] += take;
            Ammo[slot] -= take;
        }

        public void Select(int slot)
        {
            if (slot < 0 || slot >= Owned.Length || !Owned[slot]) return;
            if (Current != slot) Sfx.Play2D(Sfx.Click, 0.4f);
            Current = slot;
            reloadT = 0f;
        }

        public string AmmoText()
        {
            var w = Def;
            if (w.Melee) return "";
            if (w.Thrown) return Clip[w.Slot].ToString();
            return reloadT > 0 ? "recarregando..." : $"{Clip[w.Slot]}  <color=#bbbbbb>/ {Ammo[w.Slot]}</color>";
        }

        public void Tick(bool canUse, bool inVehicle)
        {
            cooldown -= Time.deltaTime;
            var gm = GameManager.I;

            // trocar arma pela rodinha do mouse (a roda de armas fica no Tab, no HUD)
            float wheel = Input.GetAxis("Mouse ScrollWheel");
            if (canUse && Mathf.Abs(wheel) > 0.01f) Cycle(wheel > 0 ? 1 : -1);
            for (int k = 0; k < 7; k++) if (canUse && Input.GetKeyDown(KeyCode.Alpha1 + k)) Select(k);

            // dentro do carro só dá para atirar com armas curtas (drive-by)
            bool usable = canUse && (!inVehicle || (Def.Model == 1 && !Def.Melee));
            bool wantAim = usable && (Input.GetMouseButton(1) || ScriptedAim) && !Def.Melee;
            if (wantAim && !Aiming) StartAssist();
            Aiming = wantAim;

            if (reloadT > 0f)
            {
                reloadT -= Time.deltaTime;
                if (reloadT <= 0f) Refill(Current);
            }

            if (Aiming) DoAssist();
            UpdateCrosshairTarget();

            if (!usable) return;
            if (Input.GetKeyDown(KeyCode.R)) TryReload();

            bool fire = Def.Auto ? Input.GetMouseButton(0) : Input.GetMouseButtonDown(0);
            if (fire && cooldown <= 0f && reloadT <= 0f)
            {
                if (Def.Melee) { Melee(); return; }
                if (Def.Thrown) { Throw(); return; }
                if (Clip[Current] <= 0) { TryReload(); if (Clip[Current] <= 0 && Ammo[Current] <= 0) Sfx.Play2D(Sfx.Click, 0.5f); return; }
                Shoot();
            }
        }

        void Cycle(int dir)
        {
            int n = Owned.Length;
            for (int i = 1; i <= n; i++)
            {
                int s = (Current + dir * i + n * 2) % n;
                if (Owned[s] && (s == 0 || Clip[s] + Ammo[s] > 0)) { Select(s); return; }
            }
        }

        void TryReload()
        {
            var w = Def;
            if (w.Melee || w.Thrown || Clip[Current] >= w.Mag || Ammo[Current] <= 0 || reloadT > 0f) return;
            reloadT = w.Slot == 5 ? 2.2f : w.Slot == 3 ? 1.8f : 1.3f;
            Sfx.PlayAt(Sfx.Reload, player.Position, 0.6f);
        }

        Ray AimRay()
        {
            var cam = GameManager.I.Cam.Camera;
            return cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        }

        void Shoot()
        {
            var w = Def;
            var gm = GameManager.I;
            cooldown = w.Rate;
            Clip[Current]--;
            Ray ray = AimRay();
            Vector3 muzzle = player.MuzzlePosition;
            float spread = w.Spread * (Aiming ? 1f : 2.2f) * (player.Moving ? 1.4f : 1f);
            for (int i = 0; i < w.Pellets; i++)
            {
                Vector3 dir = Quaternion.Euler(Random.Range(-spread, spread), Random.Range(-spread, spread), 0) * ray.direction;
                // começa um pouco à frente da câmera para não acertar o próprio carro/jogador
                Vector3 origin = ray.origin + ray.direction * gm.Cam.Distance;
                Vector3 end = origin + dir * w.Range;
                if (Physics.Raycast(origin, dir, out var hit, w.Range, ~(1 << 2), QueryTriggerInteraction.Collide))
                {
                    end = hit.point;
                    var target = hit.collider.GetComponentInParent<IDamageable>();
                    if (target != null && !target.IsDead)
                    {
                        bool head = false;
                        var ped = hit.collider.GetComponentInParent<Ped>();
                        if (ped != null) head = hit.point.y - ped.transform.position.y > 1.5f;
                        target.TakeDamage(w.Damage * (head ? 2.5f : 1f), hit.point, dir, true, head);
                        if (ped != null) Fx.Blood(hit.point, dir); else Fx.Sparks(hit.point, hit.normal, 6);
                    }
                    else Fx.Sparks(hit.point, hit.normal, 5);
                }
                Fx.Tracer(muzzle, end);
            }
            Fx.MuzzleFlash(muzzle, ray.direction);
            Sfx.PlayAt(w.Sound, muzzle, 0.9f, 1f, 220f);
            gm.Cam.Recoil(w.Slot == 5 ? 3f : w.Slot == 3 ? 2.2f : 0.6f);
            gm.Wanted.Commit(Crime.Gunfire, player.Position);
            gm.Peds.Panic(player.Position, 45f);
            if (Clip[Current] == 0) TryReload();
        }

        void Melee()
        {
            cooldown = Def.Rate;
            player.Model.Punch();
            Sfx.PlayAt(Sfx.Punch, player.Position, 0.5f);
            Vector3 c = player.Position + Vector3.up * 1.1f + player.transform.forward * 1f;
            foreach (var col in Physics.OverlapSphere(c, 0.9f, ~(1 << 2), QueryTriggerInteraction.Collide))
            {
                var ped = col.GetComponentInParent<Ped>();
                if (ped != null && !ped.IsDead)
                {
                    ped.TakeDamage(Def.Damage, c, player.transform.forward, true);
                    ped.Knock(player.transform.forward * 3f + Vector3.up * 1.5f, ped.Health <= 0f);
                    break;
                }
            }
        }

        void Throw()
        {
            if (Clip[Current] <= 0) return;
            Clip[Current]--;
            cooldown = 1f;
            var ray = AimRay();
            var g = Gen.Prim(PrimitiveType.Sphere, null, player.Position + Vector3.up * 1.6f + player.transform.forward * 0.6f, Vector3.one * 0.18f, Mats.Get(new Color(0.2f, 0.3f, 0.15f)), true);
            g.name = "Granada";
            g.layer = 2;
            var rb = g.AddComponent<Rigidbody>();
            rb.mass = 0.4f;
            rb.linearVelocity = ray.direction * 17f + Vector3.up * 4f + (player.Vehicle != null ? player.Vehicle.Body.linearVelocity : Vector3.zero);
            g.AddComponent<GrenadeFuse>();
            if (Clip[Current] <= 0) Select(0);
        }

        // ---------- mira assistida ("Assisted aim" do GTA V: ajuda sutil, sem travar para sempre) ----------

        void StartAssist()
        {
            assistTarget = null;
            assistTransform = null;
            var cam = GameManager.I.Cam.Camera;
            float best = 12f;
            foreach (var ped in Ped.All)
            {
                if (ped == null || ped.IsDead || ped.Role == PedRole.Giver) continue;
                Vector3 chest = ped.transform.position + Vector3.up * 1.3f;
                Vector3 to = chest - cam.transform.position;
                float dist = to.magnitude;
                if (dist > Mathf.Min(Def.Range, 60f)) continue;
                float ang = Vector3.Angle(cam.transform.forward, to);
                // prioriza quem é ameaça (polícia/hostil)
                float score = ang - (ped.Hostile ? 4f : 0f);
                if (score < best && !Physics.Linecast(cam.transform.position + to.normalized * 3f, chest, ~(1 << 2), QueryTriggerInteraction.Ignore))
                {
                    best = score;
                    assistTarget = ped;
                    assistTransform = ped.transform;
                }
            }
            assistT = assistTarget != null ? 0.35f : 0f;
        }

        void DoAssist()
        {
            if (assistT <= 0f || assistTransform == null || assistTarget.IsDead) return;
            assistT -= Time.deltaTime;
            GameManager.I.Cam.LookToward(assistTransform.position + Vector3.up * 1.3f, 12f);
        }

        void UpdateCrosshairTarget()
        {
            TargetUnderCrosshair = false;
            if (GameManager.I.Cam == null) return;
            var ray = AimRay();
            Vector3 origin = ray.origin + ray.direction * GameManager.I.Cam.Distance;
            if (Physics.Raycast(origin, ray.direction, out var hit, Mathf.Max(Def.Range, 2f), ~(1 << 2), QueryTriggerInteraction.Collide))
            {
                var ped = hit.collider.GetComponentInParent<Ped>();
                TargetUnderCrosshair = ped != null && !ped.IsDead;
            }
        }
    }

    public class GrenadeFuse : MonoBehaviour
    {
        float t = 2.6f;
        void Update()
        {
            t -= Time.deltaTime;
            if (t <= 0f)
            {
                Explosion.Create(transform.position, 7f, 220f, true);
                Destroy(gameObject);
            }
        }
    }
}

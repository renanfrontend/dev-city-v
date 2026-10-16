using System.Collections.Generic;
using UnityEngine;

namespace DevCity
{
    public enum PedRole { Civilian, Cop, Swat, Gang, Clerk, Target, Giver }
    public enum PedState { Wander, Idle, Flee, Cower, HandsUp, Calling, Fight, ReturnToCar, Dead }

    /// <summary>
    /// Pedestre/NPC: anda pelas calçadas (e atravessa na faixa), foge de tiros, se encolhe,
    /// levanta as mãos, liga para a polícia, revida, atira, prende o jogador (policial),
    /// é atropelado e deixa dinheiro cair quando morre.
    /// </summary>
    public class Ped : MonoBehaviour, IDamageable
    {
        public static readonly List<Ped> All = new List<Ped>();

        public PedRole Role;
        public PedState State = PedState.Wander;
        public float Health = 100f, Armor;
        public bool Hostile;
        public bool Persistent;
        public WeaponDef Weapon;
        public float Accuracy = 0.3f;
        public VehicleController Car;           // viatura (policiais)
        public System.Action<Ped> OnDeath;
        public Humanoid Model { get; private set; }
        public Outfit Outfit { get; private set; }

        public bool IsDead => State == PedState.Dead;
        public bool Knocked => knockT > 0f;
        public bool IsCop => Role == PedRole.Cop || Role == PedRole.Swat;
        public float DeadTime { get; private set; }
        public Vector3 Chest => transform.position + Vector3.up * 1.3f;

        Vector3Int corner, prevCorner;
        Vector3 target, threat, kVel;
        float speed, stateT, fireT, knockT, arrestT, thinkT, strafeT;
        int strafeDir = 1;
        bool dieAfterKnock;

        // ===================== criação =====================

        public static Ped Spawn(Vector3 pos, PedRole role, Outfit? outfit = null)
        {
            Weapons.Init();
            var go = new GameObject(role == PedRole.Civilian ? "Pedestre" : role.ToString());
            go.transform.position = pos;
            var p = go.AddComponent<Ped>();
            p.Role = role;
            p.Outfit = outfit ?? (role == PedRole.Cop ? Outfit.Cop() : role == PedRole.Swat ? Outfit.Swat() : role == PedRole.Gang ? Outfit.Gang() : Outfit.Random());
            p.Model = Humanoid.Create(go.transform, p.Outfit);
            var col = go.AddComponent<CapsuleCollider>();
            col.isTrigger = true;
            col.center = new Vector3(0, 0.9f, 0);
            col.height = 1.8f;
            col.radius = 0.32f;
            switch (role)
            {
                case PedRole.Cop: p.Health = 140f; p.Weapon = Weapons.Pistol; p.Accuracy = 0.32f; break;
                case PedRole.Swat: p.Health = 160f; p.Armor = 100f; p.Weapon = Weapons.Rifle; p.Accuracy = 0.4f; break;
                case PedRole.Gang: p.Health = 110f; p.Weapon = Random.value < 0.4f ? Weapons.Smg : Weapons.Pistol; p.Accuracy = 0.22f; p.Hostile = true; break;
                case PedRole.Clerk: p.State = PedState.Idle; p.Persistent = true; break;
                case PedRole.Giver: p.State = PedState.Idle; p.Persistent = true; break;
            }
            if (role == PedRole.Civilian || role == PedRole.Target) p.corner = CityGenerator.NearestCorner(pos);
            p.prevCorner = p.corner;
            p.target = CityGenerator.Corner(p.corner.x, p.corner.y, p.corner.z);
            if (p.Hostile || role == PedRole.Cop || role == PedRole.Swat) p.State = p.Hostile ? PedState.Fight : PedState.Wander;
            All.Add(p);
            return p;
        }

        void OnDestroy() { All.Remove(this); }

        // ===================== comportamento =====================

        void Update()
        {
            var gm = GameManager.I;
            if (gm == null) return;
            float dt = Time.deltaTime;
            if (knockT > 0f) { KnockUpdate(dt); return; }
            if (State == PedState.Dead) return;

            stateT -= dt;
            Model.HandsUp = State == PedState.HandsUp;
            Model.Cower = State == PedState.Cower;
            Model.Phone = State == PedState.Calling;
            Model.Aim = State == PedState.Fight && Weapon != null && !Weapon.Melee;
            Model.WeaponModel = (State == PedState.Fight || Role == PedRole.Cop && gm.Wanted.Stars > 0) && Weapon != null ? Weapon.Model : 0;

            switch (State)
            {
                case PedState.Wander: Wander(dt, gm); break;
                case PedState.Idle:
                    speed = 0f;
                    if ((Role == PedRole.Civilian || Role == PedRole.Target) && stateT <= 0f) State = PedState.Wander;
                    break;
                case PedState.Flee: Flee(dt); break;
                case PedState.Cower:
                case PedState.HandsUp:
                    speed = 0f;
                    if (stateT <= 0f) { if (Role == PedRole.Clerk) State = PedState.Idle; else Flee(gm.PlayerPos, 10f); }
                    break;
                case PedState.Calling:
                    speed = 0f;
                    if (stateT <= 0f)
                    {
                        gm.Wanted.CivilianReport(pendingCrime, pendingPos);
                        Flee(gm.PlayerPos, 8f);
                    }
                    break;
                case PedState.Fight: Fight(dt, gm); break;
                case PedState.ReturnToCar: ReturnToCar(dt, gm); break;
            }
            Model.MoveSpeed = speed;
        }

        void Wander(float dt, GameManager gm)
        {
            if (IsCop && gm.Wanted.Stars > 0 && Car == null) { State = PedState.Fight; return; }
            speed = 1.35f;
            if (Gen.FlatDist(transform.position, target) < 0.6f)
            {
                // chegou no canto: segue pela calçada ou atravessa
                var options = CityGenerator.CornerNeighbors(corner);
                Vector3Int next = options[Random.Range(0, options.Count)];
                if (next == prevCorner && options.Count > 1) next = options[Random.Range(0, options.Count)];
                prevCorner = corner;
                corner = next;
                target = CityGenerator.Corner(corner.x, corner.y, corner.z);
                if (Random.value < 0.08f) { State = PedState.Idle; stateT = Random.Range(2f, 5f); }
            }
            // pausa curta e volta a andar
            if (State == PedState.Idle && stateT <= 0f) State = PedState.Wander;
            MoveToward(target, speed, dt, false);
        }

        void Flee(float dt)
        {
            speed = 5.4f;
            Vector3 away = Gen.Flat(transform.position - threat);
            if (away.sqrMagnitude < 0.01f) away = transform.forward;
            MoveToward(transform.position + away.normalized * 5f, speed, dt, true);
            if (stateT <= 0f && Gen.FlatDist(transform.position, threat) > 30f)
            {
                State = Role == PedRole.Clerk ? PedState.Idle : PedState.Wander;
                corner = CityGenerator.NearestCorner(transform.position);
                target = CityGenerator.Corner(corner.x, corner.y, corner.z);
            }
        }

        void Fight(float dt, GameManager gm)
        {
            var player = gm.Player;
            Vector3 pp = gm.PlayerPos;
            float d = Gen.FlatDist(transform.position, pp);
            bool canSee = HasLineOfSight(pp + Vector3.up * 1.2f);
            int stars = gm.Wanted.Stars;

            if (IsCop)
            {
                if (stars == 0) { State = Car != null ? PedState.ReturnToCar : PedState.Wander; return; }
                // jogador fugiu de carro: volta para a viatura
                if (player.Vehicle != null && d > 45f && Car != null && !Car.Destroyed) { State = PedState.ReturnToCar; return; }
            }

            bool shootAllowed = !IsCop || stars >= 2 || Time.time - player.LastAggro < 6f || player.Weapons.Aiming;
            bool tryArrest = IsCop && !shootAllowed && player.Vehicle == null && !player.IsDead;

            float want = tryArrest ? 1.6f : (Weapon != null && !Weapon.Melee ? 11f : 1.4f);
            Vector3 face = Gen.Flat(pp - transform.position);
            if (face.sqrMagnitude > 0.01f) transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(face), dt * 8f);

            if (d > want || !canSee)
            {
                speed = d > 25f ? 5.6f : 4.2f;
                MoveToward(pp, speed, dt, true);
            }
            else
            {
                // atirando: anda de lado de vez em quando (mais difícil de acertar)
                strafeT -= dt;
                if (strafeT <= 0f) { strafeT = Random.Range(1f, 2.5f); strafeDir = Random.value < 0.5f ? -1 : 1; }
                speed = tryArrest ? 0f : 1.6f;
                if (!tryArrest) MoveToward(transform.position + transform.right * strafeDir * 2f, speed, dt, true, false);
            }

            Model.AimPitch = Mathf.Clamp((pp.y - transform.position.y) * 5f, -30f, 30f);

            if (tryArrest)
            {
                if (d < 2.4f)
                {
                    arrestT += dt;
                    gm.Hud.Help("<b>Polícia:</b> Mãos para cima! Parado!", 0.2f);
                    if (arrestT > 1.6f) player.Arrest();
                }
                else arrestT = Mathf.Max(0f, arrestT - dt);
                return;
            }

            fireT -= dt;
            if (shootAllowed && canSee && d < (Weapon != null ? Mathf.Min(Weapon.Range, 70f) : 1.6f) && fireT <= 0f)
            {
                if (Weapon == null || Weapon.Melee)
                {
                    fireT = 0.9f;
                    Model.Punch();
                    if (d < 1.6f) player.TakeDamage(9f, Chest, transform.forward, false);
                }
                else
                {
                    fireT = Weapon.Auto ? Weapon.Rate * Random.Range(2.5f, 5f) : Weapon.Rate * Random.Range(4f, 7f);
                    ShootAtPlayer(gm, d);
                }
            }
        }

        void ShootAtPlayer(GameManager gm, float dist)
        {
            var player = gm.Player;
            Vector3 from = Model.Muzzle.position;
            Vector3 aim = gm.PlayerPos + Vector3.up * (player.Vehicle != null ? 1f : 1.2f);
            float acc = Accuracy * (player.Moving || player.Vehicle != null && player.Vehicle.Speed > 5f ? 0.55f : 1f) * (dist > 30f ? 0.6f : 1f);
            if (gm.Wanted.Stars >= 4) acc *= 1.25f;
            bool hit = Random.value < acc;
            Vector3 end = hit ? aim : aim + Random.insideUnitSphere * 2.5f;
            Fx.Tracer(from, end);
            Fx.MuzzleFlash(from, (aim - from).normalized);
            Sfx.PlayAt(Weapon.Sound, from, 0.75f, 1f, 180f);
            if (!hit) { if (Random.value < 0.3f) Fx.Sparks(end, Vector3.up, 3); return; }
            float dmg = Weapon.Damage * 0.55f;
            if (player.Vehicle != null)
            {
                player.Vehicle.TakeDamage(dmg * 1.5f, end, (end - from).normalized, false);
                if (Random.value < 0.35f) player.TakeDamage(dmg * 0.6f, end, (end - from).normalized, false);
            }
            else player.TakeDamage(dmg, end, (end - from).normalized, false);
        }

        void ReturnToCar(float dt, GameManager gm)
        {
            if (Car == null || Car.Destroyed) { State = gm.Wanted.Stars > 0 ? PedState.Fight : PedState.Wander; return; }
            if (gm.Wanted.Stars > 0 && gm.Player.Vehicle == null && Gen.FlatDist(transform.position, gm.PlayerPos) < 30f) { State = PedState.Fight; return; }
            Vector3 door = Car.DoorPoint(true);
            speed = 5f;
            MoveToward(door, speed, dt, true);
            if (Gen.FlatDist(transform.position, door) < 1.6f)
            {
                gm.Police.Reboard(this, Car);
            }
        }

        // ===================== movimento =====================

        void MoveToward(Vector3 dest, float spd, float dt, bool avoid, bool rotate = true)
        {
            Vector3 dir = Gen.Flat(dest - transform.position);
            if (dir.sqrMagnitude < 0.0004f) return;
            dir.Normalize();
            if (avoid)
            {
                Vector3 eye = transform.position + Vector3.up * 0.8f;
                if (Physics.Raycast(eye, dir, out var hit, 1.4f, ~(1 << 2), QueryTriggerInteraction.Ignore) && hit.rigidbody == null)
                {
                    Vector3 alt = Vector3.Cross(Vector3.up, dir) * (Vector3.Dot(hit.normal, Vector3.Cross(Vector3.up, dir)) > 0 ? 1f : -1f);
                    dir = (alt + hit.normal * 0.5f).normalized;
                }
            }
            if (rotate) transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), dt * 10f);
            Vector3 p = transform.position + dir * spd * dt;
            // acompanha o chão (rua 0 m, calçada 0,2 m)
            if (Physics.Raycast(p + Vector3.up * 1.2f, Vector3.down, out var g, 3f, ~(1 << 2), QueryTriggerInteraction.Ignore) && g.rigidbody == null)
                p.y = Mathf.Lerp(p.y, g.point.y, dt * 15f);
            transform.position = p;
        }

        bool HasLineOfSight(Vector3 point)
        {
            Vector3 eye = transform.position + Vector3.up * 1.6f;
            return !Physics.Linecast(eye, point, ~(1 << 2), QueryTriggerInteraction.Ignore);
        }

        // ===================== reações =====================

        Crime pendingCrime;
        Vector3 pendingPos;

        /// <summary>Tiro, explosão ou briga por perto.</summary>
        public void React(Vector3 source)
        {
            if (IsDead || Role == PedRole.Giver || Hostile || IsCop || State == PedState.Calling) return;
            if (Role == PedRole.Clerk) { State = PedState.Cower; stateT = 6f; return; }
            float r = Random.value;
            if (r < 0.2f) { State = PedState.Cower; stateT = Random.Range(3f, 6f); threat = source; }
            else Flee(source, Random.Range(6f, 12f));
        }

        public void Flee(Vector3 from, float duration)
        {
            if (IsDead) return;
            State = PedState.Flee;
            threat = from;
            stateT = duration;
        }

        public void HandsUp(float duration)
        {
            if (IsDead || Hostile || IsCop) return;
            State = PedState.HandsUp;
            stateT = duration;
        }

        /// <summary>Viu um crime: liga para a polícia (a estrela só vem depois que a ligação termina).</summary>
        public void CallPolice(Crime c, Vector3 pos)
        {
            if (IsDead || State == PedState.Calling || Hostile || IsCop) return;
            pendingCrime = c;
            pendingPos = pos;
            State = PedState.Calling;
            stateT = Random.Range(3f, 5.5f);
            var dir = Gen.Flat(pos - transform.position);
            if (dir.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(dir);
        }

        public void BecomeHostile()
        {
            if (IsDead) return;
            Hostile = true;
            if (Weapon == null) Weapon = Random.value < 0.3f ? Weapons.Pistol : Weapons.Fists;
            State = PedState.Fight;
        }

        public void TakeDamage(float amount, Vector3 point, Vector3 dir, bool fromPlayer, bool headshot = false)
        {
            if (IsDead || Role == PedRole.Giver) return;
            var gm = GameManager.I;
            if (Armor > 0f && !headshot) { float a = Mathf.Min(Armor, amount); Armor -= a; amount -= a * 0.7f; }
            Health -= amount;
            if (fromPlayer)
            {
                if (IsCop) gm.Wanted.Commit(Crime.AttackCop, transform.position);
                else if (!Hostile) gm.Wanted.Commit(Crime.Assault, transform.position);
            }
            if (Health <= 0f)
            {
                Knock(dir * 2f + Vector3.up * 1f, true);
                Kill(fromPlayer);
                return;
            }
            if (Role == PedRole.Gang || IsCop) { State = PedState.Fight; return; }
            // civil: alguns revidam, a maioria foge
            if (fromPlayer && Role == PedRole.Civilian && Random.value < 0.12f) BecomeHostile();
            else Flee(gm.PlayerPos, 10f);
        }

        public void HitByVehicle(VehicleController v, Vector3 rel)
        {
            if (Role == PedRole.Giver) return;
            float s = rel.magnitude;
            bool byPlayer = v != null && v.PlayerDriving;
            float dmg = s * 11f;
            Health -= dmg;
            bool dead = Health <= 0f;
            Knock(rel * 0.75f + Vector3.up * Mathf.Min(s * 0.45f, 8f), dead);
            if (byPlayer) GameManager.I.Wanted.Commit(Crime.HitPed, transform.position);
            if (dead && State != PedState.Dead) Kill(byPlayer);
            else if (!dead) Flee(transform.position - rel, 8f);
        }

        public void Kill(bool byPlayer)
        {
            if (State == PedState.Dead) return;
            State = PedState.Dead;
            Health = 0f;
            DeadTime = Time.time;
            speed = 0f;
            Model.MoveSpeed = 0f;
            Model.HandsUp = Model.Cower = Model.Phone = Model.Aim = false;
            if (knockT <= 0f) Knock(Vector3.up * 0.5f - transform.forward, true);
            Sfx.PlayAt(Sfx.Punch, transform.position, 0.4f, 0.7f);
            // dinheiro no chão, como no GTA V
            if (Role == PedRole.Civilian || Role == PedRole.Gang || Role == PedRole.Target)
            {
                int cash = Role == PedRole.Gang ? Random.Range(40, 180) : Random.Range(5, 70);
                if (Random.value < 0.75f) Pickup.Create(transform.position + Vector3.up * 0.1f + transform.right * 0.6f, new Color(0.3f, 1f, 0.4f), cash);
            }
            else if (IsCop && Random.value < 0.5f)
            {
                var p = Pickup.Create(transform.position + transform.right * 0.6f, new Color(1f, 0.7f, 0.2f), 0, "pacote");
                var w = Weapon;
                p.OnCollect = () => { GameManager.I.Player.Weapons.Give(w, w.AmmoPack / 2 + 6); GameManager.I.Hud.Notify($"Pegou <b>{w.Name}</b>."); };
            }
            ReportDeath(byPlayer);
            OnDeath?.Invoke(this);
        }

        public void ReportDeath(bool byPlayer)
        {
            if (!byPlayer) return;
            var gm = GameManager.I;
            if (IsCop) gm.Wanted.Commit(Crime.KillCop, transform.position);
            else gm.Wanted.Commit(Crime.KillCivilian, transform.position);
            gm.Peds.Panic(transform.position, 25f);
        }

        /// <summary>Empurrão/atropelamento: voa, cai e (se não morreu) levanta.</summary>
        public void Knock(Vector3 vel, bool stayDown)
        {
            kVel = vel;
            knockT = stayDown ? 2.5f : 1.6f;
            dieAfterKnock = stayDown;
        }

        void KnockUpdate(float dt)
        {
            knockT -= dt;
            kVel.y -= 20f * dt;
            Vector3 p = transform.position + kVel * dt;
            float ground = transform.position.y;
            if (Physics.Raycast(transform.position + Vector3.up * 1f, Vector3.down, out var g, 4f, ~(1 << 2), QueryTriggerInteraction.Ignore) && g.rigidbody == null)
                ground = g.point.y;
            if (p.y <= ground)
            {
                p.y = ground;
                kVel.y = Mathf.Abs(kVel.y) > 4f ? -kVel.y * 0.25f : 0f;
                kVel.x *= 1f - 5f * dt; kVel.z *= 1f - 5f * dt;
            }
            // não atravessa paredes
            Vector3 flatMove = Gen.Flat(p - transform.position);
            if (flatMove.sqrMagnitude > 0.0001f && Physics.Raycast(transform.position + Vector3.up * 0.5f, flatMove.normalized, flatMove.magnitude + 0.3f, ~(1 << 2), QueryTriggerInteraction.Ignore))
            { p.x = transform.position.x; p.z = transform.position.z; kVel.x = kVel.z = 0f; }
            transform.position = p;

            float fall = dieAfterKnock || knockT > 0.5f ? 88f : Mathf.Lerp(0f, 88f, knockT / 0.5f);
            Model.transform.localRotation = Quaternion.Slerp(Model.transform.localRotation, Quaternion.Euler(-fall, 0, 0), dt * 10f);
            Model.transform.localPosition = new Vector3(0, fall > 45f ? 0.2f : 0f, 0);
            if (knockT <= 0f)
            {
                knockT = 0f;
                if (State == PedState.Dead) { knockT = 0f; return; }
                Model.transform.localRotation = Quaternion.identity;
                Model.transform.localPosition = Vector3.zero;
            }
        }
    }
}

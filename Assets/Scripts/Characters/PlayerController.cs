using System.Collections;
using UnityEngine;

namespace DevCity
{
    /// <summary>
    /// Jogador em terceira pessoa: corre (estamina), pula, nada, entra/sai/rouba carros, atira,
    /// leva dano (armadura primeiro) e regenera a vida até 50% como no GTA V.
    /// </summary>
    public class PlayerController : MonoBehaviour, IDamageable
    {
        public const float MaxHealth = 200f;
        public const float MaxArmor = 100f;

        public float Health = MaxHealth;
        public float Armor;
        public float Stamina = 100f;
        public float LastAggro = -99f;
        // usados pelo teste automático (AutoTest) no lugar do teclado
        public static Vector2? ScriptedMove;
        public static Vector3? ScriptedDrive; // acelerador, direção, freio de mão (>0,5)      // último tiro/agressão (polícia só prende quem está "manso")
        public Outfit Outfit = Outfit.Player();

        public CharacterController CC { get; private set; }
        public Humanoid Model { get; private set; }
        public PlayerWeapons Weapons { get; private set; }
        public VehicleController Vehicle { get; private set; }
        public bool IsDead => Health <= 0f;
        public bool Swimming { get; private set; }
        public bool Moving { get; private set; }
        public bool Busy => entering;
        public Vector3 Position => Vehicle != null ? Vehicle.transform.position : transform.position;
        public Vector3 Velocity => Vehicle != null ? Vehicle.Body.linearVelocity : velocity;
        public Vector3 MuzzlePosition => Model.Muzzle.position;

        float vy, lastDamage = -99f, knockT;
        Vector3 knockVel, velocity;
        bool entering, wasGrounded;
        float fallSpeed;

        public static PlayerController Create(Vector3 pos)
        {
            var go = new GameObject("Jogador");
            go.layer = 2; // Ignore Raycast: tiros e câmera não acertam o próprio jogador
            go.transform.position = pos;
            var p = go.AddComponent<PlayerController>();
            p.CC = go.AddComponent<CharacterController>();
            p.CC.height = 1.8f;
            p.CC.radius = 0.35f;
            p.CC.center = new Vector3(0, 0.92f, 0);
            p.CC.stepOffset = 0.45f;
            p.CC.slopeLimit = 55f;
            p.CC.skinWidth = 0.04f;
            p.Model = Humanoid.Create(go.transform, p.Outfit);
            Gen.SetLayerRecursive(p.Model.gameObject, 2);
            p.Weapons = go.AddComponent<PlayerWeapons>();
            p.Weapons.Init(p);
            go.AddComponent<AudioListener>().enabled = false; // o listener fica na câmera
            return p;
        }

        public void SetOutfit(Outfit o)
        {
            Outfit = o;
            Model.SetOutfit(o);
        }

        /// <summary>Troca o boneco (usado na troca de protagonista). Usa o modelo da Mixamo se existir.</summary>
        public void SetCharacter(string modelName, Outfit o)
        {
            Outfit = o;
            if (Model != null) Destroy(Model.gameObject);
            Model = Humanoid.Create(transform, o, modelName);
            Gen.SetLayerRecursive(Model.gameObject, 2);
        }

        void Update()
        {
            var gm = GameManager.I;
            if (gm == null || IsDead) return;
            bool playing = gm.Playing;
            Weapons.Tick(playing && !entering && knockT <= 0f && !Swimming, Vehicle != null);
            Model.WeaponModel = Weapons.Def.Melee || Weapons.Def.Thrown || Swimming ? 0 : Weapons.Def.Model;
            Model.Aim = Weapons.Aiming;
            Model.AimPitch = -gm.Cam.Pitch;
            if (!playing || entering) { if (Vehicle == null) Model.MoveSpeed = 0f; return; }

            if (Vehicle != null) { DriveInput(); return; }
            if (knockT > 0f) { KnockUpdate(); return; }

            OnFoot(gm);
            if (Input.GetKeyDown(KeyCode.F) && !Swimming) TryEnterVehicle();

            // regeneração até metade da vida (5 s sem levar dano)
            if (Health < MaxHealth * 0.5f && Time.time - lastDamage > 5f)
                Health = Mathf.Min(MaxHealth * 0.5f, Health + 8f * Time.deltaTime);
        }

        // ===================== a pé =====================

        void OnFoot(GameManager gm)
        {
            float dt = Time.deltaTime;
            Vector2 input = new Vector2(
                (Input.GetKey(KeyCode.D) ? 1 : 0) - (Input.GetKey(KeyCode.A) ? 1 : 0),
                (Input.GetKey(KeyCode.W) ? 1 : 0) - (Input.GetKey(KeyCode.S) ? 1 : 0));
            if (ScriptedMove.HasValue) input = ScriptedMove.Value;
            if (input.sqrMagnitude > 1f) input.Normalize();

            Quaternion yaw = Quaternion.Euler(0, gm.Cam.Yaw, 0);
            Vector3 move = yaw * new Vector3(input.x, 0, input.y);
            bool sprint = Input.GetKey(KeyCode.LeftShift) && Stamina > 1f && !Weapons.Aiming && input.sqrMagnitude > 0.1f;
            float speed = Weapons.Aiming ? 2.6f : sprint ? 7.6f : 4.6f;

            // água: nada devagar, sem armas
            CheckWater(gm);
            if (Swimming) speed = sprint ? 3.8f : 2.4f;

            Stamina = sprint ? Mathf.Max(0f, Stamina - 14f * dt) : Mathf.Min(100f, Stamina + 22f * dt);
            if (sprint && Stamina <= 0f && Swimming) TakeDamage(8f * dt, transform.position, Vector3.zero, false);

            Vector3 horiz = move * speed;
            bool grounded = CC.isGrounded;
            if (Swimming)
            {
                float targetY = CityGenerator.WATER_Y - 1.25f;
                vy = (targetY - transform.position.y) * 4f;
                // perto da areia: sobe para a praia
                if (transform.position.z > CityGenerator.WATER_Z - 2.5f && move.z > 0.3f)
                    Teleport(new Vector3(transform.position.x, 0.1f, CityGenerator.WATER_Z + 1.2f));
            }
            else
            {
                if (grounded && vy < 0f)
                {
                    if (!wasGrounded && fallSpeed < -15f) TakeDamage((-fallSpeed - 15f) * 9f, transform.position, Vector3.down, false);
                    vy = -2f;
                }
                if (grounded && Input.GetKeyDown(KeyCode.Space) && !Weapons.Aiming) vy = 6.2f;
                vy -= 20f * dt;
                fallSpeed = vy;
            }
            wasGrounded = grounded;

            CC.Move((horiz + Vector3.up * vy) * dt);
            velocity = horiz;
            Moving = input.sqrMagnitude > 0.05f;

            // rotação: mirando olha para a câmera, senão para onde anda
            Vector3 face = Weapons.Aiming ? yaw * Vector3.forward : move;
            if (face.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(face), dt * (Weapons.Aiming ? 20f : 12f));

            Model.MoveSpeed = horiz.magnitude;
            Model.Cower = false;
        }

        void CheckWater(GameManager gm)
        {
            var p = transform.position;
            bool inWater = gm.City.IsWater(p) && p.y < CityGenerator.WATER_Y + 0.2f;
            if (inWater != Swimming)
            {
                Swimming = inWater;
                if (Swimming) { Weapons.Select(0); gm.Hud.Help("Você está nadando. Volte para a areia.", 3f); }
            }
        }

        // ===================== veículos =====================

        void TryEnterVehicle()
        {
            VehicleController best = null;
            float bd = 4.2f;
            foreach (var v in VehicleController.All)
            {
                if (v == null || v.Destroyed || v.IsAircraft) continue;
                float d = Vector3.Distance(transform.position, v.transform.position);
                if (d < bd) { bd = d; best = v; }
            }
            if (best != null) StartCoroutine(EnterRoutine(best));
        }

        public void ForceEnter(VehicleController v) { if (Vehicle == null && !entering) StartCoroutine(EnterRoutine(v)); }

        IEnumerator EnterRoutine(VehicleController v)
        {
            entering = true;
            var gm = GameManager.I;
            var act = Model.Rigged && !v.TwoWheeler ? CarActions.Enter : null; // na moto, monta rápido
            // se estava caído no chão, levanta antes de entrar
            knockT = 0f;
            Model.transform.localRotation = Quaternion.identity;
            Model.transform.localPosition = Vector3.zero;

            // Onde ficar na porta para a animação terminar em cima do banco:
            // o corpo anda act.Move e gira act.Turn; então começamos "girado ao contrário" e recuados.
            float carYaw = v.transform.eulerAngles.y;
            Quaternion startRot = Quaternion.Euler(0f, carYaw - (act != null ? act.Turn : 0f), 0f);
            Vector3 door = v.DoorPoint(true);
            if (act != null)
            {
                Vector3 seat = v.Seat.position;
                door = seat - startRot * act.Move * Model.BodyScale;
            }
            door.y = transform.position.y;

            Vector3 start = transform.position;
            Quaternion startLook = transform.rotation;
            float t = 0f;
            // anda até a porta
            while (t < 0.3f)
            {
                t += Time.deltaTime;
                CC.enabled = false;
                transform.position = Vector3.Lerp(start, door, t / 0.3f);
                if (act != null) transform.rotation = Quaternion.Slerp(startLook, startRot, t / 0.3f);
                Model.MoveSpeed = 3f;
                yield return null;
            }

            bool hadDriver = v.HasNpcDriver;
            if (hadDriver) v.EjectDriver(transform.position, true);
            // roubar carro é crime (se alguém vir); roubar viatura sempre dá estrela
            if (v.Kind == VehicleKind.Police || v.Kind == VehicleKind.Swat) gm.Wanted.Commit(Crime.StealPoliceCar, transform.position);
            else if (hadDriver || !v.OwnedByPlayer) gm.Wanted.Commit(Crime.CarTheft, transform.position);

            // animação da Mixamo "Entering Car" (se o personagem tiver)
            if (act != null && Model.PlayAction("EntrandoNoCarro"))
            {
                Model.MoveSpeed = 0f;
                // guardamos a posição em relação ao carro: se ele for empurrado, o jogador vai junto
                Vector3 local = v.transform.InverseTransformPoint(transform.position);
                float seatY = v.transform.InverseTransformPoint(v.Seat.position).y;
                float yawOff = Mathf.DeltaAngle(carYaw, startRot.eulerAngles.y);
                float timeout = act.len + 2f;
                float logT = 0f;
                while (timeout > 0f && !v.Destroyed)
                {
                    timeout -= Time.deltaTime;
                    v.SetInput(0f, 0f, true); // carro freado enquanto o jogador entra
                    float k = Model.ActionProgress("EntrandoNoCarro");
                    if (AutoTest.Running && (logT += Time.deltaTime) > 0.4f) { logT = 0f; Debug.Log($"[Carro] entrando: k={k:0.00} carro {v.Speed * 3.6f:0} km/h em {v.transform.position}"); }
                    if (k >= 0.96f || (k < 0f && timeout < act.len)) break;
                    // na hora de sentar, o corpo sobe do chão até a altura do banco
                    var l = local;
                    l.y = Mathf.Lerp(local.y, seatY, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.45f, 0.9f, k)));
                    transform.position = v.transform.TransformPoint(l);
                    transform.rotation = Quaternion.Euler(0f, v.transform.eulerAngles.y + yawOff, 0f);
                    yield return null;
                }
                if (v.Destroyed) { entering = false; Teleport(transform.position); yield break; }
                lastHipsGap = Model.Hips.position;
            }
            else lastHipsGap = Vector3.zero;

            Vehicle = v;
            v.SetPlayerDriver(true);
            CC.enabled = false;
            transform.SetParent(v.Seat, false);
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
            Model.Sitting = true;
            Model.MoveSpeed = 0f;
            Moving = false;
            entering = false;
            gm.Hud.ShowVehicleName(v.DisplayName);
            if (!Weapons.Def.Melee && Weapons.Def.Model != 1) Weapons.Select(Weapons.Owned[1] ? 1 : 0);
            if (lastHipsGap != Vector3.zero)
            {
                // corta direto para "sentado" e mede o "pulo" do quadril (quanto menor, melhor o encaixe)
                Model.transform.localScale = Vector3.one * 0.9f;
                Model.SnapTo("Sentado");
                Debug.Log($"[Carro] entrou com animação; o quadril pulou {Vector3.Distance(lastHipsGap, Model.Hips.position):0.00} m ao sentar");
            }
        }

        Vector3 lastHipsGap;

        /// <summary>
        /// Sai do carro com a animação "Exiting Car": o corpo começa no banco, sai pela porta e
        /// termina em pé do lado de fora. No fim, o objeto do jogador vai para onde o corpo está.
        /// </summary>
        IEnumerator ExitRoutine(VehicleController v, CarActions.Action act)
        {
            entering = true;
            var gm = GameManager.I;
            float seatY = v.transform.InverseTransformPoint(v.Seat.position).y;
            Vector3 local = v.transform.InverseTransformPoint(v.Seat.position);
            float groundY = v.transform.InverseTransformPoint(v.DoorPoint(true)).y - 0.15f;

            transform.SetParent(null, true);
            transform.localScale = Vector3.one;
            v.SetPlayerDriver(false);
            v.SetInput(0f, 0f, true);
            Vehicle = null;
            gm.Hud.ShowVehicleName(null);
            Model.Sitting = false;
            Model.MoveSpeed = 0f;
            bool played = Model.PlayAction("SaindoDoCarro", 0.1f);

            float timeout = act.len + 2f, logT = 0f;
            while (timeout > 0f && !v.Destroyed)
            {
                timeout -= Time.deltaTime;
                float k = Model.ActionProgress("SaindoDoCarro");
                if (AutoTest.Running && (logT += Time.deltaTime) > 0.4f) { logT = 0f; Debug.Log($"[Carro] saindo: tocou={played} k={k:0.00} pos={transform.position}"); }
                if (k >= 0.96f || (k < 0f && timeout < act.len)) break;
                // ao pôr os pés para fora, desce da altura do banco até o chão
                var l = local;
                l.y = Mathf.Lerp(seatY, groundY, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.15f, 0.55f, k)));
                transform.position = v.transform.TransformPoint(l);
                transform.rotation = Quaternion.Euler(0f, v.transform.eulerAngles.y, 0f);
                yield return null;
            }

            // o jogador "alcança" o corpo: vai para onde o quadril está e olha para onde ele olha
            Vector3 hips = Model.Hips.position;
            Vector3 p = new Vector3(hips.x, transform.position.y, hips.z);
            transform.rotation = Quaternion.Euler(0f, transform.eulerAngles.y + act.Turn, 0f);
            Gen.SetLayerRecursive(gameObject, 2);
            Teleport(p + Vector3.up * 0.05f);
            Model.SnapTo("Movimento");
            Debug.Log($"[Carro] saiu com animação; o quadril pulou {Vector3.Distance(hips, Model.Hips.position):0.00} m");
            entering = false;
        }

        void DriveInput()
        {
            var v = Vehicle;
            if (v == null) return;
            float throttle = (Input.GetKey(KeyCode.W) ? 1f : 0f) - (Input.GetKey(KeyCode.S) ? 1f : 0f);
            float steer = (Input.GetKey(KeyCode.D) ? 1f : 0f) - (Input.GetKey(KeyCode.A) ? 1f : 0f);
            bool hb = Input.GetKey(KeyCode.Space);
            if (ScriptedDrive.HasValue) { throttle = ScriptedDrive.Value.x; steer = ScriptedDrive.Value.y; hb = ScriptedDrive.Value.z > 0.5f; }
            v.SetInput(throttle, steer, hb);
            if (Input.GetKeyDown(KeyCode.E)) v.Horn();
            if (Input.GetKeyDown(KeyCode.H)) v.ToggleLights();
            if (Input.GetKeyDown(KeyCode.G) && (v.Kind == VehicleKind.Police || v.Kind == VehicleKind.Swat)) v.SirenOn = !v.SirenOn;

            if (v.Destroyed && v.Burning == false) { ExitVehicle(false); return; }
            if (v.Sunk) { ExitVehicle(false); return; }
            if (v.TwoWheeler && v.transform.up.y < 0.4f) { ExitVehicle(true); return; } // a moto tombou: o piloto cai
            if (Input.GetKeyDown(KeyCode.F)) ExitVehicle(v.Body.linearVelocity.magnitude > 7f, true);
        }

        public void ExitVehicle(bool bail, bool animate = false)
        {
            var v = Vehicle;
            if (v == null) return;
            Vector3 vel = v.Body.linearVelocity;
            Vector3 p = v.DoorPoint(true);
            bool leftBlocked = Physics.CheckCapsule(p + Vector3.up * 0.5f, p + Vector3.up * 1.5f, 0.3f, ~(1 << 2), QueryTriggerInteraction.Ignore);
            // saída calma pela porta do motorista: usa a animação, se houver
            var act = Model.Rigged && !v.TwoWheeler ? CarActions.Exit : null;
            if (animate && !bail && !leftBlocked && act != null && !v.Destroyed && !v.Sunk && vel.magnitude < 2f && !IsDead)
            {
                StartCoroutine(ExitRoutine(v, act));
                return;
            }
            if (leftBlocked) p = v.DoorPoint(false);
            transform.SetParent(null, true);
            Gen.SetLayerRecursive(gameObject, 2);
            v.SetPlayerDriver(false);
            Vehicle = null;
            Model.Sitting = false;
            transform.rotation = Quaternion.Euler(0, v.transform.eulerAngles.y, 0);
            Teleport(p + Vector3.up * 0.2f);
            GameManager.I.Hud.ShowVehicleName(null);
            if (bail)
            {
                // pulou do carro andando: rola no chão e se machuca
                Knock(vel * 0.55f + v.transform.right * -3f + Vector3.up * 2f, 1.3f);
                TakeDamage(12f, transform.position, Vector3.zero, false);
            }
        }

        public void Teleport(Vector3 p)
        {
            CC.enabled = false;
            transform.position = p;
            CC.enabled = true;
            vy = 0f;
        }

        // ===================== dano =====================

        public void Knock(Vector3 vel, float time)
        {
            if (Vehicle != null || IsDead) return;
            knockVel = vel;
            knockT = time;
            Model.MoveSpeed = 0f;
        }

        void KnockUpdate()
        {
            float dt = Time.deltaTime;
            knockT -= dt;
            knockVel.y -= 20f * dt;
            if (CC.isGrounded && knockVel.y < 0f) { knockVel.y = -1f; knockVel.x *= 1f - 4f * dt; knockVel.z *= 1f - 4f * dt; }
            CC.Move(knockVel * dt);
            // caído no chão, levanta no final
            float fall = knockT > 0.4f ? 85f : Mathf.Lerp(0f, 85f, knockT / 0.4f);
            Model.transform.localRotation = Quaternion.Euler(-fall, 0, 0);
            Model.transform.localPosition = new Vector3(0, fall > 45f ? 0.25f : 0f, 0);
            if (knockT <= 0f) { Model.transform.localRotation = Quaternion.identity; Model.transform.localPosition = Vector3.zero; }
        }

        public void HitByVehicle(VehicleController v, Vector3 relVel)
        {
            float speed = relVel.magnitude;
            if (speed < 4f || knockT > 0f) return;
            TakeDamage(speed * 4.5f, transform.position, relVel.normalized, v != null && v.PlayerDriving);
            Knock(relVel * 0.7f + Vector3.up * Mathf.Min(speed * 0.4f, 7f), 1.4f);
        }

        public void TakeDamage(float amount, Vector3 point, Vector3 dir, bool fromPlayer, bool headshot = false)
        {
            if (IsDead || amount <= 0f) return;
            var gm = GameManager.I;
            if (gm.State == GameState.Title) return;
            lastDamage = Time.time;
            if (Armor > 0f)
            {
                float a = Mathf.Min(Armor, amount);
                Armor -= a;
                amount -= a;
            }
            Health -= amount;
            if (Health <= 0f)
                Debug.Log($"[Dano] jogador -{amount:0} (vida {Health:0}, colete {Armor:0}) em {point} | no carro: {Vehicle != null}");
            gm.Hud.DamageFlash();
            if (Health <= 0f)
            {
                Health = 0f;
                Die();
            }
        }

        void Die()
        {
            var gm = GameManager.I;
            if (entering)
            {
                // morreu no meio de entrar/sair do carro: cancela a animação
                StopAllCoroutines();
                entering = false;
                Model.SnapTo("Movimento");
                Teleport(transform.position);
            }
            if (Vehicle != null && !Vehicle.Destroyed) ExitVehicle(false);
            else if (Vehicle != null) { transform.SetParent(null, true); Gen.SetLayerRecursive(gameObject, 2); Vehicle = null; Model.Sitting = false; }
            Model.transform.localRotation = Quaternion.Euler(-88f, 0, 0);
            Model.transform.localPosition = new Vector3(0, 0.25f, 0);
            Model.MoveSpeed = 0f;
            Model.HandsUp = false;
            gm.Wasted("Você morreu.");
        }

        public void Arrest()
        {
            if (IsDead) return;
            Model.HandsUp = true;
            Model.MoveSpeed = 0f;
            GameManager.I.Busted();
        }

        public void Respawn(Vector3 p)
        {
            if (Vehicle != null) { transform.SetParent(null, true); Gen.SetLayerRecursive(gameObject, 2); Vehicle.SetPlayerDriver(false); Vehicle = null; }
            StopAllCoroutines();
            entering = false;
            knockT = 0f;
            Health = MaxHealth;
            Stamina = 100f;
            Swimming = false;
            Model.Sitting = false;
            Model.HandsUp = false;
            Model.transform.localRotation = Quaternion.identity;
            Model.transform.localPosition = Vector3.zero;
            transform.rotation = Quaternion.Euler(0, 180f, 0);
            Teleport(p);
        }
    }
}

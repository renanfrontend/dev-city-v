using System.Collections.Generic;
using UnityEngine;

namespace DevCity
{
    public enum VehicleKind { Sedan, Sports, SUV, Taxi, Van, Police, Swat, Armored }

    /// <summary>"Cérebro" de quem dirige: trânsito, polícia, fuga de missão...</summary>
    public interface IVehicleBrain
    {
        void Drive(VehicleController v, float dt);
    }

    public class VehicleSpec
    {
        public string Name;
        public float Length, Width, Height, WheelRadius, Mass, Engine, MaxSpeed, Grip, Health = 1000f;
        public bool AWD;
    }

    /// <summary>
    /// Carro arcade com física de verdade (Rigidbody): suspensão por raycast em cada roda, atrito
    /// lateral com limite (dá para derrapar no freio de mão), tração, arrasto, barra estabilizadora,
    /// dano, fumaça, fogo, explosão, faróis, giroflex e sirene.
    /// </summary>
    public class VehicleController : MonoBehaviour, IDamageable
    {
        public static readonly List<VehicleController> All = new List<VehicleController>();

        public VehicleKind Kind;
        public VehicleSpec Spec;
        public Rigidbody Body { get; private set; }
        public Transform Seat { get; private set; }
        public IVehicleBrain Brain;
        public float Health;
        public bool Destroyed { get; private set; }
        public bool Burning { get; private set; }
        public bool Sunk { get; private set; }
        public bool PlayerDriving { get; private set; }
        public bool OwnedByPlayer;
        public bool Persistent;          // missão: não some sozinho
        public bool SirenOn;
        public bool IsAircraft => false;
        public bool ExplodesOnDeath = true;
        public System.Action<VehicleController> OnDestroyed;

        // passageiros NPC
        public Humanoid DriverModel;
        public Outfit DriverOutfit;
        public PedRole DriverRole = PedRole.Civilian;
        public int ExtraOfficers;
        public bool HasNpcDriver => DriverModel != null;

        public string DisplayName => Spec.Name;
        public float CameraHeight => Spec.Height * 0.85f + 0.6f;
        public float CameraDistance => Spec.Length * 1.25f + 1.5f;
        public float Speed => Body.linearVelocity.magnitude;
        public float ForwardSpeed => Vector3.Dot(Body.linearVelocity, transform.forward);

        // entrada
        float throttle, steerInput, steerAngle;
        bool handbrake, braking;

        // rodas
        internal Vector3[] wheelLocal;
        internal Transform[] wheelPivot, wheelSpin;
        float[] compression;
        bool[] grounded;
        float suspRest = 0.35f, spring, damper;

        // visual e som
        internal Renderer[] bodyRenderers;
        internal Renderer[] headRenderers, tailRenderers;
        internal Renderer sirenA, sirenB;
        internal Light[] headLights;
        Light sirenLight;
        AudioSource engineAudio, sirenAudio;
        bool lightsOn = true;
        float burnT, flipT, waterT, smokeT, lastHit;
        bool lastDamageByPlayer;

        public void Setup(VehicleKind kind, VehicleSpec spec, Rigidbody rb, Transform seat)
        {
            Kind = kind;
            Spec = spec;
            Body = rb;
            Seat = seat;
            Health = spec.Health;
            spring = spec.Mass * 10f;
            damper = spec.Mass * 1.1f;
            compression = new float[4];
            grounded = new bool[4];
            All.Add(this);
            if (kind == VehicleKind.Police || kind == VehicleKind.Swat)
            {
                var go = new GameObject("LuzGiroflex");
                go.transform.SetParent(transform, false);
                go.transform.localPosition = new Vector3(0, spec.Height + 0.6f, 0);
                sirenLight = go.AddComponent<Light>();
                sirenLight.type = LightType.Point;
                sirenLight.range = 14f;
                sirenLight.intensity = 0f;
            }
        }

        void OnDestroy() { All.Remove(this); }

        public void SetInput(float t, float s, bool hb)
        {
            throttle = Mathf.Clamp(t, -1f, 1f);
            steerInput = Mathf.Clamp(s, -1f, 1f);
            handbrake = hb;
        }

        public Vector3 DoorPoint(bool left)
        {
            return transform.TransformPoint(new Vector3((left ? -1f : 1f) * (Spec.Width / 2f + 0.9f), 0.15f, Spec.Length * 0.05f));
        }

        /// <summary>Restaura a vida e apaga o fogo (usado pelo AutoTest para o carro não explodir com o jogador dentro).</summary>
        public void Repair()
        {
            if (Destroyed) return;
            Health = Spec.Health;
            Burning = false;
        }

        public void Horn() => Sfx.PlayAt(Sfx.Horn, transform.position, 0.8f);
        public void ToggleLights() => lightsOn = !lightsOn;

        public void SetPlayerDriver(bool on)
        {
            PlayerDriving = on;
            Gen.SetLayerRecursive(gameObject, on ? 2 : 0);
            if (on)
            {
                Brain = null;
                OwnedByPlayer = true;
                Persistent = true;
                Body.interpolation = RigidbodyInterpolation.Interpolate;
                if (engineAudio == null) engineAudio = Sfx.Loop(gameObject, Sfx.Engine, 0.35f, 60f);
                engineAudio.enabled = true;
            }
            else
            {
                throttle = 0f; steerInput = 0f; handbrake = true;
                if (engineAudio != null) engineAudio.enabled = false;
            }
        }

        // ===================== motorista NPC =====================

        public void PutNpcDriver(Outfit o, PedRole role)
        {
            DriverOutfit = o;
            DriverRole = role;
            DriverModel = Humanoid.Create(Seat, o);
            DriverModel.Sitting = true;
        }

        /// <summary>Tira o motorista NPC do carro (roubo, polícia descendo, motorista morto).</summary>
        public Ped EjectDriver(Vector3 near, bool byPlayer, bool dead = false)
        {
            if (DriverModel == null) return null;
            Destroy(DriverModel.gameObject);
            DriverModel = null;
            Brain = null;
            SetInput(0f, 0f, true);
            var ped = Ped.Spawn(DoorPoint(true), DriverRole, DriverOutfit);
            ped.transform.rotation = Quaternion.LookRotation(Gen.Flat(ped.transform.position - near).normalized + transform.forward * 0.01f);
            if (dead) ped.Kill(false);
            else if (byPlayer && DriverRole == PedRole.Civilian)
            {
                ped.Knock((ped.transform.position - near).normalized * 4f + Vector3.up * 2f, false);
                if (Random.value < 0.15f) ped.BecomeHostile(); else ped.Flee(near, 12f);
            }
            return ped;
        }

        // ===================== física =====================

        void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;
            if (Brain != null && !Destroyed && !Sunk) Brain.Drive(this, dt);
            if (Destroyed || Sunk) { throttle = 0f; handbrake = true; steerInput = 0f; }

            Vector3 vel = Body.linearVelocity;
            float speed = vel.magnitude;
            float fwdSpeed = Vector3.Dot(vel, transform.forward);

            // direção mais "curta" em alta velocidade
            float maxSteer = Mathf.Lerp(36f, 9f, Mathf.Clamp01(speed / 42f));
            steerAngle = Mathf.MoveTowards(steerAngle, steerInput * maxSteer, 140f * dt);

            braking = (throttle < -0.1f && fwdSpeed > 1f) || (throttle > 0.1f && fwdSpeed < -1f);
            float engineForce = 0f;
            if (!braking && !Destroyed && !Sunk)
            {
                if (throttle > 0f) engineForce = Spec.Engine * throttle * Mathf.Clamp01(1f - Mathf.Pow(Mathf.Max(fwdSpeed, 0f) / Spec.MaxSpeed, 2f));
                else if (throttle < 0f) engineForce = Spec.Engine * 0.5f * throttle * Mathf.Clamp01(1f - Mathf.Abs(fwdSpeed) / 14f);
            }
            float brakeForce = braking ? Spec.Mass * 9f : 0f;

            int groundedCount = 0;
            float quarterMass = Spec.Mass / 4f;
            for (int i = 0; i < 4; i++)
            {
                bool front = i < 2;
                Vector3 mount = transform.TransformPoint(wheelLocal[i]);
                float maxLen = suspRest + Spec.WheelRadius;
                grounded[i] = Physics.Raycast(mount, -transform.up, out var hit, maxLen, ~(1 << 2), QueryTriggerInteraction.Ignore);
                if (!grounded[i])
                {
                    compression[i] = 0f;
                    if (wheelPivot[i] != null) wheelPivot[i].localPosition = wheelLocal[i] - Vector3.up * suspRest;
                    continue;
                }
                groundedCount++;
                compression[i] = maxLen - hit.distance;
                Vector3 pv = Body.GetPointVelocity(mount);
                float compVel = -Vector3.Dot(pv, transform.up);
                float springF = Mathf.Max(0f, compression[i] * spring + compVel * damper);
                Body.AddForceAtPosition(transform.up * springF, mount);

                // sistema da roda (dianteira vira)
                Quaternion steer = front ? Quaternion.AngleAxis(steerAngle, transform.up) : Quaternion.identity;
                Vector3 fwd = Vector3.ProjectOnPlane(steer * transform.forward, hit.normal).normalized;
                Vector3 right = Vector3.ProjectOnPlane(steer * transform.right, hit.normal).normalized;
                float vLat = Vector3.Dot(pv, right);
                float vLong = Vector3.Dot(pv, fwd);

                float mu = Spec.Grip;
                float gain = 0.55f;
                if (!front && handbrake) { mu *= 0.4f; gain = 0.12f; }
                float maxF = springF * mu + 200f;
                float latF = Mathf.Clamp(-vLat * quarterMass / dt * gain, -maxF, maxF);

                float longF = 0f;
                bool driven = Spec.AWD || !front;
                if (driven) longF += engineForce / (Spec.AWD ? 4f : 2f);
                if (braking) longF -= Mathf.Sign(vLong) * Mathf.Min(brakeForce / 4f, Mathf.Abs(vLong) * quarterMass / dt);
                if (handbrake && !front) longF -= Mathf.Sign(vLong) * Mathf.Min(Spec.Mass * 3f, Mathf.Abs(vLong) * quarterMass / dt * 0.5f);
                longF -= vLong * 12f; // resistência de rolagem

                // círculo de atrito: força total limitada pela aderência
                Vector2 f = new Vector2(latF, longF);
                if (f.magnitude > maxF * 1.15f) f = f.normalized * maxF * 1.15f;
                Vector3 at = mount + transform.up * (Body.centerOfMass.y - wheelLocal[i].y);
                Body.AddForceAtPosition(right * f.x + fwd * f.y, at);

                if (wheelPivot[i] != null)
                {
                    wheelPivot[i].localPosition = wheelLocal[i] - Vector3.up * (hit.distance - Spec.WheelRadius);
                    wheelPivot[i].localRotation = Quaternion.Euler(0, front ? steerAngle : 0f, 0);
                    wheelSpin[i].Rotate(vLong / Spec.WheelRadius * Mathf.Rad2Deg * dt, 0, 0, Space.Self);
                }
            }

            // barra estabilizadora (menos capotamento)
            for (int axle = 0; axle < 2; axle++)
            {
                int l = axle * 2, r = axle * 2 + 1;
                float diff = (compression[l] - compression[r]) * spring * 0.6f;
                if (grounded[l]) Body.AddForceAtPosition(-transform.up * diff, transform.TransformPoint(wheelLocal[l]));
                if (grounded[r]) Body.AddForceAtPosition(transform.up * diff, transform.TransformPoint(wheelLocal[r]));
            }

            // arrasto e "downforce"
            float drag = Spec.Engine / (Spec.MaxSpeed * Spec.MaxSpeed) * 0.35f;
            Body.AddForce(-vel * speed * drag);
            if (groundedCount > 0) Body.AddForce(-transform.up * speed * speed * 1.5f);
            else Body.AddTorque(-Body.angularVelocity * Spec.Mass * 0.3f); // estabiliza no ar
        }

        void Update()
        {
            float dt = Time.deltaTime;
            var gm = GameManager.I;
            if (gm == null) return;

            // virou de cabeça para baixo: desvira depois de alguns segundos
            if (transform.up.y < 0.25f && Speed < 2f && !Destroyed)
            {
                flipT += dt;
                if (flipT > 3f)
                {
                    transform.SetPositionAndRotation(transform.position + Vector3.up * 1.2f, Quaternion.Euler(0, transform.eulerAngles.y, 0));
                    Body.linearVelocity = Vector3.zero;
                    Body.angularVelocity = Vector3.zero;
                    flipT = 0f;
                }
            }
            else flipT = 0f;

            // água: afunda e o motor morre
            if (!Sunk && gm.City.IsWater(transform.position) && transform.position.y < CityGenerator.WATER_Y - 0.2f)
            {
                waterT += dt;
                Body.linearDamping = 2.5f;
                if (waterT > 2.5f) { Sunk = true; Brain = null; if (PlayerDriving) gm.Hud.Help("O carro afundou!", 3f); }
            }

            DamageEffects(dt);
            LightsAndSiren(gm);

            if (engineAudio != null && engineAudio.enabled)
            {
                float rpm = Mathf.Abs(ForwardSpeed) / Spec.MaxSpeed;
                engineAudio.pitch = Mathf.Lerp(engineAudio.pitch, 0.55f + rpm * 1.7f + Mathf.Abs(throttle) * 0.15f, dt * 5f);
                engineAudio.volume = Destroyed || Sunk ? 0f : 0.25f + Mathf.Abs(throttle) * 0.2f;
            }
        }

        void DamageEffects(float dt)
        {
            smokeT -= dt;
            Vector3 hood = transform.TransformPoint(new Vector3(0, Spec.Height * 0.55f, Spec.Length * 0.38f));
            float hp = Health / Spec.Health;
            if (!Destroyed && hp < 0.35f && smokeT <= 0f)
            {
                smokeT = hp < 0.15f ? 0.05f : 0.12f;
                Fx.Smoke(hood, 0.8f, hp < 0.15f ? new Color(0.1f, 0.1f, 0.1f, 0.7f) : new Color(0.7f, 0.7f, 0.7f, 0.5f));
                if (hp < 0.12f) Fx.Fire(hood, 0.7f);
            }
            if (Burning)
            {
                burnT -= dt;
                Fx.Fire(hood, 1.4f);
                if (Random.value < 0.4f) Fx.Smoke(hood + Vector3.up, 1.5f, new Color(0.08f, 0.08f, 0.08f, 0.8f));
                if (burnT <= 0f) Explode();
            }
            else if (Destroyed && smokeT <= 0f)
            {
                smokeT = 0.25f;
                Fx.Smoke(hood, 1.2f, new Color(0.1f, 0.1f, 0.1f, 0.6f));
            }
        }

        void LightsAndSiren(GameManager gm)
        {
            bool alive = !Destroyed && !Sunk && (PlayerDriving || HasNpcDriver);
            bool head = alive && lightsOn && gm.DayNight.Night01 > 0.4f;
            if (headRenderers != null) foreach (var r in headRenderers) r.sharedMaterial = head ? Mats.HeadOn : Mats.HeadOff;
            if (tailRenderers != null) foreach (var r in tailRenderers) r.sharedMaterial = alive && (braking || head) ? Mats.TailOn : Mats.TailOff;
            if (headLights != null)
            {
                bool near = Vector3.Distance(transform.position, gm.Cam.transform.position) < 90f;
                foreach (var l in headLights) l.enabled = head && near && (PlayerDriving || Kind == VehicleKind.Police);
            }

            if (sirenA != null)
            {
                bool on = SirenOn && !Destroyed;
                bool phase = Mathf.Repeat(Time.time * 3f, 1f) < 0.5f;
                sirenA.sharedMaterial = on && phase ? Mats.SirenRed : Mats.SirenOff;
                sirenB.sharedMaterial = on && !phase ? Mats.SirenBlue : Mats.SirenOff;
                if (sirenLight != null)
                {
                    sirenLight.intensity = on ? 3f : 0f;
                    sirenLight.color = phase ? new Color(1f, 0.1f, 0.1f) : new Color(0.15f, 0.3f, 1f);
                }
                if (on && sirenAudio == null) sirenAudio = Sfx.Loop(gameObject, Sfx.Siren, 0.45f, 140f);
                if (sirenAudio != null) sirenAudio.mute = !on;
            }
        }

        // ===================== dano =====================

        void OnCollisionEnter(Collision c)
        {
            float dv = c.impulse.magnitude / Spec.Mass;
            var gm = GameManager.I;
            var otherPlayer = c.collider.GetComponentInParent<PlayerController>();
            if (otherPlayer != null && otherPlayer.Vehicle == null)
            {
                otherPlayer.HitByVehicle(this, c.relativeVelocity);
                return;
            }
            if (dv < 2.5f || Time.time - lastHit < 0.15f) return;
            lastHit = Time.time;
            var cp = c.GetContact(0);
            Fx.Sparks(cp.point, cp.normal, Mathf.Clamp((int)(dv * 2f), 4, 20));
            Sfx.PlayAt(Sfx.Crash, cp.point, Mathf.Clamp01(dv / 12f), Random.Range(0.8f, 1.1f));
            float armor = Kind == VehicleKind.Armored ? 0.3f : Kind == VehicleKind.Swat ? 0.5f : 1f;
            ApplyDamage((dv - 2.5f) * 60f * armor, PlayerDriving);
            if (PlayerDriving)
            {
                gm.Cam.Shake(Mathf.Clamp01(dv / 15f));
                var other = c.collider.GetComponentInParent<VehicleController>();
                if (other != null && (other.Kind == VehicleKind.Police || other.Kind == VehicleKind.Swat) && dv > 4f)
                    gm.Wanted.Commit(Crime.RamPolice, transform.position);
                // batida forte machuca quem está dentro
                if (dv > 14f) gm.Player.TakeDamage((dv - 14f) * 3f, transform.position, Vector3.zero, false);
            }
        }

        void OnTriggerEnter(Collider other)
        {
            var ped = other.GetComponentInParent<Ped>();
            if (ped == null || ped.IsDead && ped.Knocked) return;
            Vector3 rel = Body.linearVelocity;
            if (rel.magnitude < 3f) return;
            ped.HitByVehicle(this, rel);
        }

        public void TakeDamage(float amount, Vector3 point, Vector3 dir, bool fromPlayer, bool headshot = false)
        {
            if (Destroyed) return;
            float armor = Kind == VehicleKind.Armored ? 0.25f : Kind == VehicleKind.Swat ? 0.5f : 1f;
            ApplyDamage(amount * 0.7f * armor, fromPlayer);
            // tiro no vidro pode acertar o motorista
            if (fromPlayer && HasNpcDriver && transform.InverseTransformPoint(point).y > Spec.Height * 0.55f && Random.value < 0.3f)
            {
                var ped = EjectDriver(point, true, true);
                if (ped != null) ped.ReportDeath(true);
            }
            else if (fromPlayer && Brain is TrafficAI t) t.Panic(point);
        }

        void ApplyDamage(float amount, bool fromPlayer)
        {
            if (Destroyed || amount <= 0f) return;
            Health -= amount;
            if (fromPlayer) lastDamageByPlayer = true;
            if (Health <= 0f)
            {
                Health = 0f;
                if (!ExplodesOnDeath) { Destroyed = true; Brain = null; OnDestroyed?.Invoke(this); return; }
                if (!Burning) { Burning = true; burnT = 4f; }
            }
        }

        public void Explode()
        {
            if (Destroyed && !Burning) return;
            Burning = false;
            Destroyed = true;
            Brain = null;
            SirenOn = false;
            var gm = GameManager.I;
            Explosion.Create(transform.position + Vector3.up, 7f, 260f, lastDamageByPlayer);
            Body.AddForce(Vector3.up * Spec.Mass * 7f + Random.insideUnitSphere * Spec.Mass * 2f, ForceMode.Impulse);
            Body.AddTorque(Random.insideUnitSphere * Spec.Mass * 2f, ForceMode.Impulse);
            if (bodyRenderers != null) foreach (var r in bodyRenderers) r.sharedMaterial = Mats.Wreck;
            if (HasNpcDriver) { var p = EjectDriver(transform.position, lastDamageByPlayer, true); if (p != null) p.ReportDeath(lastDamageByPlayer); }
            if (PlayerDriving) gm.Player.TakeDamage(9999f, transform.position, Vector3.up, false);
            if (lastDamageByPlayer && (Kind == VehicleKind.Police || Kind == VehicleKind.Swat)) gm.Wanted.Commit(Crime.DestroyPoliceVehicle, transform.position);
            OnDestroyed?.Invoke(this);
        }

        public bool IsDead => Destroyed;
    }
}

using UnityEngine;

namespace DevCity
{
    /// <summary>
    /// Helicóptero da polícia (3★+): circula sobre o jogador, ilumina com holofote à noite,
    /// tem um atirador e pode ser derrubado (cai girando e explode).
    /// </summary>
    public class Helicopter : MonoBehaviour, IDamageable
    {
        public float Health = 700f;
        public bool IsDead { get; private set; }

        Transform rotor, tail;
        Light spot;
        AudioSource sound;
        float orbit, fireT = 3f;
        bool leaving;
        Vector3 vel;

        public static Helicopter Create(Vector3 pos)
        {
            var go = new GameObject("Helicoptero DCPD");
            go.transform.position = pos;
            var h = go.AddComponent<Helicopter>();
            var body = Mats.Get(new Color(0.1f, 0.12f, 0.2f), 0.6f, 0.3f);
            var white = Mats.Get(new Color(0.9f, 0.9f, 0.92f), 0.6f);
            Gen.Prim(PrimitiveType.Sphere, go.transform, new Vector3(0, 0, 0.3f), new Vector3(2.2f, 2f, 3.6f), body, true);
            Gen.Prim(PrimitiveType.Sphere, go.transform, new Vector3(0, 0.15f, 1.3f), new Vector3(1.7f, 1.3f, 1.6f), Mats.Glass);
            Gen.Cube(go.transform, new Vector3(0, -0.2f, -0.2f), new Vector3(2.25f, 0.35f, 2.6f), white);
            Gen.Cube(go.transform, new Vector3(0, 0.25f, -3.2f), new Vector3(0.35f, 0.4f, 4.2f), body);
            Gen.Cube(go.transform, new Vector3(0, 0.9f, -5.1f), new Vector3(0.15f, 1.4f, 0.7f), body);
            Gen.Cube(go.transform, new Vector3(0.7f, -1.15f, 0.2f), new Vector3(0.12f, 0.1f, 3.2f), Mats.Black);
            Gen.Cube(go.transform, new Vector3(-0.7f, -1.15f, 0.2f), new Vector3(0.12f, 0.1f, 3.2f), Mats.Black);
            h.rotor = new GameObject("Rotor").transform;
            h.rotor.SetParent(go.transform, false);
            h.rotor.localPosition = new Vector3(0, 1.15f, 0.2f);
            Gen.Cube(h.rotor, Vector3.zero, new Vector3(10f, 0.05f, 0.35f), Mats.Black);
            Gen.Cube(h.rotor, Vector3.zero, new Vector3(0.35f, 0.05f, 10f), Mats.Black);
            h.tail = new GameObject("RotorCauda").transform;
            h.tail.SetParent(go.transform, false);
            h.tail.localPosition = new Vector3(0.15f, 0.9f, -5.2f);
            Gen.Cube(h.tail, Vector3.zero, new Vector3(0.04f, 1.6f, 0.15f), Mats.Black);

            var sl = new GameObject("Holofote");
            sl.transform.SetParent(go.transform, false);
            sl.transform.localPosition = new Vector3(0, -1f, 1.5f);
            h.spot = sl.AddComponent<Light>();
            h.spot.type = LightType.Spot;
            h.spot.range = 140f;
            h.spot.spotAngle = 14f;
            h.spot.intensity = 6f;
            h.spot.color = new Color(0.95f, 0.97f, 1f);
            h.sound = Sfx.Loop(go, Sfx.Heli, 0.9f, 260f);
            h.orbit = Random.value * 6f;
            return h;
        }

        public void Leave() { leaving = true; }

        void Update()
        {
            var gm = GameManager.I;
            if (gm == null) return;
            float dt = Time.deltaTime;
            rotor.Rotate(0, 1400f * dt, 0, Space.Self);
            tail.Rotate(2000f * dt, 0, 0, Space.Self);

            if (IsDead)
            {
                // caindo girando
                vel.y -= 14f * dt;
                transform.position += vel * dt;
                transform.Rotate(0, 300f * dt, 40f * dt, Space.World);
                Fx.Smoke(transform.position, 1.5f, new Color(0.1f, 0.1f, 0.1f, 0.8f));
                Fx.Fire(transform.position, 1.2f);
                if (Physics.Raycast(transform.position, Vector3.down, 2f, ~(1 << 2), QueryTriggerInteraction.Ignore) || transform.position.y < 0.5f)
                {
                    Explosion.Create(transform.position, 10f, 300f, true);
                    Destroy(gameObject);
                }
                return;
            }

            if (leaving || gm.Wanted.Stars == 0)
            {
                transform.position += (transform.forward + Vector3.up * 0.3f) * 30f * dt;
                if (Gen.FlatDist(transform.position, gm.PlayerPos) > 450f) Destroy(gameObject);
                spot.enabled = false;
                return;
            }

            // órbita em volta do alvo (jogador visto ou última posição)
            Vector3 center = gm.Wanted.Seen ? gm.PlayerPos : gm.Wanted.LastKnown;
            orbit += dt * 0.25f;
            Vector3 want = center + new Vector3(Mathf.Cos(orbit) * 38f, 42f, Mathf.Sin(orbit) * 38f);
            Vector3 to = want - transform.position;
            vel = Vector3.Lerp(vel, Vector3.ClampMagnitude(to * 0.8f, 38f), dt * 1.5f);
            transform.position += vel * dt;
            Vector3 look = Gen.Flat(center - transform.position);
            if (look.sqrMagnitude > 1f)
            {
                Quaternion q = Quaternion.LookRotation(look) * Quaternion.Euler(Mathf.Clamp(vel.magnitude, 0f, 15f), 0, 0);
                transform.rotation = Quaternion.Slerp(transform.rotation, q, dt * 2f);
            }

            spot.enabled = gm.DayNight.Night01 > 0.3f;
            spot.transform.rotation = Quaternion.LookRotation(center - spot.transform.position);

            // atirador na porta
            fireT -= dt;
            if (fireT <= 0f && gm.Wanted.Seen && gm.Wanted.Stars >= 3)
            {
                fireT = Random.Range(1.4f, 2.4f);
                Vector3 from = transform.position + Vector3.down * 1.2f;
                Vector3 aim = gm.PlayerPos + Vector3.up;
                if (!Physics.Linecast(from, aim, ~(1 << 2), QueryTriggerInteraction.Ignore))
                {
                    bool hit = Random.value < 0.2f;
                    Vector3 end = hit ? aim : aim + Random.insideUnitSphere * 3f;
                    Fx.Tracer(from, end);
                    Sfx.PlayAt(Sfx.Rifle, from, 0.8f, 1f, 250f);
                    if (hit)
                    {
                        if (gm.Player.Vehicle != null) gm.Player.Vehicle.TakeDamage(40f, end, Vector3.down, false);
                        else gm.Player.TakeDamage(18f, end, Vector3.down, false);
                    }
                }
            }
        }

        public void TakeDamage(float amount, Vector3 point, Vector3 dir, bool fromPlayer, bool headshot = false)
        {
            if (IsDead) return;
            Health -= amount;
            Fx.Sparks(point, -dir, 4);
            if (fromPlayer) GameManager.I.Wanted.Commit(Crime.AttackCop, transform.position);
            if (Health <= 0f)
            {
                IsDead = true;
                vel = transform.forward * 8f;
                spot.enabled = false;
                GameManager.I.Hud.Notify("Helicóptero da polícia derrubado!");
            }
        }
    }
}

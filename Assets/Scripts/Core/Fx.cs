using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace DevCity
{
    /// <summary>Qualquer coisa que leva dano: jogador, pedestre, policial, carro, helicóptero.</summary>
    public interface IDamageable
    {
        void TakeDamage(float amount, Vector3 point, Vector3 dir, bool fromPlayer, bool headshot = false);
        bool IsDead { get; }
    }

    /// <summary>Partículas e efeitos rápidos (faíscas, fumaça, fogo, sangue, rastro de bala).</summary>
    public static class Fx
    {
        static ParticleSystem sparks, smoke, fire, blood, debris;
        static readonly Queue<LineRenderer> tracers = new Queue<LineRenderer>();
        static Transform root;
        static Runner runner;

        class Runner : MonoBehaviour { }

        public static void Init()
        {
            root = new GameObject("Efeitos").transform;
            Object.DontDestroyOnLoad(root.gameObject);
            runner = root.gameObject.AddComponent<Runner>();
            sparks = MakeSystem("Faiscas", 3f, 0.4f);
            smoke = MakeSystem("Fumaca", -0.15f, 3.5f);
            fire = MakeSystem("Fogo", -0.6f, 0.9f);
            blood = MakeSystem("Sangue", 2.5f, 0.8f);
            debris = MakeSystem("Destrocos", 2f, 1.8f);
            for (int i = 0; i < 24; i++)
            {
                var go = new GameObject("Tracer");
                go.transform.SetParent(root);
                var lr = go.AddComponent<LineRenderer>();
                lr.sharedMaterial = Mats.ParticleMat;
                lr.positionCount = 2;
                lr.widthMultiplier = 0.035f;
                lr.startColor = new Color(1f, 0.9f, 0.6f, 0.9f);
                lr.endColor = new Color(1f, 0.8f, 0.4f, 0.2f);
                lr.enabled = false;
                tracers.Enqueue(lr);
            }
        }

        static ParticleSystem MakeSystem(string name, float gravity, float life)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = false;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = gravity;
            main.startLifetime = life;
            main.maxParticles = 3000;
            var em = ps.emission;
            em.enabled = false;
            var shape = ps.shape;
            shape.enabled = false;
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                      new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0, 0.6f, 1, 1.6f));
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = Mats.ParticleMat;
            r.renderMode = ParticleSystemRenderMode.Billboard;
            ps.Play();
            return ps;
        }

        static void Emit(ParticleSystem ps, Vector3 pos, Vector3 vel, Color c, float size, float life)
        {
            var p = new ParticleSystem.EmitParams
            {
                position = pos,
                velocity = vel,
                startColor = c,
                startSize = size,
                startLifetime = life,
                applyShapeToPosition = false
            };
            ps.Emit(p, 1);
        }

        public static void Sparks(Vector3 pos, Vector3 normal, int count = 8)
        {
            for (int i = 0; i < count; i++)
                Emit(sparks, pos, (normal + Random.insideUnitSphere * 0.8f) * Random.Range(2f, 7f), new Color(1f, 0.8f, 0.4f), Random.Range(0.04f, 0.09f), Random.Range(0.15f, 0.4f));
            Emit(smoke, pos, normal * 0.5f, new Color(0.6f, 0.6f, 0.6f, 0.5f), 0.4f, 0.8f);
        }

        public static void Blood(Vector3 pos, Vector3 dir)
        {
            for (int i = 0; i < 10; i++)
                Emit(blood, pos, (dir * 1.5f + Random.insideUnitSphere) * Random.Range(1f, 3f), new Color(0.55f, 0.02f, 0.02f), Random.Range(0.06f, 0.14f), Random.Range(0.3f, 0.7f));
        }

        public static void Smoke(Vector3 pos, float amount, Color c)
        {
            Emit(smoke, pos + Random.insideUnitSphere * 0.3f, Vector3.up * Random.Range(1f, 2.5f) + Random.insideUnitSphere * 0.4f, c, Random.Range(0.8f, 1.6f) * amount, Random.Range(2f, 3.5f));
        }

        public static void Fire(Vector3 pos, float amount)
        {
            Emit(fire, pos + Random.insideUnitSphere * 0.4f * amount, Vector3.up * Random.Range(1.5f, 3f), Color.Lerp(new Color(1f, 0.5f, 0.1f), new Color(1f, 0.85f, 0.3f), Random.value), Random.Range(0.6f, 1.2f) * amount, Random.Range(0.4f, 0.8f));
        }

        public static void Debris(Vector3 pos, int count, float force)
        {
            for (int i = 0; i < count; i++)
                Emit(debris, pos, (Random.insideUnitSphere + Vector3.up) * force * Random.Range(0.4f, 1f), new Color(0.15f, 0.13f, 0.12f), Random.Range(0.15f, 0.4f), Random.Range(1.2f, 2.2f));
        }

        public static void Tracer(Vector3 from, Vector3 to)
        {
            var lr = tracers.Dequeue();
            tracers.Enqueue(lr);
            lr.SetPosition(0, from);
            lr.SetPosition(1, to);
            lr.enabled = true;
            runner.StartCoroutine(Hide(lr, 0.05f));
        }

        public static void MuzzleFlash(Vector3 pos, Vector3 dir)
        {
            for (int i = 0; i < 4; i++)
                Emit(fire, pos + dir * 0.1f * i, dir * 4f, new Color(1f, 0.85f, 0.45f), 0.25f - i * 0.04f, 0.05f);
            runner.StartCoroutine(FlashLight(pos, new Color(1f, 0.75f, 0.4f), 3f, 0.05f, 6f));
        }

        public static IEnumerator Hide(LineRenderer lr, float t)
        {
            yield return new WaitForSeconds(t);
            lr.enabled = false;
        }

        public static void Run(IEnumerator routine) => runner.StartCoroutine(routine);

        static IEnumerator FlashLight(Vector3 pos, Color c, float intensity, float time, float range)
        {
            var go = new GameObject("Clarao");
            go.transform.position = pos;
            var l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = c;
            l.range = range;
            l.intensity = intensity;
            float t = 0f;
            while (t < time)
            {
                t += Time.deltaTime;
                l.intensity = intensity * (1f - t / time);
                yield return null;
            }
            Object.Destroy(go);
        }

        public static IEnumerator ExplosionVisual(Vector3 pos, float radius)
        {
            for (int i = 0; i < 40; i++) Fire(pos + Random.insideUnitSphere * radius * 0.4f, 2.5f);
            for (int i = 0; i < 25; i++) Smoke(pos + Random.insideUnitSphere * radius * 0.4f, 2.5f, new Color(0.15f, 0.14f, 0.13f, 0.8f));
            Debris(pos, 30, 14f);
            Sparks(pos, Vector3.up, 30);
            var ball = Gen.Prim(PrimitiveType.Sphere, null, pos, Vector3.one, Mats.Emit(new Color(1f, 0.55f, 0.15f), 4f));
            ball.name = "BolaDeFogo";
            yield return FlashLightAndBall(ball, pos, radius);
        }

        static IEnumerator FlashLightAndBall(GameObject ball, Vector3 pos, float radius)
        {
            var l = ball.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = new Color(1f, 0.6f, 0.25f);
            l.range = radius * 6f;
            float t = 0f;
            var mat = ball.GetComponent<MeshRenderer>().material;
            while (t < 0.6f)
            {
                t += Time.deltaTime;
                float k = t / 0.6f;
                ball.transform.localScale = Vector3.one * Mathf.Lerp(radius * 0.4f, radius * 1.6f, Mathf.Sqrt(k));
                l.intensity = Mathf.Lerp(8f, 0f, k);
                mat.SetColor("_EmissionColor", new Color(1f, 0.55f, 0.15f) * Mathf.Lerp(4f, 0f, k));
                mat.color = new Color(1f, 0.6f, 0.2f) * (1f - k);
                yield return null;
            }
            Object.Destroy(ball);
        }
    }

    public static class Explosion
    {
        public static void Create(Vector3 pos, float radius, float damage, bool fromPlayer)
        {
            Fx.Run(Fx.ExplosionVisual(pos, radius));
            Sfx.PlayAt(Sfx.Explosion, pos, 1f, 1f, 400f);
            var gm = GameManager.I;
            if (gm != null && gm.Cam != null)
            {
                float d = Vector3.Distance(gm.Cam.transform.position, pos);
                gm.Cam.Shake(Mathf.Clamp01(1f - d / 80f) * 1.2f);
            }
            var hit = new HashSet<IDamageable>();
            foreach (var col in Physics.OverlapSphere(pos, radius, ~0, QueryTriggerInteraction.Collide))
            {
                var dmg = col.GetComponentInParent<IDamageable>();
                if (dmg != null && hit.Add(dmg))
                {
                    float k = 1f - Mathf.Clamp01(Vector3.Distance(col.ClosestPoint(pos), pos) / radius);
                    dmg.TakeDamage(damage * (0.25f + 0.75f * k), col.transform.position, (col.transform.position - pos).normalized, fromPlayer);
                }
                var rb = col.attachedRigidbody;
                if (rb != null && !rb.isKinematic) rb.AddExplosionForce(damage * 60f, pos, radius * 1.5f, 1.5f, ForceMode.Impulse);
            }
            if (gm != null && fromPlayer) gm.Wanted.Commit(Crime.Explosion, pos);
            if (gm != null) gm.Peds.Panic(pos, 70f);
        }
    }

    /// <summary>Item que gira no chão: dinheiro, maleta, pacote, colete...</summary>
    public class Pickup : MonoBehaviour
    {
        public System.Action OnCollect;
        public int Money;
        public float Radius = 1.6f;
        public bool AllowInVehicle = true;
        Vector3 basePos;

        public static Pickup Create(Vector3 pos, Color color, int money = 0, string kind = "dinheiro")
        {
            var go = new GameObject("Pickup_" + kind);
            go.transform.position = pos;
            var p = go.AddComponent<Pickup>();
            p.Money = money;
            p.basePos = pos;
            var mat = Mats.Emit(color, 1.3f);
            if (kind == "maleta")
            {
                Gen.Cube(go.transform, new Vector3(0, 0.35f, 0), new Vector3(0.6f, 0.42f, 0.18f), mat);
                Gen.Cube(go.transform, new Vector3(0, 0.62f, 0), new Vector3(0.22f, 0.08f, 0.05f), Mats.Black);
            }
            else if (kind == "pacote")
            {
                Gen.Cube(go.transform, new Vector3(0, 0.3f, 0), new Vector3(0.5f, 0.5f, 0.5f), Mats.Get(new Color(0.6f, 0.45f, 0.25f)));
                Gen.Cube(go.transform, new Vector3(0, 0.3f, 0), new Vector3(0.52f, 0.1f, 0.52f), mat);
            }
            else
            {
                Gen.Cube(go.transform, new Vector3(0, 0.25f, 0), new Vector3(0.35f, 0.12f, 0.2f), mat);
                Gen.Cube(go.transform, new Vector3(0.05f, 0.35f, 0.02f), new Vector3(0.35f, 0.12f, 0.2f), mat);
            }
            var light = new GameObject("Brilho").AddComponent<Light>();
            light.transform.SetParent(go.transform, false);
            light.transform.localPosition = Vector3.up * 0.6f;
            light.type = LightType.Point;
            light.color = color;
            light.range = 3f;
            light.intensity = 1.2f;
            return p;
        }

        void Update()
        {
            transform.Rotate(0, 120f * Time.deltaTime, 0, Space.World);
            transform.position = basePos + Vector3.up * (Mathf.Sin(Time.time * 3f) * 0.08f + 0.05f);
            var gm = GameManager.I;
            if (gm == null || !gm.Playing || gm.Player.IsDead) return;
            bool inCar = gm.Player.Vehicle != null;
            if (inCar && !AllowInVehicle) return;
            float r = inCar ? Radius + 1.5f : Radius;
            if (Gen.FlatDist(gm.PlayerPos, transform.position) < r && Mathf.Abs(gm.PlayerPos.y - transform.position.y) < 3f)
            {
                if (Money > 0) { gm.AddMoney(Money); Sfx.Play2D(Sfx.Cash, 0.8f); }
                else Sfx.Play2D(Sfx.Pickup, 0.8f);
                OnCollect?.Invoke();
                Destroy(gameObject);
            }
        }
    }
}

using System.Collections.Generic;
using UnityEngine;

namespace DevCity
{
    /// <summary>
    /// Resposta policial por nível (como pesquisado do GTA V):
    /// 1-2★ viaturas; 3★ + helicóptero com atirador e bloqueios; 4★ + SWAT e 2º helicóptero; 5★ tudo junto, agressivo.
    /// </summary>
    public class PoliceSystem : MonoBehaviour
    {
        static readonly int[] carsFor = { 0, 2, 3, 4, 4, 5 };
        static readonly int[] swatFor = { 0, 0, 0, 0, 2, 3 };
        static readonly int[] helisFor = { 0, 0, 0, 1, 2, 2 };

        readonly List<VehicleController> units = new List<VehicleController>();
        readonly List<Ped> footCops = new List<Ped>();
        readonly List<Helicopter> helis = new List<Helicopter>();
        float spawnT, roadblockT;

        public IEnumerable<VehicleController> Units => units;
        public IEnumerable<Ped> FootCops => footCops;
        public IEnumerable<Helicopter> Helis => helis;

        public bool IsUnit(VehicleController v) => units.Contains(v);

        void Update()
        {
            var gm = GameManager.I;
            if (gm == null || !gm.Playing) return;
            float dt = Time.deltaTime;
            units.RemoveAll(u => u == null);
            footCops.RemoveAll(c => c == null);
            helis.RemoveAll(h => h == null);

            int s = gm.Wanted.Stars;
            Vector3 p = gm.PlayerPos;

            // viaturas de ronda viram unidades de perseguição
            if (s > 0)
                foreach (var v in VehicleController.All)
                    if (v != null && v.Kind == VehicleKind.Police && v.Brain is TrafficAI && v.HasNpcDriver && Gen.FlatDist(v.transform.position, p) < 260f)
                    {
                        v.Brain = new PoliceDriverAI();
                        v.Persistent = true;
                        if (!units.Contains(v)) units.Add(v);
                    }

            // remove unidades muito longe (ficaram para trás)
            for (int i = units.Count - 1; i >= 0; i--)
            {
                var u = units[i];
                float d = Gen.FlatDist(u.transform.position, p);
                if (d > 380f || (s == 0 && d > 160f && !TrafficSystem.Visible(u.transform.position)))
                {
                    Destroy(u.gameObject);
                    units.RemoveAt(i);
                }
            }
            for (int i = footCops.Count - 1; i >= 0; i--)
            {
                var c = footCops[i];
                float d = Gen.FlatDist(c.transform.position, p);
                if ((c.IsDead && Time.time - c.DeadTime > 40f) || d > 200f || (s == 0 && d > 90f && !TrafficSystem.Visible(c.transform.position)))
                { Destroy(c.gameObject); footCops.RemoveAt(i); }
            }

            if (s == 0) return;

            spawnT -= dt;
            if (spawnT <= 0f)
            {
                spawnT = 2.2f;
                int cars = 0, swat = 0;
                foreach (var u in units)
                    if (!u.Destroyed) { if (u.Kind == VehicleKind.Swat) swat++; else cars++; }
                int onFoot = 0;
                foreach (var c in footCops) if (c != null && !c.IsDead) onFoot++;
                cars += onFoot / 3; // policiais a pé também "ocupam" a vaga de uma viatura
                if (swat < swatFor[s]) SpawnUnit(VehicleKind.Swat);
                else if (cars < carsFor[s]) SpawnUnit(VehicleKind.Police);
                if (helis.Count < helisFor[s]) SpawnHeli();
            }

            roadblockT -= dt;
            if (s >= 3 && roadblockT <= 0f && gm.Player.Vehicle != null && gm.Player.Vehicle.Speed > 14f)
            {
                roadblockT = 28f;
                SpawnRoadblock(gm);
            }
        }

        void SpawnUnit(VehicleKind kind)
        {
            var gm = GameManager.I;
            Vector3 center = gm.Wanted.Seen ? gm.PlayerPos : gm.Wanted.LastKnown;
            for (int tries = 0; tries < 8; tries++)
            {
                if (!gm.City.RandomLanePoint(center, 120f, 210f, out var pos, out var from, out var to)) continue;
                if (TrafficSystem.Visible(pos) || TrafficSystem.Occupied(pos, 9f)) continue;
                Vector3 dir = Gen.Flat(center - pos).normalized;
                var v = VehicleFactory.Create(kind, pos, Quaternion.LookRotation(dir));
                bool swat = kind == VehicleKind.Swat;
                v.PutNpcDriver(swat ? Outfit.Swat() : Outfit.Cop(), swat ? PedRole.Swat : PedRole.Cop);
                v.ExtraOfficers = swat ? 3 : 1;
                v.Brain = new PoliceDriverAI();
                v.Persistent = true;
                v.SirenOn = true;
                units.Add(v);
                return;
            }
        }

        void SpawnHeli()
        {
            var gm = GameManager.I;
            Vector3 c = gm.PlayerPos;
            Vector2 r = Random.insideUnitCircle.normalized * 260f;
            var h = Helicopter.Create(new Vector3(c.x + r.x, 70f, c.z + r.y));
            helis.Add(h);
        }

        void SpawnRoadblock(GameManager gm)
        {
            var car = gm.Player.Vehicle;
            Vector3 ahead = car.transform.position + Gen.Flat(car.Body.linearVelocity).normalized * 140f;
            var node = gm.City.NearestNode(ahead);
            Vector3 np = CityGenerator.NodePos(node);
            if (TrafficSystem.Visible(np) && Gen.FlatDist(np, car.transform.position) < 90f) return;
            Vector3 dir = Gen.Flat(np - car.transform.position).normalized;
            Vector3 right = CityGenerator.RightOf(dir);
            // duas viaturas atravessadas um pouco antes do cruzamento
            for (int k = -1; k <= 1; k += 2)
            {
                Vector3 pos = np - dir * 12f + right * k * 3f;
                if (TrafficSystem.Occupied(pos, 4f)) continue;
                var v = VehicleFactory.Create(VehicleKind.Police, pos, Quaternion.LookRotation(right * k));
                v.SirenOn = true;
                v.Persistent = true;
                v.SetInput(0f, 0f, true);
                units.Add(v);
                var cop = Ped.Spawn(pos - dir * 3f + right * k * 1.5f, PedRole.Cop);
                cop.Car = v;
                cop.State = PedState.Fight;
                footCops.Add(cop);
            }
        }

        // ---------- policiais descendo/subindo ----------

        public void Disembark(VehicleController v)
        {
            if (v.HasNpcDriver)
            {
                var ped = v.EjectDriver(v.transform.position, false);
                if (ped != null) { ped.Car = v; ped.State = PedState.Fight; footCops.Add(ped); }
            }
            for (int i = 0; i < v.ExtraOfficers; i++)
            {
                var role = v.Kind == VehicleKind.Swat ? PedRole.Swat : PedRole.Cop;
                var cop = Ped.Spawn(v.DoorPoint(false) - v.transform.forward * i * 1.2f, role);
                cop.Car = v;
                cop.State = PedState.Fight;
                footCops.Add(cop);
            }
            v.ExtraOfficers = 0;
            v.SetInput(0f, 0f, true);
        }

        public void Reboard(Ped cop, VehicleController car)
        {
            if (car == null || car.Destroyed) return;
            if (!car.HasNpcDriver)
            {
                car.PutNpcDriver(cop.Outfit, cop.Role);
                car.Brain = new PoliceDriverAI();
            }
            else car.ExtraOfficers++;
            footCops.Remove(cop);
            Destroy(cop.gameObject);
        }

        public void RegisterFootCop(Ped c) { if (!footCops.Contains(c)) footCops.Add(c); }

        // ---------- visão ----------

        static bool Sees(Vector3 eye, Vector3 forward, Vector3 point, float range, float halfAngle)
        {
            Vector3 to = point - eye;
            float d = to.magnitude;
            if (d > range) return false;
            if (d > 10f && Vector3.Angle(Gen.Flat(forward), Gen.Flat(to)) > halfAngle) return false;
            return !Physics.Linecast(eye, point, ~(1 << 2), QueryTriggerInteraction.Ignore);
        }

        IEnumerable<(Vector3 eye, Vector3 fwd, float range, float angle)> Viewers()
        {
            foreach (var v in VehicleController.All)
                if (v != null && (v.Kind == VehicleKind.Police || v.Kind == VehicleKind.Swat) && v.HasNpcDriver && !v.Destroyed)
                    yield return (v.transform.position + Vector3.up * 1.4f, v.transform.forward, 85f, 65f);
            foreach (var c in footCops)
                if (c != null && !c.IsDead) yield return (c.transform.position + Vector3.up * 1.6f, c.transform.forward, 60f, 70f);
            foreach (var p in Ped.All)
                if (p != null && p.IsCop && !p.IsDead && !footCops.Contains(p)) yield return (p.transform.position + Vector3.up * 1.6f, p.transform.forward, 60f, 70f);
            foreach (var h in helis)
                if (h != null && !h.IsDead) yield return (h.transform.position, Vector3.down, 170f, 180f);
        }

        public bool CanSeePlayer()
        {
            var gm = GameManager.I;
            Vector3 target = gm.PlayerPos + Vector3.up * 1.1f;
            foreach (var w in Viewers())
                if (Sees(w.eye, w.fwd, target, w.range, w.angle)) return true;
            return false;
        }

        public bool CanSeePoint(Vector3 point, float maxRange)
        {
            Vector3 target = point + Vector3.up * 1.1f;
            foreach (var w in Viewers())
                if (Sees(w.eye, w.fwd, target, Mathf.Min(maxRange, w.range), 80f)) return true;
            return false;
        }

        // ---------- fim da perseguição ----------

        public void StandDown()
        {
            foreach (var c in footCops) if (c != null && !c.IsDead) c.State = c.Car != null ? PedState.ReturnToCar : PedState.Wander;
            foreach (var u in units) if (u != null) u.SirenOn = false;
            foreach (var h in helis) if (h != null) h.Leave();
        }

        public void DespawnAll()
        {
            foreach (var u in units) if (u != null) Destroy(u.gameObject);
            foreach (var c in footCops) if (c != null) Destroy(c.gameObject);
            foreach (var h in helis) if (h != null) Destroy(h.gameObject);
            units.Clear(); footCops.Clear(); helis.Clear();
            foreach (var p in Ped.All.ToArray())
                if (p != null && (p.Hostile || p.IsCop) && !p.Persistent) Destroy(p.gameObject);
        }
    }

    /// <summary>Motorista de viatura: persegue pelo grafo de ruas, cerca, bate (3★+) e procura na área.</summary>
    public class PoliceDriverAI : IVehicleBrain
    {
        List<Vector2Int> path = new List<Vector2Int>();
        float repathT, stuckT, reverseT;
        Vector3 searchTarget;
        bool hasSearch;

        public void Drive(VehicleController v, float dt)
        {
            var gm = GameManager.I;
            var w = gm.Wanted;
            Vector3 pos = v.transform.position;
            float speed = v.Speed;
            v.SirenOn = w.Stars > 0;

            Vector3 goal;
            float desired;
            if (w.Stars == 0)
            {
                // vai embora devagar
                if (!hasSearch || Gen.FlatDist(pos, searchTarget) < 20f) { searchTarget = pos + Random.onUnitSphere * 300f; searchTarget.y = 0; hasSearch = true; }
                goal = searchTarget;
                desired = 12f;
            }
            else if (w.Seen)
            {
                goal = gm.PlayerPos;
                desired = w.Stars >= 3 ? 55f : 42f;
                hasSearch = false;
            }
            else
            {
                // procurando: rodeia a última posição conhecida
                if (!hasSearch || Gen.FlatDist(pos, searchTarget) < 18f)
                {
                    Vector2 r = Random.insideUnitCircle * (40f + w.Stars * 20f);
                    searchTarget = w.LastKnown + new Vector3(r.x, 0, r.y);
                    hasSearch = true;
                }
                goal = searchTarget;
                desired = 20f;
            }

            float distGoal = Gen.FlatDist(pos, goal);
            var player = gm.Player;

            // jogador a pé e perto: para e desce
            if (w.Stars > 0 && w.Seen && player.Vehicle == null && distGoal < 20f && !player.IsDead)
            {
                v.SetInput(speed > 2f ? -1f : 0f, 0f, speed < 3f);
                if (speed < 3f) gm.Police.Disembark(v);
                return;
            }

            // rota: pelas ruas quando longe, direto quando perto
            Vector3 target = goal;
            if (distGoal > 50f && gm.City.InsideCity(goal))
            {
                repathT -= dt;
                if (repathT <= 0f || path.Count == 0)
                {
                    repathT = 1f;
                    path = gm.City.FindPath(gm.City.NearestNode(pos), gm.City.NearestNode(goal));
                }
                while (path.Count > 1 && Gen.FlatDist(pos, CityGenerator.NodePos(path[0])) < 12f) path.RemoveAt(0);
                if (path.Count > 0) target = CityGenerator.NodePos(path[0]);
            }
            else if (player.Vehicle != null && w.Seen)
            {
                // mira um pouco à frente do carro do jogador (intercepta)
                target = goal + player.Vehicle.Body.linearVelocity * Mathf.Clamp(distGoal / 40f, 0f, 1.2f);
            }

            Vector3 fwd = v.transform.forward;
            Vector3 to = Gen.Flat(target - pos);
            float angle = Vector3.SignedAngle(fwd, to, Vector3.up);
            float steer = Mathf.Clamp(angle / 30f, -1f, 1f);

            // desvia de prédios
            Vector3 eye = pos + Vector3.up * 0.8f + fwd * (v.Spec.Length * 0.5f);
            float probe = 6f + speed * 0.45f;
            bool hitL = Physics.Raycast(eye, Quaternion.Euler(0, -22f, 0) * fwd, out var hl, probe, ~(1 << 2), QueryTriggerInteraction.Ignore) && hl.rigidbody == null;
            bool hitR = Physics.Raycast(eye, Quaternion.Euler(0, 22f, 0) * fwd, out var hr, probe, ~(1 << 2), QueryTriggerInteraction.Ignore) && hr.rigidbody == null;
            if (hitL && !hitR) steer = Mathf.Max(steer, 0.6f);
            if (hitR && !hitL) steer = Mathf.Min(steer, -0.6f);

            if (Mathf.Abs(angle) > 50f) desired = Mathf.Min(desired, 11f);
            // poucas estrelas: acompanha em vez de bater
            if (w.Stars <= 2 && player.Vehicle != null && distGoal < 14f) desired = Mathf.Min(desired, player.Vehicle.Speed + 2f);

            if (reverseT > 0f)
            {
                reverseT -= dt;
                v.SetInput(-1f, -steer, false);
                return;
            }
            if (speed < 1f && desired > 4f) stuckT += dt; else stuckT = 0f;
            if (stuckT > 2.2f) { stuckT = 0f; reverseT = 1.3f; }

            float throttle = speed < desired ? 1f : (speed - desired > 4f ? -0.6f : 0f);
            // curva fechada em alta: freio de mão para "puxar" a traseira
            bool hb = Mathf.Abs(angle) > 70f && speed > 14f;
            v.SetInput(throttle, steer, hb);
        }
    }
}

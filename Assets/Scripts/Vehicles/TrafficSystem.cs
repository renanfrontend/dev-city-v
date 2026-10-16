using System.Collections.Generic;
using UnityEngine;

namespace DevCity
{
    /// <summary>
    /// Motorista comum: segue a faixa da direita, vira nos cruzamentos, respeita o semáforo,
    /// freia para carros/pedestres à frente, buzina, dá ré quando trava e foge quando há tiroteio.
    /// </summary>
    public class TrafficAI : IVehicleBrain
    {
        public Vector2Int From, To, Next;
        public float Cruise;
        public bool IgnoreLights;
        public bool Fleeing => panicT > 0f;
        float panicT, stuckT, reverseT, honkT;
        Vector3 panicFrom;
        readonly CityGenerator city;

        public TrafficAI(Vector2Int from, Vector2Int to, float cruise = -1f)
        {
            city = GameManager.I.City;
            From = from; To = to;
            Next = PickNext(To, From);
            Cruise = cruise > 0 ? cruise : Random.Range(10f, 14.5f);
        }

        public void Panic(Vector3 from)
        {
            panicT = 15f;
            panicFrom = from;
        }

        Vector2Int PickNext(Vector2Int at, Vector2Int came)
        {
            var nbs = city.Neighbors(at);
            if (nbs.Count > 1) nbs.Remove(came);
            if (panicT > 0f)
            {
                // fugindo: escolhe a rua que mais se afasta do perigo
                Vector2Int best = nbs[0]; float bd = -1f;
                foreach (var n in nbs)
                {
                    float d = Vector3.Distance(CityGenerator.NodePos(n), panicFrom);
                    if (d > bd) { bd = d; best = n; }
                }
                return best;
            }
            return nbs[Random.Range(0, nbs.Count)];
        }

        public void Drive(VehicleController v, float dt)
        {
            var gm = GameManager.I;
            panicT -= dt;
            Vector3 pos = v.transform.position;
            Vector3 fwd = v.transform.forward;
            float speed = v.Speed;

            Vector3 a = CityGenerator.LaneStart(From, To), b = CityGenerator.LaneEnd(From, To);
            Vector3 seg = b - a;
            float len = seg.magnitude;
            Vector3 dir = seg / len;
            float t = Vector3.Dot(pos - a, dir);

            Vector3 na = CityGenerator.LaneStart(To, Next);
            Vector3 nd = (CityGenerator.LaneEnd(To, Next) - na).normalized;
            // já entrou na próxima rua: avança o trecho
            if (t > len - 1f && Vector3.Dot(pos - na, nd) > -2f)
            {
                From = To; To = Next; Next = PickNext(To, From);
                return;
            }

            float look = 5f + speed * 0.55f;
            Vector3 target;
            if (t + look < len) target = a + dir * (t + look);
            else target = na + nd * Mathf.Max(0f, t + look - len - CityGenerator.ROAD * 0.5f);

            float steer = Mathf.Clamp(Vector3.SignedAngle(fwd, Gen.Flat(target - pos), Vector3.up) / 30f, -1f, 1f);

            float desired = Cruise * (panicT > 0f ? 1.9f : 1f);
            bool turning = Vector3.Dot(dir, nd) < 0.7f;
            float toEnd = len - t;
            if (turning && toEnd < 20f) desired = Mathf.Min(desired, 6.5f + toEnd * 0.3f);

            // semáforo (eixo 0 = rua norte-sul)
            if (!IgnoreLights && panicT <= 0f && toEnd > 0f && toEnd < 22f)
            {
                int axis = Mathf.Abs(dir.x) > Mathf.Abs(dir.z) ? 1 : 0;
                bool green = gm.DayNight.IsGreenFor(axis);
                bool yellow = gm.DayNight.IsYellowFor(axis);
                if (!green && !(yellow && toEnd < 6f))
                    desired = Mathf.Min(desired, Mathf.Max(0f, (toEnd - 3.5f) * 0.7f));
            }

            // obstáculo à frente: carros, pedestres, jogador
            Vector3 bumper = pos + fwd * (v.Spec.Length * 0.5f + 0.4f) + Vector3.up * 0.8f;
            float range = 4f + speed * 1.3f;
            bool blockedByPlayer = false;
            if (Physics.SphereCast(bumper, 0.7f, fwd, out var hit, range, ~0, QueryTriggerInteraction.Collide))
            {
                var ped = hit.collider.GetComponentInParent<Ped>();
                bool relevant = hit.collider.attachedRigidbody != null || (ped != null && !ped.IsDead) || hit.collider.GetComponentInParent<PlayerController>() != null;
                if (relevant && panicT <= 0f)
                {
                    desired = Mathf.Min(desired, Mathf.Max(0f, (hit.distance - 1.5f) * 0.7f));
                    var pv = hit.collider.GetComponentInParent<VehicleController>();
                    blockedByPlayer = (pv != null && pv.PlayerDriving) || hit.collider.GetComponentInParent<PlayerController>() != null;
                }
            }

            // buzina para o jogador parado na frente
            honkT -= dt;
            if (blockedByPlayer && speed < 1f && honkT <= 0f) { honkT = Random.Range(2f, 4f); v.Horn(); }

            // travado (não por semáforo): dá ré e tenta de novo
            if (reverseT > 0f)
            {
                reverseT -= dt;
                v.SetInput(-0.7f, -steer, false);
                return;
            }
            if (desired > 3f && speed < 0.6f) stuckT += dt; else stuckT = 0f;
            if (stuckT > 4f) { stuckT = 0f; reverseT = 1.4f; }

            float throttle;
            if (speed < desired) throttle = Mathf.Clamp((desired - speed) * 0.35f, 0.15f, 1f);
            else throttle = speed - desired > 1.5f ? -Mathf.Clamp((speed - desired) * 0.25f, 0.2f, 1f) : 0f;
            bool hold = desired < 0.5f && speed < 1.2f;
            if (hold) throttle = 0f;
            v.SetInput(throttle, steer, hold);
        }
    }

    /// <summary>Cria e remove carros perto do jogador (trânsito, estacionados e viaturas de ronda).</summary>
    public class TrafficSystem : MonoBehaviour
    {
        public int TargetMoving = 22;
        public int TargetParked = 10;
        public int TargetPatrol = 2;
        float timer;

        public void Init()
        {
            for (int i = 0; i < 14; i++) SpawnMoving(30f, 160f, false);
            for (int i = 0; i < TargetParked; i++) SpawnParked(15f, 140f);
        }

        void Update()
        {
            var gm = GameManager.I;
            if (gm == null || !(gm.Playing || gm.State == GameState.Title)) return;
            timer -= Time.deltaTime;
            if (timer > 0f) return;
            timer = 0.4f;

            Vector3 p = gm.PlayerPos;
            int moving = 0, parked = 0, patrol = 0;
            for (int i = VehicleController.All.Count - 1; i >= 0; i--)
            {
                var v = VehicleController.All[i];
                if (v == null) continue;
                float d = Gen.FlatDist(v.transform.position, p);
                bool keep = v.Persistent || v.PlayerDriving || gm.Police.IsUnit(v);
                if (!keep && d > 230f && !Visible(v.transform.position)) { Destroy(v.gameObject); continue; }
                if (!keep && v.Destroyed && d > 120f) { Destroy(v.gameObject); continue; }
                if (v.Brain is TrafficAI) { if (v.Kind == VehicleKind.Police) patrol++; else moving++; }
                else if (!v.HasNpcDriver && !v.Destroyed && !keep) parked++;
            }

            int target = gm.DayNight.IsNight ? TargetMoving - 8 : TargetMoving;
            if (moving < target) SpawnMoving(95f, 180f, true);
            if (parked < TargetParked) SpawnParked(80f, 150f);
            if (patrol < TargetPatrol && gm.Wanted.Stars == 0) SpawnMoving(120f, 190f, true, true);
        }

        public static bool Visible(Vector3 pos)
        {
            var cam = GameManager.I.Cam;
            if (cam == null) return false;
            Vector3 vp = cam.Camera.WorldToViewportPoint(pos);
            return vp.z > 0f && vp.z < 160f && vp.x > -0.15f && vp.x < 1.15f && vp.y > -0.2f && vp.y < 1.2f;
        }

        public VehicleController SpawnMoving(float minD, float maxD, bool hidden, bool police = false)
        {
            var gm = GameManager.I;
            for (int tries = 0; tries < 6; tries++)
            {
                if (!gm.City.RandomLanePoint(gm.PlayerPos, minD, maxD, out var pos, out var from, out var to)) continue;
                if (hidden && Visible(pos)) continue;
                if (Occupied(pos, 9f)) continue;
                Vector3 dir = (CityGenerator.NodePos(to) - CityGenerator.NodePos(from)).normalized;
                var kind = police ? VehicleKind.Police : VehicleFactory.RandomCivilianKind();
                var v = VehicleFactory.Create(kind, pos, Quaternion.LookRotation(dir));
                v.PutNpcDriver(police ? Outfit.Cop() : Outfit.Random(), police ? PedRole.Cop : PedRole.Civilian);
                if (police) v.ExtraOfficers = 1;
                var ai = new TrafficAI(from, to);
                v.Brain = ai;
                v.Body.linearVelocity = dir * ai.Cruise * 0.8f;
                return v;
            }
            return null;
        }

        void SpawnParked(float minD, float maxD)
        {
            var gm = GameManager.I;
            for (int tries = 0; tries < 6; tries++)
            {
                if (!gm.City.RandomLanePoint(gm.PlayerPos, minD, maxD, out var pos, out var from, out var to)) continue;
                Vector3 dir = (CityGenerator.NodePos(to) - CityGenerator.NodePos(from)).normalized;
                // encosta no meio-fio (fora da faixa de rolamento)
                pos += CityGenerator.RightOf(dir) * (CityGenerator.ROAD * 0.5f - CityGenerator.LANE - 1.15f);
                if (Visible(pos) && Gen.FlatDist(pos, gm.PlayerPos) < 60f) continue;
                if (Occupied(pos, 7f)) continue;
                var v = VehicleFactory.Create(VehicleFactory.RandomCivilianKind(), pos, Quaternion.LookRotation(dir));
                v.SetInput(0f, 0f, true);
                return;
            }
        }

        public static bool Occupied(Vector3 pos, float radius)
        {
            foreach (var v in VehicleController.All)
                if (v != null && Gen.FlatDist(v.transform.position, pos) < radius) return true;
            return false;
        }

        /// <summary>Tiroteio por perto: os motoristas fogem.</summary>
        public void Panic(Vector3 pos, float radius)
        {
            foreach (var v in VehicleController.All)
                if (v != null && v.Brain is TrafficAI t && v.Kind != VehicleKind.Police && Gen.FlatDist(v.transform.position, pos) < radius)
                    t.Panic(pos);
        }
    }
}

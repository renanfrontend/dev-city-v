using UnityEngine;

namespace DevCity
{
    /// <summary>Povoa as calçadas em volta do jogador e cuida do pânico e das testemunhas.</summary>
    public class PedestrianSystem : MonoBehaviour
    {
        public int TargetDay = 34, TargetNight = 18;
        float timer;

        public void Init()
        {
            for (int i = 0; i < 20; i++) SpawnNear(15f, 90f, false);
        }

        void Update()
        {
            var gm = GameManager.I;
            if (gm == null || !(gm.Playing || gm.State == GameState.Title)) return;
            timer -= Time.deltaTime;
            if (timer > 0f) return;
            timer = 0.35f;

            Vector3 p = gm.PlayerPos;
            int civ = 0;
            for (int i = Ped.All.Count - 1; i >= 0; i--)
            {
                var ped = Ped.All[i];
                if (ped == null) continue;
                float d = Gen.FlatDist(ped.transform.position, p);
                if (ped.Persistent) continue;
                if (ped.IsDead && (Time.time - ped.DeadTime > 45f || d > 90f)) { Destroy(ped.gameObject); continue; }
                if (ped.Role == PedRole.Civilian)
                {
                    if (d > 130f && !TrafficSystem.Visible(ped.transform.position)) { Destroy(ped.gameObject); continue; }
                    if (!ped.IsDead) civ++;
                }
                else if (!ped.IsCop && d > 220f) Destroy(ped.gameObject);
            }

            int target = gm.DayNight.IsNight ? TargetNight : TargetDay;
            if (civ < target) SpawnNear(50f, 100f, true);
        }

        void SpawnNear(float minD, float maxD, bool hidden)
        {
            var gm = GameManager.I;
            Vector3 p = gm.PlayerPos;
            for (int tries = 0; tries < 8; tries++)
            {
                var c = CityGenerator.NearestCorner(p + new Vector3(Random.Range(-maxD, maxD), 0, Random.Range(-maxD, maxD)));
                // um ponto qualquer no lado da quadra entre dois cantos
                Vector3 a = CityGenerator.Corner(c.x, c.y, c.z);
                Vector3 b = CityGenerator.Corner(c.x, c.y, (c.z + 1) % 4);
                Vector3 pos = Vector3.Lerp(a, b, Random.value);
                float d = Gen.FlatDist(pos, p);
                if (d < minD || d > maxD) continue;
                if (hidden && TrafficSystem.Visible(pos) && d < 75f) continue;
                Ped.Spawn(pos, PedRole.Civilian);
                return;
            }
        }

        /// <summary>Tiro/explosão: pedestres fogem ou se encolhem; motoristas aceleram para longe.</summary>
        public void Panic(Vector3 pos, float radius)
        {
            foreach (var ped in Ped.All)
                if (ped != null && !ped.IsDead && Gen.FlatDist(ped.transform.position, pos) < radius)
                    ped.React(pos);
            GameManager.I.Traffic.Panic(pos, radius);
        }

        /// <summary>Um civil vivo que consegue ver o local do crime.</summary>
        public Ped FindWitness(Vector3 pos, float radius)
        {
            Ped best = null;
            float bd = radius;
            foreach (var ped in Ped.All)
            {
                if (ped == null || ped.IsDead || ped.Role != PedRole.Civilian || ped.State == PedState.Calling) continue;
                float d = Vector3.Distance(ped.transform.position, pos);
                if (d > bd) continue;
                if (Physics.Linecast(ped.transform.position + Vector3.up * 1.6f, pos + Vector3.up * 1.2f, ~(1 << 2), QueryTriggerInteraction.Ignore)) continue;
                bd = d;
                best = ped;
            }
            // nem toda testemunha liga (umas fogem direto)
            if (best != null && Random.value < 0.75f) return best;
            return null;
        }

        /// <summary>Jogador apontando a arma para alguém: a pessoa levanta as mãos ou foge.</summary>
        void LateUpdate()
        {
            var gm = GameManager.I;
            if (gm == null || !gm.Playing || !gm.Player.Weapons.Aiming || gm.Player.Vehicle != null) return;
            var cam = gm.Cam.transform;
            foreach (var ped in Ped.All)
            {
                if (ped == null || ped.IsDead || ped.Role != PedRole.Civilian) continue;
                if (ped.State == PedState.HandsUp || ped.State == PedState.Flee || ped.State == PedState.Cower) continue;
                Vector3 to = ped.Chest - cam.position;
                if (to.magnitude < 22f && Vector3.Angle(cam.forward, to) < 6f)
                {
                    if (Random.value < 0.5f) ped.HandsUp(Random.Range(3f, 6f)); else ped.Flee(gm.PlayerPos, 10f);
                }
            }
        }
    }
}

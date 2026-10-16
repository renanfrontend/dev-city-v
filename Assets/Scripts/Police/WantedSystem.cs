using UnityEngine;

namespace DevCity
{
    public enum Crime { Gunfire, CarTheft, StealPoliceCar, RamPolice, Assault, HitPed, KillCivilian, AttackCop, KillCop, Robbery, Explosion, DestroyPoliceVehicle }

    /// <summary>
    /// Nível de procurado inspirado no GTA V:
    /// - cada crime tem um mínimo de estrelas; a polícia vê na hora (cone de visão + linha de visada),
    ///   civis precisam ligar (demora alguns segundos e dá para impedir);
    /// - perseguição: estrelas sólidas; quando a polícia perde você de vista, as estrelas piscam;
    /// - fora de vista por 30/45/60/75/90 s (1 a 5 estrelas) e o nível zera;
    /// - continuar atacando a polícia aumenta o nível.
    /// </summary>
    public class WantedSystem : MonoBehaviour
    {
        public int Stars { get; private set; }
        public bool Seen { get; private set; }
        public bool Searching => Stars > 0 && unseen > 1.5f;
        public Vector3 LastKnown { get; private set; }
        public float UnseenTime => unseen;
        public float ClearProgress => Stars == 0 ? 0f : Mathf.Clamp01(unseen / ClearTime[Stars]);

        public static readonly float[] ClearTime = { 0f, 30f, 45f, 60f, 75f, 90f };

        float unseen, copKills, civKills, lastCrime;

        public static int MinStars(Crime c)
        {
            switch (c)
            {
                case Crime.AttackCop: case Crime.KillCop: case Crime.DestroyPoliceVehicle: return 3;
                case Crime.Robbery: case Crime.Explosion: case Crime.StealPoliceCar: return 2;
                default: return 1;
            }
        }

        /// <summary>O jogador fez algo errado em 'pos'. Decide quem viu e quantas estrelas dá.</summary>
        public void Commit(Crime c, Vector3 pos)
        {
            var gm = GameManager.I;
            if (gm.State == GameState.Title) return;
            if (c == Crime.Gunfire || c == Crime.AttackCop || c == Crime.Assault || c == Crime.KillCop) gm.Player.LastAggro = Time.time;

            if (c == Crime.KillCop) copKills++;
            if (c == Crime.KillCivilian) civKills++;

            // crimes contra a polícia: ela sabe na hora
            bool copsKnow = c == Crime.AttackCop || c == Crime.KillCop || c == Crime.DestroyPoliceVehicle || c == Crime.RamPolice;
            if (!copsKnow) copsKnow = (Stars > 0 && Seen) || gm.Police.CanSeePoint(pos, 70f);

            if (copsKnow) Raise(c, pos);
            else
            {
                // um civil por perto pode ligar para a polícia
                var w = gm.Peds.FindWitness(pos, c == Crime.Gunfire || c == Crime.Explosion ? 45f : 30f);
                if (w != null) w.CallPolice(c, pos);
            }
        }

        /// <summary>A ligação do civil terminou (ele não foi impedido).</summary>
        public void CivilianReport(Crime c, Vector3 pos)
        {
            Raise(c, pos);
            GameManager.I.Hud.Notify("Uma testemunha chamou a polícia.");
        }

        void Raise(Crime c, Vector3 pos)
        {
            int target = Mathf.Max(Stars, MinStars(c));
            // continuar matando policiais/civis com a polícia atrás aumenta o nível
            if (c == Crime.KillCop && Stars >= 3 && copKills >= 2) { target = Mathf.Max(target, Stars + 1); copKills = 0; }
            if (c == Crime.KillCivilian && civKills >= 4) { target = Mathf.Max(target, 2); civKills = 0; }
            if (c == Crime.Gunfire && Stars >= 1 && Seen && Time.time - lastCrime < 2f && Stars < 2) target = 2;
            lastCrime = Time.time;
            SetStars(target, pos);
        }

        public void SetStars(int s, Vector3 pos)
        {
            s = Mathf.Clamp(s, 0, 5);
            bool up = s > Stars;
            Stars = s;
            LastKnown = pos;
            unseen = 0f;
            Seen = true;
            if (up) GameManager.I.Hud.FlashStars();
        }

        public void Clear()
        {
            Stars = 0;
            unseen = 0f;
            Seen = false;
            copKills = civKills = 0f;
        }

        void Update()
        {
            var gm = GameManager.I;
            if (gm == null || Stars == 0 || !gm.Playing) return;
            Seen = gm.Police.CanSeePlayer();
            if (Seen)
            {
                LastKnown = gm.PlayerPos;
                unseen = 0f;
            }
            else
            {
                unseen += Time.deltaTime;
                if (unseen >= ClearTime[Stars])
                {
                    Clear();
                    gm.Hud.Notify("Você despistou a polícia.");
                    gm.Police.StandDown();
                }
            }
            // civis matando o tempo: crimes antigos de civis são esquecidos
            civKills = Mathf.Max(0f, civKills - Time.deltaTime / 60f);
        }
    }
}

using System.Collections.Generic;
using UnityEngine;

namespace DevCity
{
    /// <summary>
    /// Missões no formato GTA: contatos marcados no radar com a inicial, corona amarela para começar,
    /// cartão de título, objetivos na legenda com palavras coloridas, GPS automático,
    /// "MISSÃO CUMPRIDA"/"MISSÃO FALHOU" e opção de tentar de novo.
    /// </summary>
    public class MissionSystem : MonoBehaviour
    {
        public const string Yellow = "#ffd84a", Blue = "#5fb4ff", Red = "#ff5a5a", Green = "#5fe08a";

        public int Done;
        public float RaceBest;
        public Mission Active { get; private set; }
        public Mission LastFailed { get; private set; }
        public readonly List<Transform> EnemyBlips = new List<Transform>();

        readonly List<Mission> story = new List<Mission>();
        RaceMission race;
        readonly Dictionary<PlaceType, Ped> givers = new Dictionary<PlaceType, Ped>();
        readonly Dictionary<PlaceType, Marker> startMarkers = new Dictionary<PlaceType, Marker>();
        Marker targetMarker, targetArrow;
        float retryT, startCooldown;

        public static (string name, string letter, Color color) GiverInfo(PlaceType t)
        {
            switch (t)
            {
                case PlaceType.MissionD: return ("Dudu", "D", new Color(1f, 0.6f, 0.2f));
                case PlaceType.MissionN: return ("Dona Neide", "N", new Color(1f, 0.45f, 0.7f));
                case PlaceType.MissionG: return ("Gordo", "G", new Color(0.4f, 0.9f, 0.4f));
                case PlaceType.MissionP: return ("Professor", "P", new Color(0.7f, 0.5f, 1f));
                default: return ("?", "?", Color.white);
            }
        }

        public IEnumerable<Mission> Story => story;
        public Mission NextStory => Done < story.Count ? story[Done] : null;
        public bool InMission => Active != null;

        public void Init()
        {
            story.Add(new MissionStolenCar());
            story.Add(new MissionDebt());
            story.Add(new MissionStoreRobbery());
            story.Add(new MissionTurf());
            story.Add(new MissionHeist());
            race = new RaceMission();

            var city = GameManager.I.City;
            foreach (var t in new[] { PlaceType.MissionD, PlaceType.MissionN, PlaceType.MissionG, PlaceType.MissionP })
            {
                var place = city.Places[t];
                var info = GiverInfo(t);
                var ped = Ped.Spawn(place.Door + Vector3.forward * 1.2f, PedRole.Giver, t == PlaceType.MissionP ? SuitOutfit() : (Outfit?)null);
                ped.transform.rotation = Quaternion.LookRotation(Vector3.back);
                ped.name = info.name;
                givers[t] = ped;
            }
            RefreshGivers();
        }

        static Outfit SuitOutfit()
        {
            var o = Outfit.Random();
            o.Shirt = new Color(0.15f, 0.15f, 0.18f); o.Pants = new Color(0.15f, 0.15f, 0.18f); o.Style = 4; o.Cap = false;
            return o;
        }

        /// <summary>Mostra a corona só no contato da próxima missão (história linear) e no racha.</summary>
        public void RefreshGivers()
        {
            foreach (var m in startMarkers.Values) if (m != null) Destroy(m.gameObject);
            startMarkers.Clear();
            if (Active != null) return;
            var city = GameManager.I.City;
            var next = NextStory;
            if (next != null) startMarkers[next.GiverPlace] = Marker.Corona(city.Places[next.GiverPlace].Door, new Color(1f, 0.82f, 0.2f));
            startMarkers[PlaceType.Race] = Marker.Corona(city.Places[PlaceType.Race].Door, new Color(1f, 0.82f, 0.2f), 3f);
        }

        /// <summary>Lugares que aparecem no radar com letra (contato da próxima missão e racha).</summary>
        public IEnumerable<PlaceType> ActiveGiverPlaces()
        {
            if (Active != null) yield break;
            var next = NextStory;
            if (next != null) yield return next.GiverPlace;
            yield return PlaceType.Race;
        }

        void Update()
        {
            var gm = GameManager.I;
            if (gm == null || !gm.Playing) return;
            EnemyBlips.RemoveAll(t => t == null);

            if (Active != null)
            {
                Active.Tick(Time.deltaTime);
                return;
            }

            retryT -= Time.deltaTime;
            startCooldown -= Time.deltaTime;
            if (LastFailed != null && retryT > 0f)
            {
                gm.Hud.Help("Aperte <b>Enter</b> para tentar a missão de novo.", 0.15f);
                if (Input.GetKeyDown(KeyCode.Return)) { var m = LastFailed; LastFailed = null; StartMission(m); }
                return;
            }

            // entrar na corona do contato (depois de um tempinho, para não recomeçar sem querer)
            if (startCooldown > 0f) return;
            var city = gm.City;
            var player = gm.Player;
            var next = NextStory;
            if (next != null && player.Vehicle == null && Gen.FlatDist(player.Position, city.Places[next.GiverPlace].Door) < 1.6f && gm.Wanted.Stars == 0)
                StartMission(next);
            if (Gen.FlatDist(player.Position, city.Places[PlaceType.Race].Door) < 4f)
            {
                if (player.Vehicle == null) gm.Hud.Help("Entre num carro para participar do <b>Racha da Orla</b>.", 0.15f);
                else if (gm.Wanted.Stars > 0) gm.Hud.Help("Despiste a polícia antes do racha.", 0.15f);
                else StartMission(race);
            }
        }

        void StartMission(Mission m)
        {
            var gm = GameManager.I;
            Active = m;
            RefreshGivers();
            gm.Hud.MissionTitle(m.Title, m.Giver);
            m.Begin();
        }

        // ---------- ferramentas para as missões ----------

        public void Objective(string text) => GameManager.I.Hud.Objective(text);

        public void TargetPoint(Vector3 p, Color c, bool corona = true)
        {
            ClearTarget();
            var gps = GameManager.I.Gps;
            gps.MissionTarget = p;
            gps.MissionColor = c;
            if (corona) targetMarker = Marker.Corona(p, new Color(1f, 0.82f, 0.2f));
        }

        public void TargetEntity(Transform t, Color c, float arrowHeight = 2.6f)
        {
            ClearTarget();
            var gps = GameManager.I.Gps;
            gps.MissionTargetTransform = t;
            gps.MissionColor = c;
            targetArrow = Marker.Arrow(t, c, arrowHeight);
        }

        public void ClearTarget()
        {
            var gps = GameManager.I.Gps;
            gps.MissionTarget = null;
            gps.MissionTargetTransform = null;
            if (targetMarker != null) Destroy(targetMarker.gameObject);
            if (targetArrow != null) Destroy(targetArrow.gameObject);
        }

        public void Pass(int reward, string extra = null)
        {
            var gm = GameManager.I;
            var m = Active;
            if (m == null) return;
            m.Cleanup();
            ClearTarget();
            EnemyBlips.Clear();
            gm.Hud.Objective(null);
            Active = null;
            LastFailed = null;
            if (!(m is RaceMission)) Done++;
            startCooldown = 8f;
            gm.AddMoney(reward);
            Sfx.Play2D(Sfx.Passed, 0.9f);
            gm.Hud.BigMessage("MISSÃO CUMPRIDA", $"<color=#9cff9c>+${reward:N0}</color>" + (extra != null ? "\n" + extra : ""), new Color(1f, 0.8f, 0.2f), 4.5f);
            RefreshGivers();
            SaveSystem.Save();
        }

        public void Fail(string reason)
        {
            var gm = GameManager.I;
            var m = Active;
            if (m == null) return;
            m.Cleanup();
            ClearTarget();
            EnemyBlips.Clear();
            gm.Hud.Objective(null);
            Active = null;
            LastFailed = m;
            retryT = 10f;
            startCooldown = 8f;
            Sfx.Play2D(Sfx.Failed, 0.9f);
            gm.Hud.BigMessage("MISSÃO FALHOU", reason, new Color(1f, 0.25f, 0.25f), 4f);
            RefreshGivers();
        }

        /// <summary>Chamado ao morrer/ser preso.</summary>
        public void FailActive(string reason)
        {
            if (Active != null) Fail(reason);
        }
    }

    // =====================================================================
    //  Base das missões
    // =====================================================================

    public abstract class Mission
    {
        public string Title, Giver;
        public PlaceType GiverPlace;
        protected int Step;
        protected readonly List<GameObject> spawned = new List<GameObject>();

        protected GameManager GM => GameManager.I;
        protected MissionSystem M => GameManager.I.Missions;
        protected CityGenerator City => GameManager.I.City;
        protected PlayerController Player => GameManager.I.Player;

        public abstract void Begin();
        public abstract void Tick(float dt);

        public virtual void Cleanup()
        {
            foreach (var go in spawned) if (go != null) Object.Destroy(go);
            spawned.Clear();
        }

        protected T Track<T>(T c) where T : Component { if (c != null) spawned.Add(c.gameObject); return c; }

        protected bool Arrived(Vector3 p, float radius = 4f) => Gen.FlatDist(Player.Position, p) < radius;

        /// <summary>Carro estacionado no meio-fio perto de um ponto (para a missão).</summary>
        protected VehicleController ParkedCar(VehicleKind kind, Vector3 near, float minD, float maxD, Color? paint, out Vector2Int from, out Vector2Int to)
        {
            Vector3 pos;
            if (!City.RandomLanePoint(near, minD, maxD, out pos, out from, out to))
                City.RandomLanePoint(near, 0f, maxD * 2f, out pos, out from, out to);
            Vector3 dir = (CityGenerator.NodePos(to) - CityGenerator.NodePos(from)).normalized;
            pos += CityGenerator.RightOf(dir) * (CityGenerator.ROAD * 0.5f - CityGenerator.LANE - 1.15f);
            var v = VehicleFactory.Create(kind, pos, Quaternion.LookRotation(dir), paint);
            v.Persistent = true;
            v.SetInput(0f, 0f, true);
            return v;
        }
    }

    // ---------------------------------------------------------------------
    // 1) Carro Novo — Dudu: roube um esportivo e entregue na oficina sem polícia.
    // ---------------------------------------------------------------------
    public class MissionStolenCar : Mission
    {
        VehicleController car;

        public MissionStolenCar() { Title = "Carro Novo"; Giver = "Dudu"; GiverPlace = PlaceType.MissionD; }

        public override void Begin()
        {
            Step = 0;
            var bar = City.Places[PlaceType.Clothes].Door;
            car = ParkedCar(VehicleKind.Sports, bar, 20f, 70f, new Color(0.8f, 0.05f, 0.05f), out _, out _);
            spawned.Add(car.gameObject);
            M.Objective($"Dudu: \"Tem um <color={MissionSystem.Blue}>Fúria GT</color> vermelho dando sopa perto do shopping. Traz ele pra mim.\"");
            M.TargetEntity(car.transform, new Color(0.37f, 0.7f, 1f));
        }

        public override void Tick(float dt)
        {
            if (car == null || car.Destroyed) { M.Fail("O Fúria GT foi destruído."); return; }
            var garage = City.Places[PlaceType.Garage].Inside;
            bool inCar = Player.Vehicle == car;
            switch (Step)
            {
                case 0:
                    if (inCar)
                    {
                        Step = 1;
                        M.Objective($"Leve o carro até a <color={MissionSystem.Yellow}>Oficina do Dudu</color>. Cuidado com a lataria!");
                        M.TargetPoint(garage, new Color(1f, 0.85f, 0.2f));
                    }
                    break;
                case 1:
                    if (!inCar)
                    {
                        Step = 0;
                        M.Objective($"Volte para o <color={MissionSystem.Blue}>Fúria GT</color>.");
                        M.TargetEntity(car.transform, new Color(0.37f, 0.7f, 1f));
                    }
                    else if (Arrived(garage, 5f))
                    {
                        if (GM.Wanted.Stars > 0) GM.Hud.Help("Despiste a polícia antes de entrar na oficina!", 0.15f);
                        else if (car.Speed < 2f) { Step = 2; M.Objective("Saia do carro."); M.ClearTarget(); }
                    }
                    break;
                case 2:
                    if (!inCar)
                    {
                        float hp = car.Health / car.Spec.Health;
                        int pay = Mathf.RoundToInt(700f + 1300f * hp);
                        car.Persistent = false;
                        spawned.Remove(car.gameObject); // o carro fica na oficina
                        M.Pass(pay, hp < 0.9f ? "Dudu descontou os arranhões." : "Dudu: \"Impecável!\"");
                    }
                    break;
            }
        }
    }

    // ---------------------------------------------------------------------
    // 2) Cobrança — Dona Neide: o devedor foge de carro; pare-o e pegue a grana.
    // ---------------------------------------------------------------------
    public class MissionDebt : Mission
    {
        Ped debtor;
        VehicleController car;
        Vector2Int from, to;
        Pickup bag;

        public MissionDebt() { Title = "Cobrança"; Giver = "Dona Neide"; GiverPlace = PlaceType.MissionN; }

        public override void Begin()
        {
            Step = 0;
            var bar = City.Places[PlaceType.Bar];
            car = ParkedCar(VehicleKind.Sedan, bar.Door, 8f, 30f, new Color(0.3f, 0.32f, 0.35f), out from, out to);
            spawned.Add(car.gameObject);
            debtor = Track(Ped.Spawn(bar.Door + Vector3.right * 2f, PedRole.Target));
            debtor.Persistent = true;
            debtor.State = PedState.Idle;
            M.Objective($"Neide: \"O <color={MissionSystem.Red}>Mão-de-Vaca</color> me deve. Ele bebe no <color={MissionSystem.Yellow}>Bar do Português</color>.\"");
            M.TargetPoint(bar.Door, new Color(1f, 0.85f, 0.2f), false);
        }

        public override void Tick(float dt)
        {
            switch (Step)
            {
                case 0:
                    if (debtor == null || debtor.IsDead) { DropBag(debtor != null ? debtor.transform.position : car.transform.position); return; }
                    if (Arrived(debtor.transform.position, 28f))
                    {
                        // ele te vê e foge de carro
                        Object.Destroy(debtor.gameObject);
                        car.PutNpcDriver(debtor.Outfit, PedRole.Target);
                        var ai = new TrafficAI(from, to, 21f) { IgnoreLights = true };
                        ai.Panic(Player.Position);
                        car.Brain = ai;
                        Step = 1;
                        GM.Hud.Help("Ele te viu! <b>Atire nos pneus... ou no carro todo.</b>", 4f);
                        M.Objective($"O <color={MissionSystem.Red}>Mão-de-Vaca</color> está fugindo! Pare o carro dele.");
                        M.TargetEntity(car.transform, new Color(1f, 0.35f, 0.35f), 3f);
                    }
                    break;
                case 1:
                    if (car == null) { M.Fail("O devedor sumiu."); return; }
                    if (car.Brain is TrafficAI t && !t.Fleeing) t.Panic(Player.Position);
                    if (Gen.FlatDist(car.transform.position, Player.Position) > 380f) { M.Fail("O Mão-de-Vaca fugiu."); return; }
                    if (car.Destroyed || car.Health < car.Spec.Health * 0.3f || !car.HasNpcDriver)
                    {
                        Vector3 p = car.transform.position - car.transform.right * 3f;
                        if (car.HasNpcDriver)
                        {
                            var ped = car.EjectDriver(car.transform.position, true);
                            if (ped != null) { ped.HandsUp(8f); spawned.Add(ped.gameObject); p = ped.transform.position + ped.transform.forward; }
                            GM.Hud.Help("Mão-de-Vaca: \"Tá bom, tá bom! Leva o dinheiro!\"", 3f);
                        }
                        DropBag(p);
                    }
                    break;
                case 2:
                    break;
                case 3:
                    if (Arrived(City.Places[PlaceType.MissionN].Door, 4f)) M.Pass(2500, "Neide: \"Assim que eu gosto.\"");
                    break;
            }
        }

        void DropBag(Vector3 p)
        {
            Step = 2;
            p.y = 0.2f;
            bag = Track(Pickup.Create(p, new Color(0.3f, 1f, 0.4f), 0, "maleta"));
            bag.OnCollect = () =>
            {
                Step = 3;
                M.Objective($"Volte para a <color={MissionSystem.Yellow}>Dona Neide</color>.");
                M.TargetPoint(City.Places[PlaceType.MissionN].Door, new Color(1f, 0.85f, 0.2f));
            };
            M.Objective($"Pegue a <color={MissionSystem.Green}>grana</color>.");
            M.TargetEntity(bag.transform, new Color(0.37f, 0.9f, 0.5f), 1.5f);
        }
    }

    // ---------------------------------------------------------------------
    // 3) Mercadinho — Gordo: assalte a loja, pegue o dinheiro e despiste a polícia.
    // ---------------------------------------------------------------------
    public class MissionStoreRobbery : Mission
    {
        public MissionStoreRobbery() { Title = "Mercadinho"; Giver = "Gordo"; GiverPlace = PlaceType.MissionG; }

        public override void Begin()
        {
            Step = 0;
            var w = Player.Weapons;
            if (!w.Owned[Weapons.Pistol.Slot] || w.Clip[1] + w.Ammo[1] == 0)
            {
                w.Give(Weapons.Pistol, 36);
                GM.Hud.Notify("Gordo te emprestou uma <b>Pistola Hawk .45</b>.");
            }
            GM.Shops.ResetRobbery();
            GM.Shops.OnStoreRobbed += Robbed;
            GM.Shops.OnRobberyCashCollected += Collected;
            M.Objective($"Gordo: \"Vai no <color={MissionSystem.Yellow}>Mercadinho 24h</color> e aponta a arma pro caixa. Sem matar ninguém.\" (botão direito para mirar)");
            M.TargetPoint(City.Places[PlaceType.Store].Door, new Color(1f, 0.85f, 0.2f), false);
        }

        void Robbed()
        {
            Step = 1;
            M.Objective($"Pegue o <color={MissionSystem.Green}>dinheiro</color> no balcão.");
        }

        void Collected()
        {
            Step = 2;
            M.Objective("Despiste a polícia.");
            M.ClearTarget();
        }

        public override void Tick(float dt)
        {
            if (Step == 2 && GM.Wanted.Stars == 0)
            {
                Step = 3;
                M.Objective($"Volte para o <color={MissionSystem.Yellow}>Gordo</color>.");
                M.TargetPoint(City.Places[PlaceType.MissionG].Door, new Color(1f, 0.85f, 0.2f));
            }
            if (Step == 3 && Arrived(City.Places[PlaceType.MissionG].Door, 4f)) M.Pass(1000, "O dinheiro do caixa é todo seu.");
        }

        public override void Cleanup()
        {
            base.Cleanup();
            GM.Shops.OnStoreRobbed -= Robbed;
            GM.Shops.OnRobberyCashCollected -= Collected;
        }
    }

    // ---------------------------------------------------------------------
    // 4) Território — Dona Neide: limpe a praça da gangue rival e traga o pacote.
    // ---------------------------------------------------------------------
    public class MissionTurf : Mission
    {
        readonly List<Ped> gang = new List<Ped>();
        int killed;
        const int Count = 6;

        public MissionTurf() { Title = "Território"; Giver = "Dona Neide"; GiverPlace = PlaceType.MissionN; }

        public override void Begin()
        {
            Step = 0;
            killed = 0;
            gang.Clear();
            var w = Player.Weapons;
            if (!w.Owned[Weapons.Smg.Slot])
            {
                w.Give(Weapons.Smg, 120);
                GM.Hud.Notify("Neide te deu uma <b>Submetralhadora Vespa</b>.");
            }
            M.Objective($"Neide: \"Os <color={MissionSystem.Red}>Vagabundos do Leste</color> tomaram a <color={MissionSystem.Yellow}>Praça do Leste</color>. Resolve isso.\"");
            M.TargetPoint(City.Places[PlaceType.GangPark].Inside, new Color(1f, 0.85f, 0.2f), false);
        }

        public override void Tick(float dt)
        {
            var park = City.Places[PlaceType.GangPark].Inside;
            if (Step == 0 && Arrived(park, 95f))
            {
                Step = 1;
                for (int i = 0; i < Count; i++)
                {
                    Vector2 r = Random.insideUnitCircle * 14f;
                    var g = Track(Ped.Spawn(park + new Vector3(r.x, 0f, r.y), PedRole.Gang));
                    g.Persistent = true;
                    g.State = PedState.Idle;
                    g.OnDeath = _ => { killed++; UpdateText(); };
                    gang.Add(g);
                    M.EnemyBlips.Add(g.transform);
                }
                M.ClearTarget();
                UpdateText();
            }
            if (Step == 1)
            {
                // ficam parados até o jogador chegar perto ou atirar
                foreach (var g in gang)
                    if (g != null && !g.IsDead && g.State == PedState.Idle && (Arrived(g.transform.position, 40f) || Time.time - Player.LastAggro < 1f))
                        g.State = PedState.Fight;
                if (killed >= Count)
                {
                    Step = 2;
                    var pack = Track(Pickup.Create(park + Vector3.up * 0.9f, new Color(1f, 0.7f, 0.2f), 0, "pacote"));
                    pack.OnCollect = () =>
                    {
                        Step = 3;
                        M.Objective($"Leve o pacote para a <color={MissionSystem.Yellow}>Dona Neide</color>.");
                        M.TargetPoint(City.Places[PlaceType.MissionN].Door, new Color(1f, 0.85f, 0.2f));
                    };
                    M.Objective($"Pegue o <color={MissionSystem.Green}>pacote</color> na fonte.");
                    M.TargetEntity(pack.transform, new Color(1f, 0.7f, 0.2f), 1.5f);
                }
            }
            if (Step == 3 && Arrived(City.Places[PlaceType.MissionN].Door, 4f))
            {
                if (GM.Wanted.Stars > 0) GM.Hud.Help("Despiste a polícia antes de encontrar a Neide.", 0.15f);
                else M.Pass(4000, "A praça agora é da Neide.");
            }
        }

        void UpdateText()
        {
            if (Step == 1) M.Objective($"Elimine os <color={MissionSystem.Red}>Vagabundos do Leste</color> ({killed}/{Count}).");
        }
    }

    // ---------------------------------------------------------------------
    // 5) O Grande Golpe — Professor: pare o carro-forte, pegue as maletas e fuja com 4 estrelas.
    // ---------------------------------------------------------------------
    public class MissionHeist : Mission
    {
        VehicleController truck;
        int bags;
        bool alarm;

        public MissionHeist() { Title = "O Grande Golpe"; Giver = "Professor"; GiverPlace = PlaceType.MissionP; }

        public override void Begin()
        {
            Step = 0;
            bags = 0;
            alarm = false;
            var w = Player.Weapons;
            if (!w.Owned[Weapons.Rifle.Slot])
            {
                w.Give(Weapons.Rifle, 150);
                GM.Hud.Notify("O Professor te deu um <b>Fuzil AR-7 Falcão</b>.");
            }
            w.Give(Weapons.Grenade, 3);
            Player.Armor = PlayerController.MaxArmor;
            Vector3 pos; Vector2Int from, to;
            if (!City.RandomLanePoint(Player.Position, 200f, 320f, out pos, out from, out to))
                City.RandomLanePoint(Player.Position, 100f, 500f, out pos, out from, out to);
            Vector3 dir = (CityGenerator.NodePos(to) - CityGenerator.NodePos(from)).normalized;
            truck = VehicleFactory.Create(VehicleKind.Armored, pos, Quaternion.LookRotation(dir));
            truck.PutNpcDriver(Outfit.Cop(), PedRole.Target);
            truck.Brain = new TrafficAI(from, to, 11f);
            truck.Persistent = true;
            truck.ExplodesOnDeath = false;
            spawned.Add(truck.gameObject);
            M.Objective($"Professor: \"Um <color={MissionSystem.Red}>carro-forte</color> está fazendo a ronda. Pare ele. Granadas ajudam.\"");
            M.TargetEntity(truck.transform, new Color(1f, 0.35f, 0.35f), 4f);
        }

        public override void Tick(float dt)
        {
            if (truck == null) { M.Fail("O carro-forte sumiu."); return; }
            switch (Step)
            {
                case 0:
                    if (!alarm && truck.Health < truck.Spec.Health)
                    {
                        alarm = true;
                        GM.Wanted.SetStars(Mathf.Max(GM.Wanted.Stars, 2), truck.transform.position);
                        if (truck.Brain is TrafficAI t) { t.Panic(Player.Position); t.IgnoreLights = true; t.Cruise = 15f; }
                        GM.Hud.Help("O motorista pediu reforço!", 3f);
                    }
                    if (truck.Brain is TrafficAI tt && alarm && !tt.Fleeing) tt.Panic(Player.Position);
                    if (Gen.FlatDist(truck.transform.position, Player.Position) > 480f) { M.Fail("O carro-forte escapou."); return; }
                    if (truck.Destroyed)
                    {
                        Step = 1;
                        GM.Wanted.SetStars(4, truck.transform.position);
                        Fx.Run(Fx.ExplosionVisual(truck.transform.position - truck.transform.forward * 3f, 3f));
                        Sfx.PlayAt(Sfx.Explosion, truck.transform.position, 0.8f);
                        for (int i = 0; i < 3; i++)
                        {
                            Vector3 p = truck.transform.position - truck.transform.forward * (4.5f + i * 1.2f) + truck.transform.right * (i - 1) * 1.2f;
                            p.y = 0.15f;
                            var b = Track(Pickup.Create(p, new Color(0.3f, 1f, 0.4f), 0, "maleta"));
                            b.OnCollect = () => { bags++; UpdateBags(); };
                        }
                        M.ClearTarget();
                        UpdateBags();
                        GM.Hud.Help("As portas abriram! <b>Pegue as maletas e suma.</b>", 4f);
                    }
                    break;
                case 2:
                    if (GM.Wanted.Stars == 0)
                    {
                        Step = 3;
                        M.Objective($"Leve a grana para a <color={MissionSystem.Yellow}>sua casa</color>.");
                        M.TargetPoint(City.Places[PlaceType.Safehouse].Door, new Color(1f, 0.85f, 0.2f));
                    }
                    break;
                case 3:
                    if (Arrived(City.Places[PlaceType.Safehouse].Door, 4f))
                        M.Pass(25000, "Você é o dono de Dev City.\n<size=22>Obrigado por jogar! A cidade continua aberta.</size>");
                    break;
            }
        }

        void UpdateBags()
        {
            if (bags >= 3)
            {
                Step = 2;
                M.Objective("Despiste a polícia (4 estrelas!). Dica: o <b>Pinta & Foge</b> ajuda.");
            }
            else M.Objective($"Pegue as <color={MissionSystem.Green}>maletas</color> ({bags}/3).");
        }
    }

    // ---------------------------------------------------------------------
    // Racha da Orla: corrida contra o relógio por checkpoints (repetível).
    // ---------------------------------------------------------------------
    public class RaceMission : Mission
    {
        static readonly Vector2Int[] route =
        {
            new Vector2Int(0, 9), new Vector2Int(4, 9), new Vector2Int(4, 6), new Vector2Int(8, 6),
            new Vector2Int(8, 2), new Vector2Int(3, 2), new Vector2Int(3, 0), new Vector2Int(0, 0), new Vector2Int(0, 4)
        };
        const float Limit = 105f;
        int cp;
        float time, countdown;
        Marker marker;

        public RaceMission() { Title = "Racha da Orla"; Giver = "Contra o relógio"; GiverPlace = PlaceType.Race; }

        public override void Begin()
        {
            cp = 0;
            time = 0f;
            countdown = 3.5f;
            Step = 0;
            NextCheckpoint();
        }

        void NextCheckpoint()
        {
            if (marker != null) Object.Destroy(marker.gameObject);
            Vector3 p = CityGenerator.NodePos(route[cp]);
            marker = Marker.Corona(p, new Color(1f, 0.82f, 0.2f), 5f);
            spawned.Add(marker.gameObject);
            M.TargetPoint(p, new Color(1f, 0.85f, 0.2f), false);
        }

        public override void Tick(float dt)
        {
            if (Player.Vehicle == null && Step == 1) { M.Fail("Você saiu do carro."); return; }
            if (Step == 0)
            {
                countdown -= dt;
                if (Player.Vehicle != null) Player.Vehicle.SetInput(0f, 0f, true);
                M.Objective($"<size=48><b>{Mathf.CeilToInt(Mathf.Max(countdown - 0.5f, 0f))}</b></size>");
                if (countdown <= 0.5f) { Step = 1; GM.Hud.Help("<b>VAI!</b>", 1.2f); Sfx.Play2D(Sfx.Pickup); }
                return;
            }
            time += dt;
            M.Objective($"Checkpoint <b>{cp + 1}/{route.Length}</b>   Tempo <b>{time:0.0}s</b>  (limite {Limit:0}s{(GM.Missions.RaceBest > 0 ? $", recorde {GM.Missions.RaceBest:0.0}s" : "")})");
            if (time > Limit) { M.Fail("O tempo acabou."); return; }
            if (Gen.FlatDist(Player.Position, CityGenerator.NodePos(route[cp])) < 9f)
            {
                Sfx.Play2D(Sfx.Pickup, 0.7f);
                cp++;
                if (cp >= route.Length)
                {
                    bool record = GM.Missions.RaceBest <= 0f || time < GM.Missions.RaceBest;
                    if (record) GM.Missions.RaceBest = time;
                    M.Pass(record ? 1500 : 800, $"Tempo: {time:0.0}s" + (record ? "  — <b>NOVO RECORDE!</b>" : ""));
                    return;
                }
                NextCheckpoint();
            }
        }
    }
}

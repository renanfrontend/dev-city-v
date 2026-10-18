using System.Collections;
using UnityEngine;

namespace DevCity
{
    public enum GameState { Title, Playing, Paused, Map, Shop, Wasted, Busted, Cutscene }

    /// <summary>
    /// Ponto de entrada do jogo. Não precisa de nada na cena: ele se cria sozinho
    /// (RuntimeInitializeOnLoadMethod) e monta a cidade, o jogador e todos os sistemas.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager I { get; private set; }

        public GameState State = GameState.Title;
        public int Money = 750;

        public CityGenerator City { get; private set; }
        public DayNightCycle DayNight { get; private set; }
        public PlayerController Player { get; private set; }
        public CameraRig Cam { get; private set; }
        public HUD Hud { get; private set; }
        public MinimapSystem Minimap { get; private set; }
        public WantedSystem Wanted { get; private set; }
        public PoliceSystem Police { get; private set; }
        public TrafficSystem Traffic { get; private set; }
        public PedestrianSystem Peds { get; private set; }
        public ShopSystem Shops { get; private set; }
        public MissionSystem Missions { get; private set; }
        public Gps Gps { get; private set; }
        public CharacterSwitch Switch { get; private set; }

        public bool Playing => State == GameState.Playing;
        public Vector3 PlayerPos => Player != null ? Player.Position : Vector3.zero;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (I == null) new GameObject("DevCityV").AddComponent<GameManager>();
        }

        T Make<T>(string name) where T : Component
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            return go.AddComponent<T>();
        }

        void Awake()
        {
            I = this;
            Application.targetFrameRate = 144;
            QualitySettings.shadowDistance = 150f;
            QualitySettings.pixelLightCount = 6;
            Physics.defaultSolverIterations = 10;
            // placa de vídeo fraca (ex.: Intel HD 4000): menos sombra, sem antisserrilhado, menos luzes
            if (SystemInfo.graphicsMemorySize <= 1024)
            {
                QualitySettings.shadowDistance = 70f;
                QualitySettings.antiAliasing = 0;
                QualitySettings.pixelLightCount = 2;
                QualitySettings.shadowResolution = ShadowResolution.Medium;
                QualitySettings.shadowCascades = 1;
                QualitySettings.skinWeights = SkinWeights.TwoBones; // personagens: cada vértice segue só 2 ossos
                Debug.Log("[Dev City] Qualidade reduzida para GPU fraca: " + SystemInfo.graphicsDeviceName);
            }

            // a cena pode ter câmera/luz padrão: o jogo cria as suas
            foreach (var c in FindObjectsByType<Camera>()) Destroy(c.gameObject);
            foreach (var l in FindObjectsByType<Light>()) Destroy(l.gameObject);

            var sw = System.Diagnostics.Stopwatch.StartNew();
            void Step(string what) { Debug.Log($"[Dev City] {what}: {sw.ElapsedMilliseconds} ms"); sw.Restart(); }
            Mats.Init();
            Sfx.Init();
            Fx.Init();

            Step("materiais, sons e efeitos");
            City = Make<CityGenerator>("Cidade");
            City.Build();
            Step("cidade");

            DayNight = Make<DayNightCycle>("Ceu");
            Gps = Make<Gps>("GPS");
            Wanted = Make<WantedSystem>("Procurado");
            Police = Make<PoliceSystem>("Policia");
            Traffic = Make<TrafficSystem>("Transito");
            Peds = Make<PedestrianSystem>("Pedestres");
            Shops = Make<ShopSystem>("Lojas");
            Missions = Make<MissionSystem>("Missoes");

            Player = PlayerController.Create(City.SpawnPoint(PlaceType.Safehouse));
            Cam = CameraRig.Create(Player);

            Hud = Make<HUD>("HUD");
            Hud.Build();
            Minimap = Hud.gameObject.AddComponent<MinimapSystem>();
            Minimap.Build(Hud);

            DayNight.Init();
            Shops.Init();
            Missions.Init();
            Traffic.Init();
            Peds.Init();

            // Trevor e Jacob (precisa do jogador e do HUD prontos)
            Switch = Make<CharacterSwitch>("Protagonistas");
            Switch.Init();

            SaveSystem.Load();
            Step("sistemas, jogador e interface");
            ShowTitle();
            AutoTest.StartIfRequested(this);
        }

        // ---------- estados ----------

        public void ShowTitle()
        {
            State = GameState.Title;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            Hud.ShowTitle(true);
            Cam.TitleMode = true;
        }

        public void StartGame()
        {
            Hud.ShowTitle(false);
            Cam.TitleMode = false;
            SetPlaying();
            Hud.Notify("Bem-vindo a <b>Dev City</b>. Vá até um <color=#ffd84a>marcador</color> no mapa para começar uma missão.");
            Hud.Help("Aperte <b>M</b> para ver o mapa e <b>ESC</b> para pausar.", 6f);
        }

        public void SetPlaying()
        {
            State = GameState.Playing;
            Time.timeScale = 1f;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        public void Pause(GameState why)
        {
            State = why;
            Time.timeScale = 0f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        void Update()
        {
            if (State == GameState.Title)
            {
                if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetMouseButtonDown(0))
                    StartGame();
                return;
            }

            if (Input.GetKeyDown(KeyCode.Escape) && !AutoTest.Running)
            {
                if (State == GameState.Playing) { Pause(GameState.Paused); Hud.ShowPause(true); }
                else if (State == GameState.Paused) { Hud.ShowPause(false); SetPlaying(); }
                else if (State == GameState.Map) { Minimap.ShowBigMap(false); SetPlaying(); }
            }
            if (Input.GetKeyDown(KeyCode.M) && !AutoTest.Running)
            {
                if (State == GameState.Playing) { Pause(GameState.Map); Minimap.ShowBigMap(true); }
                else if (State == GameState.Map) { Minimap.ShowBigMap(false); SetPlaying(); }
            }
        }

        // ---------- dinheiro ----------

        public void AddMoney(int amount)
        {
            Money += amount;
            if (Hud != null) Hud.MoneyPopup(amount);
        }

        public bool Spend(int amount)
        {
            if (Money < amount) return false;
            Money -= amount;
            if (Hud != null) Hud.MoneyPopup(-amount);
            return true;
        }

        // ---------- WASTED / BUSTED ----------

        public void Wasted(string reason)
        {
            if (State == GameState.Wasted || State == GameState.Busted) return;
            StartCoroutine(DeathSequence(false, reason));
        }

        public void Busted()
        {
            if (State == GameState.Wasted || State == GameState.Busted) return;
            StartCoroutine(DeathSequence(true, "Você foi preso."));
        }

        IEnumerator DeathSequence(bool busted, string reason)
        {
            State = busted ? GameState.Busted : GameState.Wasted;
            Missions.FailActive(busted ? "Você foi preso." : "Você morreu.");
            Time.timeScale = 0.3f;
            Sfx.Play2D(Sfx.Wasted, 0.9f);
            Hud.ShowDeath(busted ? "BUSTED" : "WASTED", reason);
            yield return new WaitForSecondsRealtime(3.2f);
            yield return Hud.FadeRoutine(1f, 0.6f);

            Time.timeScale = 1f;
            Wanted.Clear();
            Police.DespawnAll();
            Hud.HideDeath();

            if (busted)
            {
                int fine = Mathf.Min(Mathf.RoundToInt(Money * 0.1f), 2000);
                Money -= fine;
                Player.Weapons.LoseAll();
                Player.Armor = 0f;
                Player.Respawn(City.SpawnPoint(PlaceType.Police));
                Hud.Notify($"Fiança e advogado: <color=#ff6b6b>-${fine:N0}</color>. A polícia ficou com as suas armas.");
            }
            else
            {
                int bill = Mathf.Min(500, Money);
                Money -= bill;
                Player.Respawn(City.SpawnPoint(PlaceType.Hospital));
                Hud.Notify($"Conta do hospital: <color=#ff6b6b>-${bill:N0}</color>.");
            }
            DayNight.Advance(busted ? 12f : 8f);
            yield return new WaitForSecondsRealtime(0.4f);
            SetPlaying();
            yield return Hud.FadeRoutine(0f, 0.8f);
            SaveSystem.Save();
        }
    }
}

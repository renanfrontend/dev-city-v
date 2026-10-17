using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;

namespace DevCity
{
    /// <summary>
    /// Teste automático: rode o jogo com o argumento <c>-autotest</c>.
    /// Ele joga sozinho (anda, dirige, chama a polícia), mede o FPS, conta erros,
    /// tira prints em Build/autotest_*.png e fecha o jogo. O resumo vai para o log.
    /// </summary>
    public class AutoTest : MonoBehaviour
    {
        public static bool Running { get; private set; }
        int errors, exceptions;
        string firstError;
        string outDir;

        public static void StartIfRequested(GameManager gm)
        {
            if (!System.Environment.GetCommandLineArgs().Any(a => a == "-autotest")) return;
            gm.gameObject.AddComponent<AutoTest>();
        }

        void Awake()
        {
            Running = true;
            Application.runInBackground = true; // o teste continua mesmo se a janela perder o foco
            outDir = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            Application.logMessageReceived += OnLog;
        }

        void OnDestroy() { Application.logMessageReceived -= OnLog; }

        void OnLog(string msg, string stack, LogType type)
        {
            if (type == LogType.Exception) { exceptions++; if (firstError == null) firstError = msg + "\n" + stack; }
            else if (type == LogType.Error) { errors++; if (firstError == null) firstError = msg; }
        }

        static void Log(string m) => Debug.Log("[AutoTest] " + m);

        IEnumerator MeasureFps(string label, float seconds)
        {
            int frames = 0;
            float t = 0f, worst = 0f;
            while (t < seconds)
            {
                t += Time.unscaledDeltaTime;
                worst = Mathf.Max(worst, Time.unscaledDeltaTime);
                frames++;
                yield return null;
            }
            Log($"{label}: {frames / seconds:0.0} FPS médio, pior quadro {worst * 1000f:0} ms");
        }

        IEnumerator Shot(string name)
        {
            yield return new WaitForEndOfFrame();
            string path = Path.Combine(outDir, "autotest_" + name + ".png");
            ScreenCapture.CaptureScreenshot(path);
            Log("print: " + path);
            yield return new WaitForEndOfFrame();
            yield return null;
        }

        int repairs;

        /// <summary>
        /// Pilota o carro por <paramref name="seconds"/>: acelera até <paramref name="targetKmh"/> e freia/desvia
        /// quando há obstáculo à frente. Sem isso o carro bate num prédio a ~150 km/h, pega fogo e explode com o
        /// jogador dentro, o que zera o nível de procurado e invalida a etapa da polícia. Se mesmo assim o carro
        /// quase morrer, ele é consertado e o fato vai para o log.
        /// </summary>
        IEnumerator Drive(VehicleController car, float seconds, float targetKmh, float steerBias)
        {
            float t = 0f;
            while (t < seconds)
            {
                t += Time.unscaledDeltaTime;
                float speed = car.Speed;
                Vector3 o = car.transform.position + Vector3.up * 0.8f;
                Vector3 fwd = car.transform.forward;
                float look = Mathf.Max(14f, speed * 1.6f);
                bool blocked = Physics.Raycast(o, fwd, look, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
                float steer = steerBias, throttle = speed * 3.6f < targetKmh ? 1f : 0f;
                if (blocked)
                {
                    bool left = Physics.Raycast(o, Quaternion.Euler(0f, -30f, 0f) * fwd, look, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
                    bool right = Physics.Raycast(o, Quaternion.Euler(0f, 30f, 0f) * fwd, look, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
                    steer = right && !left ? -1f : 1f;
                    throttle = speed > 6f ? -1f : 0f;
                }
                PlayerController.ScriptedDrive = new Vector3(throttle, steer, 0f);
                yield return null;
            }
        }

        /// <summary>Conserta o carro quando está quase destruído, para ele não explodir com o jogador dentro.</summary>
        IEnumerator KeepCarAlive(VehicleController car)
        {
            while (true)
            {
                if (!car.Destroyed && (car.Health < car.Spec.Health * 0.4f || car.Burning))
                {
                    car.Repair();
                    repairs++;
                    Log($"carro consertado ({repairs}x) para o teste não morrer numa batida");
                }
                yield return null;
            }
        }

        IEnumerator Start()
        {
            var gm = GameManager.I;
            Log($"início | GPU: {SystemInfo.graphicsDeviceName} ({SystemInfo.graphicsMemorySize} MB) | tela {Screen.width}x{Screen.height}");
            yield return new WaitForSecondsRealtime(3f);
            yield return MeasureFps("título", 4f);
            yield return Shot("1_titulo");

            gm.StartGame();
            yield return new WaitForSecondsRealtime(2f);
            Log($"jogando | pedestres {Ped.All.Count} | veículos {VehicleController.All.Count}");

            // anda para frente
            PlayerController.ScriptedMove = new Vector2(0f, 1f);
            yield return MeasureFps("a pé (andando)", 4f);
            PlayerController.ScriptedMove = null;
            Log($"posição a pé: {gm.Player.Position} | bairro: {gm.City.AreaName(gm.Player.Position)}");
            yield return Shot("2_a_pe");

            // carro ao lado do jogador
            var p = gm.Player;
            Vector3 spawn = p.transform.position + p.transform.right * 3.5f;
            if (!gm.City.RandomLanePoint(p.Position, 4f, 40f, out var lane, out var from, out var to)) lane = spawn;
            var car = VehicleFactory.Create(VehicleKind.Sports, lane, Quaternion.LookRotation((CityGenerator.NodePos(to) - CityGenerator.NodePos(from)).normalized));
            p.Teleport(car.DoorPoint(true));
            yield return new WaitForSecondsRealtime(0.5f);
            p.ForceEnter(car);
            yield return new WaitForSecondsRealtime(1.2f);
            Log($"no carro: {(p.Vehicle == car ? "sim" : "NÃO")}");
            Log($"carro {car.DisplayName}: {(car.transform.Find("Carroceria/" + VehicleFactory.ModelName(car.Kind)) != null ? "modelo 3D" : "caixas")} | {car.Spec.Length:0.0} x {car.Spec.Width:0.0} m | raio da roda {car.Spec.WheelRadius:0.00} m");

            // acelera reto e depois faz curva (com um piloto que freia/desvia de obstáculos à frente)
            StartCoroutine(KeepCarAlive(car));
            StartCoroutine(Drive(car, 5f, 150f, 0f));
            yield return MeasureFps("dirigindo", 5f);
            Log($"velocidade após 5 s: {car.Speed * 3.6f:0} km/h | capotou: {(car.transform.up.y < 0.3f ? "sim" : "não")} | vida do carro {car.Health:0}");
            yield return Shot("3_dirigindo");
            yield return Drive(car, 2.5f, 60f, 1f);
            PlayerController.ScriptedDrive = new Vector3(-1f, 0f, 0f);
            yield return new WaitForSecondsRealtime(2f);
            PlayerController.ScriptedDrive = null;
            Log($"após curva e freio: {car.Speed * 3.6f:0} km/h | inclinação up.y={car.transform.up.y:0.00}");

            // polícia
            gm.Wanted.SetStars(3, p.Position);
            yield return new WaitForSecondsRealtime(12f);
            int heli = gm.Police.Helis.Count();
            int units = gm.Police.Units.Count();
            Log($"3 estrelas após 12 s: viaturas {units}, helicópteros {heli}, vista={gm.Wanted.Seen}");
            yield return MeasureFps("perseguição (3★)", 5f);
            yield return Shot("4_policia");

            // noite
            gm.DayNight.Hour = 22f;
            yield return new WaitForSecondsRealtime(1f);
            yield return MeasureFps("noite", 3f);
            yield return Shot("5_noite");

            // mapa grande
            gm.Pause(GameState.Map);
            gm.Minimap.ShowBigMap(true);
            yield return new WaitForSecondsRealtime(1f);
            yield return Shot("6_mapa");
            gm.Minimap.ShowBigMap(false);
            gm.SetPlaying();

            // troca de protagonista (Trevor -> Jacob) com a câmera do céu
            gm.Wanted.Clear();
            gm.Police.DespawnAll();
            gm.DayNight.Hour = 12f;
            string before = gm.Switch.Current.Name;
            gm.Switch.SwitchTo(1);
            yield return new WaitForSecondsRealtime(1.2f);
            yield return Shot("7_ceu");
            while (gm.Switch.Switching) yield return null;
            yield return new WaitForSecondsRealtime(1.5f);
            Log($"troca: {before} -> {gm.Switch.Current.Name} | modelo Mixamo: {(gm.Player.Model.Rigged ? "sim" : "não (bonecos de blocos)")} | bairro: {gm.City.AreaName(gm.Player.Position)} | estado: {gm.State}");
            yield return Shot("8_jacob");

            if (gm.Player.IsDead) Log("AVISO: o jogador morreu durante o teste");
            Log($"FIM | erros {errors} | exceções {exceptions}" + (firstError != null ? "\nPrimeiro erro: " + firstError : ""));
            yield return new WaitForSecondsRealtime(1f);
            Application.Quit();
        }
    }
}

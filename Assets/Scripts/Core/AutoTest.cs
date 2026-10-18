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

        /// <summary>Fotos de lado da porta do motorista (a câmera normal fica atrás do carro e esconde o personagem).</summary>
        IEnumerator SideShots(VehicleController car, string name)
        {
            var cam = GameManager.I.Cam;
            cam.Override = true;
            for (int i = 0; i < 3; i++)
            {
                yield return new WaitForSecondsRealtime(i == 0 ? 0.5f : 0.9f);
                cam.transform.position = car.transform.TransformPoint(new Vector3(-6f, 1.8f, 1.5f));
                cam.transform.rotation = Quaternion.LookRotation(car.transform.TransformPoint(new Vector3(-0.8f, 0.8f, 0f)) - cam.transform.position);
                yield return Shot($"{name}_{i + 1}");
            }
            cam.Override = false;
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

            // -fpsonly: só mede o desempenho (para comparar versões) e sai
            if (System.Environment.GetCommandLineArgs().Contains("-fpsonly"))
            {
                yield return MeasureFps("parado na rua", 6f);
                Log($"FIM | erros {errors} | exceções {exceptions}");
                yield return new WaitForSecondsRealtime(0.5f);
                Application.Quit();
                yield break;
            }

            // cenário: fotos de 4 bairros (centro, comercial, casas e porto)
            {
                float hourBefore = gm.DayNight.Hour;
                gm.DayNight.Hour = 12f; // meio-dia, para enxergar bem
                var cam = gm.Cam;
                cam.Override = true;
                var views = new (string name, Vector2 block, float height)[] { ("centro", new Vector2(5f, 5f), 70f), ("comercial", new Vector2(3f, 6f), 35f), ("casas", new Vector2(1f, 8f), 22f), ("porto", new Vector2(8f, 2f), 35f) };
                foreach (var v in views)
                {
                    Vector3 c = new Vector3(v.block.x * CityGenerator.P + CityGenerator.ROAD + 26f, 0f, v.block.y * CityGenerator.P + CityGenerator.ROAD + 26f);
                    cam.transform.position = c + new Vector3(-55f, v.height, -55f);
                    cam.transform.rotation = Quaternion.LookRotation(c + Vector3.up * 6f - cam.transform.position);
                    yield return new WaitForSecondsRealtime(0.4f);
                    Log($"cenário {v.name}: {gm.City.AreaName(c)}");
                    yield return Shot("2c_cidade_" + v.name);
                }
                // de perto: uma casa e um prédio comercial vistos da rua (para conferir se a frente olha para a rua)
                foreach (var (nm, blk) in new[] { ("casa_perto", new Vector2Int(1, 8)), ("predio_perto", new Vector2Int(3, 6)) })
                {
                    float x0 = blk.x * CityGenerator.P + CityGenerator.ROAD, z0 = blk.y * CityGenerator.P + CityGenerator.ROAD;
                    // na rua ao sul do quarteirão, olhando para o norte (para as fachadas viradas para essa rua)
                    cam.transform.position = new Vector3(x0 + 13f, 2.2f, z0 - CityGenerator.ROAD / 2f);
                    cam.transform.rotation = Quaternion.LookRotation(new Vector3(x0 + 13f, 4f, z0 + 12f) - cam.transform.position);
                    yield return new WaitForSecondsRealtime(0.3f);
                    yield return Shot("2d_" + nm);
                }
                Log($"modelos 3D na cidade: {GameObject.Find("Modelos3D")?.transform.childCount ?? 0}");
                yield return MeasureFps("cidade (vista do centro)", 3f);
                cam.Override = false;
                gm.DayNight.Hour = hourBefore;
            }

            // NPCs 3D: quantos pedestres usam modelo 3D, e uma fileira deles para a foto
            {
                int com3d = Ped.All.Count(pd => pd != null && pd.Model.Rigged);
                Log($"NPCs com modelo 3D: {com3d} de {Ped.All.Count}");
                var pl = gm.Player;
                var fila = new System.Collections.Generic.List<Ped>();
                var roles = new[] { PedRole.Civilian, PedRole.Civilian, PedRole.Civilian, PedRole.Gang, PedRole.Cop, PedRole.Civilian };
                for (int k = 0; k < roles.Length; k++)
                {
                    var pd = Ped.Spawn(pl.transform.position + pl.transform.forward * 5f + pl.transform.right * ((k - 2.5f) * 1.2f), roles[k]);
                    pd.transform.rotation = Quaternion.LookRotation(-pl.transform.forward);
                    fila.Add(pd);
                }
                var cam = gm.Cam;
                cam.Override = true;
                float hourBefore = gm.DayNight.Hour;
                gm.DayNight.Hour = 12f;
                cam.transform.position = pl.transform.position + pl.transform.forward * 0.5f + Vector3.up * 1.7f;
                cam.transform.rotation = Quaternion.LookRotation(pl.transform.position + pl.transform.forward * 5f + Vector3.up * 1f - cam.transform.position);
                yield return new WaitForSecondsRealtime(0.5f);
                yield return Shot("2e_npcs");
                fila[0].Kill(false);
                yield return new WaitForSecondsRealtime(1.5f);
                Log($"NPC morto: animação de morte {(fila[0].Model.DeathAnimated ? "sim" : "não (cai de lado)")}");
                yield return Shot("2f_npc_morte");
                gm.DayNight.Hour = hourBefore;
                cam.Override = false;
                foreach (var pd in fila) if (pd != null) Destroy(pd.gameObject);
            }

            // armas 3D: cada arma na mão, mirando, foto de perto (câmera de lado)
            {
                var pl = gm.Player;
                foreach (var w in Weapons.All) if (!w.Melee && !w.Thrown) pl.Weapons.Give(w, 30);
                PlayerWeapons.ScriptedAim = true;
                var cam = gm.Cam;
                cam.Override = true;
                foreach (int slot in new[] { 1, 2, 3, 4, 5 })
                {
                    pl.Weapons.Select(slot);
                    yield return new WaitForSecondsRealtime(0.6f);
                    var tr = pl.transform;
                    cam.transform.position = tr.position + tr.right * 1.6f + tr.forward * 0.9f + Vector3.up * 1.55f;
                    cam.transform.rotation = Quaternion.LookRotation(tr.position + Vector3.up * 1.35f + tr.forward * 0.35f - cam.transform.position);
                    var cano = pl.Model.Muzzle;
                    Log($"arma {Weapons.All[slot].Name}: modelo 3D {(cano != null && cano.parent != null && cano.parent.name.StartsWith("arma-") ? "sim" : "não")} | cano a {(cano != null ? Vector3.Distance(cano.position, tr.position + Vector3.up * 1.4f) : 0f):0.00} m do peito");
                    yield return Shot("2b_arma_" + slot);
                }
                cam.Override = false;
                PlayerWeapons.ScriptedAim = false;
                pl.Weapons.Select(0);
            }

            // carro ao lado do jogador
            var p = gm.Player;
            Vector3 spawn = p.transform.position + p.transform.right * 3.5f;
            if (!gm.City.RandomLanePoint(p.Position, 4f, 40f, out var lane, out var from, out var to)) lane = spawn;
            var car = VehicleFactory.Create(VehicleKind.Sports, lane, Quaternion.LookRotation((CityGenerator.NodePos(to) - CityGenerator.NodePos(from)).normalized));
            p.Teleport(car.DoorPoint(true));
            yield return new WaitForSecondsRealtime(0.5f);
            p.ForceEnter(car);
            yield return SideShots(car, "3a_entrando");
            for (float w = 0f; w < 6f && p.Vehicle != car; w += 0.2f) yield return new WaitForSecondsRealtime(0.2f);
            Log($"no carro: {(p.Vehicle == car ? "sim" : "NÃO")} | animação de entrar: {(CarActions.Enter != null && p.Model.Rigged ? "sim" : "não")}");

            // sai do carro (animação "Exiting Car") e entra de novo
            Log($"carro parado? {car.Speed * 3.6f:0.0} km/h | pos jogador {p.transform.position}");
            p.ExitVehicle(false, true);
            Log($"saindo com animação: {(p.Busy ? "sim" : "não")}");
            yield return SideShots(car, "3b_saindo");
            for (float w = 0f; w < 6f && p.Busy; w += 0.2f) yield return new WaitForSecondsRealtime(0.2f);
            Log($"saiu do carro: {(p.Vehicle == null ? "sim" : "NÃO")} | distância do carro {Vector3.Distance(p.transform.position, car.transform.position):0.0} m");
            yield return Shot("3c_fora");
            p.ForceEnter(car);
            for (float w = 0f; w < 7f && p.Vehicle != car; w += 0.2f) yield return new WaitForSecondsRealtime(0.2f);
            Log($"entrou de novo: {(p.Vehicle == car ? "sim" : "NÃO")}");
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

            // tutorial dos veículos: cores variadas, ambulância e destroços
            var pj = gm.Player;
            var cores = new System.Collections.Generic.List<string>();
            VehicleController alvo = null;
            for (int i = 0; i < 4; i++)
            {
                Vector3 pos = pj.Position + pj.transform.forward * (8f + i * 6f) + pj.transform.right * 4f;
                var c = VehicleFactory.Create(i % 2 == 0 ? VehicleKind.Sedan : VehicleKind.SUV, pos, pj.transform.rotation);
                c.SetInput(0f, 0f, true);
                var r = c.GetComponentsInChildren<Renderer>().FirstOrDefault(x => x.sharedMaterial != null && x.sharedMaterial.name.StartsWith("Lataria_"));
                cores.Add(r != null ? r.sharedMaterial.name.Replace("Lataria_", "cor ") : "original");
                if (i == 0) alvo = c;
            }
            Log("cores dos carros civis: " + string.Join(", ", cores));
            var amb = VehicleFactory.Create(VehicleKind.Ambulance, pj.Position - pj.transform.forward * 8f + pj.transform.right * 4f, pj.transform.rotation);
            amb.SetInput(0f, 0f, true);
            Log($"ambulância: {(amb.transform.Find("Carroceria/" + VehicleFactory.ModelName(VehicleKind.Ambulance)) != null ? "modelo 3D" : "caixas")} | {amb.DisplayName}");
            yield return new WaitForSecondsRealtime(1.5f);
            yield return Shot("9_cores");
            pj.Armor = PlayerController.MaxArmor; pj.Health = PlayerController.MaxHealth;
            alvo.Explode();
            yield return new WaitForSecondsRealtime(0.6f);
            int destrocos = FindObjectsByType<Rigidbody>().Count(rb => rb.name == "Destroco");
            Log($"explosão: {destrocos} destroços voando");
            yield return Shot("10_destrocos");
            yield return new WaitForSecondsRealtime(2f);

            // desafio 4: moto (2 rodas + estabilizador)
            if (!gm.City.RandomLanePoint(pj.Position, 10f, 80f, out var mLane, out var mFrom, out var mTo)) mLane = pj.Position + pj.transform.right * 6f;
            Quaternion mRot = Quaternion.LookRotation((CityGenerator.NodePos(mTo) - CityGenerator.NodePos(mFrom)).normalized);

            // vitrine: um de cada modelo lado a lado, fotografados de lado
            var vitrine = new System.Collections.Generic.List<VehicleController>();
            Vector3 vRight = mRot * Vector3.right;
            for (int k = 0; k < MotoBuilder.ModelCount; k++)
            {
                MotoBuilder.NextModel = k;
                var vm = VehicleFactory.Create(VehicleKind.Moto, mLane + vRight * (12f + k * 1.6f) + Vector3.up * 0.3f, mRot);
                vm.SetInput(0f, 0f, true);
                vitrine.Add(vm);
            }
            yield return new WaitForSecondsRealtime(1.5f);
            foreach (var vm in vitrine)
                Log($"moto {vm.DisplayName}: roda r={vm.Spec.WheelRadius:0.00} m | {vm.Spec.Length:0.0} m | em pé? up.y={vm.transform.up.y:0.00} | modelo 3D: {(vm.transform.Find("Carroceria/" ) != null && vm.transform.Find("Carroceria").childCount > 2 ? "sim" : "caixas")}");
            if (vitrine.Count > 0)
            {
                var cam = gm.Cam;
                cam.Override = true;
                Vector3 mid = (vitrine[0].transform.position + vitrine[vitrine.Count - 1].transform.position) / 2f;
                cam.transform.position = mid + (mRot * Vector3.forward) * 7.5f + Vector3.up * 1.6f;
                cam.transform.rotation = Quaternion.LookRotation(mid + Vector3.up * 0.6f - cam.transform.position);
                yield return Shot("11a_vitrine_frente");
                cam.transform.position = vitrine[0].transform.position - vRight * 4.5f + Vector3.up * 1.4f + (mRot * Vector3.forward) * 1f;
                cam.transform.rotation = Quaternion.LookRotation(vitrine[0].transform.position + Vector3.up * 0.6f - cam.transform.position);
                yield return Shot("11b_vitrine_lado");
                cam.Override = false;
            }

            MotoBuilder.NextModel = 2; // a esportiva (Raio 600)
            var moto = VehicleFactory.Create(VehicleKind.Moto, mLane, mRot);
            pj.Teleport(moto.DoorPoint(true));
            yield return new WaitForSecondsRealtime(1f);
            Log($"moto parada: em pé? up.y={moto.transform.up.y:0.00} | altura {moto.transform.position.y - mLane.y:0.00} m");
            pj.ForceEnter(moto);
            for (float w = 0f; w < 3f && pj.Vehicle != moto; w += 0.2f) yield return new WaitForSecondsRealtime(0.2f);
            Log($"na moto: {(pj.Vehicle == moto ? "sim" : "NÃO")}");
            StartCoroutine(KeepCarAlive(moto));
            yield return Drive(moto, 4f, 110f, 0f);
            Log($"moto após 4 s: {moto.Speed * 3.6f:0} km/h | up.y={moto.transform.up.y:0.00}");
            yield return Shot("11_moto");
            StartCoroutine(Drive(moto, 3f, 60f, 0.7f));
            float maxLean = 0f, leanKmh = 0f;
            for (float w = 0f; w < 3f; w += Time.unscaledDeltaTime)
            {
                float lean = Vector3.SignedAngle(Vector3.ProjectOnPlane(Vector3.up, moto.transform.forward), moto.transform.up, moto.transform.forward);
                if (Mathf.Abs(lean) > Mathf.Abs(maxLean)) { maxLean = lean; leanKmh = moto.Speed * 3.6f; }
                if (w > 1.2f && w - Time.unscaledDeltaTime <= 1.2f) yield return Shot("12_moto_curva");
                yield return null;
            }
            Log($"moto na curva: inclinação máxima {maxLean:0}° a {leanKmh:0} km/h (negativo = para a direita)");
            PlayerController.ScriptedDrive = new Vector3(-1f, 0f, 0f);
            yield return new WaitForSecondsRealtime(2.5f);
            PlayerController.ScriptedDrive = null;
            Log($"moto depois de frear: {moto.Speed * 3.6f:0} km/h | ainda montado: {(pj.Vehicle == moto ? "sim" : "não (caiu)")} | up.y={moto.transform.up.y:0.00}");

            if (gm.Player.IsDead) Log("AVISO: o jogador morreu durante o teste");
            Log($"FIM | erros {errors} | exceções {exceptions}" + (firstError != null ? "\nPrimeiro erro: " + firstError : ""));
            yield return new WaitForSecondsRealtime(1f);
            Application.Quit();
        }
    }
}

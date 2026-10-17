using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DevCity
{
    /// <summary>
    /// Monta a moto (desafio 4 do TUTORIAL-VEICULOS.md) com peças simples (caixas e cilindros).
    ///
    /// A física é a mesma dos carros (VehicleController), com 3 diferenças:
    /// - só 2 rodas, as duas no meio (x = 0): a 0 é a da frente e a 2 é a de trás;
    /// - a suspensão é 2x mais dura, porque 2 rodas seguram todo o peso;
    /// - um "estabilizador" (VehicleController.Balance) mantém a moto em pé e a deita nas curvas.
    /// </summary>
    public static class MotoBuilder
    {
        /// <summary>Qual modelo usar na próxima moto (-1 = sorteia). O AutoTest usa para testar todas.</summary>
        public static int NextModel = -1;

        static GameObject[] models;
        public static int ModelCount { get { Load(); return models.Length; } }
        static void Load() { if (models == null) models = Resources.LoadAll<GameObject>("Veiculos/Motos").OrderBy(m => m.name).ToArray(); }

        // nome e "personalidade" de cada modelo (arquivo em Resources/Veiculos/Motos)
        static readonly Dictionary<string, (string name, float mass, float engine, float maxSpeed, float grip)> Tuning = new Dictionary<string, (string, float, float, float, float)>
        {
            { "moto-esportiva", ("Raio 600",       230f, 3400f, 58f, 1.6f) },  // esportiva: a mais rápida
            { "moto-cafe",      ("Café Racer 250", 200f, 2700f, 50f, 1.55f) },
            { "moto-chopper",   ("Lobo Chopper",   300f, 3500f, 48f, 1.45f) }, // pesada, faz curva pior
            { "moto-roxa",      ("Roxinha",        260f, 3100f, 52f, 1.5f) },
            { "moto-scooter",   ("Vespina 50",     110f, 900f,  20f, 1.4f) },  // a "Faggio": lenta e leve
        };

        public static VehicleController Build(GameObject go, Rigidbody rb, VehicleSpec spec, Color paint)
        {
            // modelo 3D preparado por Dev City → Preparar motos (.glb); senão, a moto de caixas abaixo
            Load();
            if (models.Length > 0)
            {
                int i = NextModel >= 0 ? NextModel % models.Length : Random.Range(0, models.Length);
                NextModel = -1;
                var built = BuildFromModel(go, rb, spec, models[i]);
                if (built != null) return built;
            }
            return BuildBoxes(go, rb, spec, paint);
        }

        /// <summary>
        /// Usa um prefab com as peças "Carroceria", "wheel-front", "wheel-back" e "Banco".
        /// As medidas (raio da roda, entre-eixos, altura do banco) vêm do próprio modelo.
        /// </summary>
        static VehicleController BuildFromModel(GameObject go, Rigidbody rb, VehicleSpec baseSpec, GameObject prefab)
        {
            var inst = Object.Instantiate(prefab);
            var wf = inst.transform.Find("wheel-front");
            var wbk = inst.transform.Find("wheel-back");
            var bancoT = inst.transform.Find("Banco");
            var bodyPart = inst.transform.Find("Carroceria");
            if (wf == null || wbk == null || bodyPart == null) { Object.Destroy(inst); return null; }

            var spec = baseSpec.Clone();
            if (Tuning.TryGetValue(prefab.name, out var tn)) { spec.Name = tn.name; spec.Mass = tn.mass; spec.Engine = tn.engine; spec.MaxSpeed = tn.maxSpeed; spec.Grip = tn.grip; }
            rb.mass = spec.Mass;

            float RadiusOf(Transform w) { var mf = w.GetComponent<MeshFilter>(); return mf != null && mf.sharedMesh != null ? mf.sharedMesh.bounds.extents.y : 0.3f; }
            float r = (RadiusOf(wf) + RadiusOf(wbk)) / 2f;
            var bodyBounds = bodyPart.GetComponent<MeshFilter>().sharedMesh.bounds;
            spec.WheelRadius = r;
            spec.Length = Mathf.Max(bodyBounds.max.z, wf.localPosition.z + r) - Mathf.Min(bodyBounds.min.z, wbk.localPosition.z - r);
            spec.Height = bodyBounds.max.y;
            spec.Width = 0.8f;

            // carroceria: o modelo sem as rodas
            var body = new GameObject("Carroceria").transform;
            body.SetParent(go.transform, false);
            inst.name = prefab.name;
            inst.transform.SetParent(body, false);
            var vc = go.AddComponent<VehicleController>();

            // rodas: cada uma vai para dentro de RodaX (suspensão) > Giro (gira ao andar)
            vc.wheelLocal = new Vector3[4];
            vc.wheelPivot = new Transform[4];
            vc.wheelSpin = new Transform[4];
            foreach (var (i, w) in new[] { (0, wf), (2, wbk) })
            {
                var wp = new Vector3(0f, r + 0.1f, w.localPosition.z);
                vc.wheelLocal[i] = wp;
                vc.wheelLocal[i + 1] = wp;
                var pivot = new GameObject("Roda" + i).transform;
                pivot.SetParent(go.transform, false);
                pivot.localPosition = wp - Vector3.up * 0.3f;
                var spin = new GameObject("Giro").transform;
                spin.SetParent(pivot, false);
                w.SetParent(spin, false);
                w.localPosition = Vector3.zero;
                vc.wheelPivot[i] = pivot;
                vc.wheelSpin[i] = spin;
            }

            // colisor: a carroceria, acima das rodas
            float bottom = Mathf.Max(bodyBounds.min.y, r + 0.1f);
            var col = go.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, (bottom + bodyBounds.max.y) / 2f, bodyBounds.center.z);
            col.size = new Vector3(Mathf.Min(bodyBounds.size.x, 0.6f), Mathf.Max(0.3f, bodyBounds.max.y - bottom), bodyBounds.size.z);
            col.sharedMaterial = new PhysicsMaterial("Moto") { dynamicFriction = 0.25f, staticFriction = 0.3f, bounciness = 0.05f, frictionCombine = PhysicsMaterialCombine.Minimum };
            rb.centerOfMass = new Vector3(0, r + 0.15f, 0);

            // farol e lanterna (pequenos, para acender à noite e no freio)
            float seatTop = bancoT != null ? bancoT.localPosition.y : r * 2.8f;
            float seatZ = bancoT != null ? bancoT.localPosition.z : -0.25f;
            var head = Gen.Cube(body, new Vector3(0, seatTop + 0.02f, bodyBounds.max.z - 0.08f), new Vector3(0.14f, 0.1f, 0.03f), Mats.HeadOff).GetComponent<Renderer>();
            var tail = Gen.Cube(body, new Vector3(0, seatTop - 0.05f, bodyBounds.min.z + 0.02f), new Vector3(0.12f, 0.05f, 0.03f), Mats.TailOff).GetComponent<Renderer>();
            var light = HeadLight(go.transform, new Vector3(0, seatTop, bodyBounds.max.z));

            // piloto: o quadril sentado fica ~0,43 m acima da origem do personagem
            var seat = new GameObject("Banco").transform;
            seat.SetParent(go.transform, false);
            seat.localPosition = new Vector3(0f, seatTop - 0.43f, seatZ);

            vc.bodyRenderers = bodyPart.GetComponents<Renderer>();
            vc.headRenderers = new[] { head };
            vc.tailRenderers = new[] { tail };
            vc.headLights = new[] { light };
            vc.ModelScale = 0.8f;
            vc.Setup(VehicleKind.Moto, spec, rb, seat);
            return vc;
        }

        static Light HeadLight(Transform parent, Vector3 pos)
        {
            var lg = new GameObject("Farol");
            lg.transform.SetParent(parent, false);
            lg.transform.localPosition = pos;
            lg.transform.localRotation = Quaternion.Euler(8f, 0, 0);
            var light = lg.AddComponent<Light>();
            light.type = LightType.Spot;
            light.range = 30f;
            light.spotAngle = 55f;
            light.intensity = 2f;
            light.color = new Color(1f, 0.95f, 0.85f);
            light.enabled = false;
            return light;
        }

        /// <summary>A moto de caixas e cilindros (usada quando não há nenhum modelo 3D preparado).</summary>
        static VehicleController BuildBoxes(GameObject go, Rigidbody rb, VehicleSpec spec, Color paint)
        {
            float L = spec.Length, r = spec.WheelRadius;
            float wz = L / 2f - r;   // distância do centro até o eixo de cada roda
            float h = r;             // altura do eixo das rodas (moto parada)
            var paintMat = Mats.Unique(paint, 0.8f, 0.4f);

            var body = new GameObject("Carroceria").transform;
            body.SetParent(go.transform, false);
            var bodyRenderers = new List<Renderer>();
            Renderer Paint(GameObject g) { var rr = g.GetComponent<Renderer>(); bodyRenderers.Add(rr); return rr; }

            // quadro e motor (preto), tanque, banco e rabeta (pintados)
            Gen.Cube(body, new Vector3(0, h + 0.18f, -0.05f), new Vector3(0.26f, 0.32f, 0.6f), Mats.Black);           // motor
            Gen.Cube(body, new Vector3(0, h + 0.38f, 0.05f), new Vector3(0.12f, 0.1f, L * 0.62f), Mats.Black);         // quadro
            Paint(Gen.Cube(body, new Vector3(0, h + 0.55f, 0.18f), new Vector3(0.36f, 0.24f, 0.5f), paintMat));        // tanque
            Gen.Cube(body, new Vector3(0, h + 0.52f, -0.32f), new Vector3(0.3f, 0.1f, 0.55f), Mats.Black);             // banco
            Paint(Gen.Cube(body, new Vector3(0, h + 0.55f, -0.72f), new Vector3(0.24f, 0.16f, 0.38f), paintMat));      // rabeta
            Paint(Gen.Cube(body, new Vector3(0, h + 0.68f, wz - 0.32f), new Vector3(0.4f, 0.34f, 0.26f), paintMat));   // carenagem
            Gen.Cube(body, new Vector3(0, h + 0.9f, wz - 0.36f), new Vector3(0.3f, 0.14f, 0.04f), Mats.Glass);        // bolha
            // garfo da frente, guidão e escapamento
            Gen.Cube(body, new Vector3(-0.09f, h + 0.32f, wz - 0.1f), new Vector3(0.05f, 0.7f, 0.05f), Mats.Rim);
            Gen.Cube(body, new Vector3(0.09f, h + 0.32f, wz - 0.1f), new Vector3(0.05f, 0.7f, 0.05f), Mats.Rim);
            Gen.Cube(body, new Vector3(0, h + 0.78f, wz - 0.48f), new Vector3(0.72f, 0.05f, 0.05f), Mats.Black);
            Gen.Prim(PrimitiveType.Cylinder, body, new Vector3(0.17f, h + 0.08f, -0.45f), new Vector3(0.1f, 0.32f, 0.1f), Mats.Rim, false, new Vector3(90f, 0, 0));
            // braço da roda de trás
            Gen.Cube(body, new Vector3(0.1f, h + 0.05f, -wz * 0.55f), new Vector3(0.05f, 0.08f, wz * 0.9f), Mats.Black);

            // farol e lanterna
            var head = Gen.Cube(body, new Vector3(0, h + 0.66f, wz - 0.18f), new Vector3(0.18f, 0.12f, 0.04f), Mats.HeadOff).GetComponent<Renderer>();
            var tail = Gen.Cube(body, new Vector3(0, h + 0.58f, -0.92f), new Vector3(0.16f, 0.06f, 0.03f), Mats.TailOff).GetComponent<Renderer>();

            var vc = go.AddComponent<VehicleController>();

            // rodas: só os índices 0 (frente) e 2 (trás); 1 e 3 ficam vazios (a física pula)
            vc.wheelLocal = new Vector3[4];
            vc.wheelPivot = new Transform[4];
            vc.wheelSpin = new Transform[4];
            foreach (int i in new[] { 0, 2 })
            {
                var wp = new Vector3(0, r + 0.1f, i == 0 ? wz : -wz);
                vc.wheelLocal[i] = wp;
                vc.wheelLocal[i + 1] = wp;
                var pivot = new GameObject("Roda" + i).transform;
                pivot.SetParent(go.transform, false);
                pivot.localPosition = wp - Vector3.up * 0.3f;
                var spin = new GameObject("Giro").transform;
                spin.SetParent(pivot, false);
                Gen.Prim(PrimitiveType.Cylinder, spin, Vector3.zero, new Vector3(r * 2f, 0.07f, r * 2f), Mats.Tire, false, new Vector3(0, 0, 90));
                Gen.Prim(PrimitiveType.Cylinder, spin, Vector3.zero, new Vector3(r * 1.25f, 0.075f, r * 1.25f), Mats.Rim, false, new Vector3(0, 0, 90));
                Gen.Cube(spin, Vector3.zero, new Vector3(0.08f, r * 1.2f, 0.06f), Mats.Black); // raio (mostra a roda girando)
                vc.wheelPivot[i] = pivot;
                vc.wheelSpin[i] = spin;
            }

            // colisor estreito, acima das rodas (quem toca o chão são os raios da suspensão)
            var col = go.AddComponent<BoxCollider>();
            col.center = new Vector3(0, h + 0.45f, 0);
            col.size = new Vector3(0.5f, 0.6f, L);
            col.sharedMaterial = new PhysicsMaterial("Moto") { dynamicFriction = 0.25f, staticFriction = 0.3f, bounciness = 0.05f, frictionCombine = PhysicsMaterialCombine.Minimum };
            rb.centerOfMass = new Vector3(0, h + 0.15f, 0); // centro de massa baixo = moto mais estável

            var light = HeadLight(go.transform, new Vector3(0, h + 0.66f, wz - 0.1f));

            // piloto montado: o quadril fica em cima do banco
            var seat = new GameObject("Banco").transform;
            seat.SetParent(go.transform, false);
            seat.localPosition = new Vector3(0, h + 0.1f, -0.3f);

            vc.bodyRenderers = bodyRenderers.ToArray();
            vc.headRenderers = new[] { head };
            vc.tailRenderers = new[] { tail };
            vc.headLights = new[] { light };
            vc.ModelScale = 0.8f;
            vc.Setup(VehicleKind.Moto, spec, rb, seat);
            return vc;
        }
    }
}

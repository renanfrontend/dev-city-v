using System.Collections.Generic;
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
        public static VehicleController Build(GameObject go, Rigidbody rb, VehicleSpec spec, Color paint)
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

            var lg = new GameObject("Farol");
            lg.transform.SetParent(go.transform, false);
            lg.transform.localPosition = new Vector3(0, h + 0.66f, wz - 0.1f);
            lg.transform.localRotation = Quaternion.Euler(8f, 0, 0);
            var light = lg.AddComponent<Light>();
            light.type = LightType.Spot;
            light.range = 30f;
            light.spotAngle = 55f;
            light.intensity = 2f;
            light.color = new Color(1f, 0.95f, 0.85f);
            light.enabled = false;

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

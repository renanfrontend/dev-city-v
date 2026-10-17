using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DevCity
{
    /// <summary>Monta os carros (modelo + física) por código. Nomes fictícios no estilo GTA.</summary>
    public static class VehicleFactory
    {
        static readonly Dictionary<VehicleKind, VehicleSpec> specs = new Dictionary<VehicleKind, VehicleSpec>
        {
            { VehicleKind.Sedan,   new VehicleSpec { Name = "Bravo Sedã",      Length = 4.6f, Width = 1.9f,  Height = 1.5f,  WheelRadius = 0.36f, Mass = 1400f, Engine = 10500f, MaxSpeed = 47f, Grip = 1.5f } },
            { VehicleKind.Sports,  new VehicleSpec { Name = "Fúria GT",        Length = 4.4f, Width = 2.0f,  Height = 1.25f, WheelRadius = 0.36f, Mass = 1300f, Engine = 16500f, MaxSpeed = 66f, Grip = 1.8f, AWD = true } },
            { VehicleKind.SUV,     new VehicleSpec { Name = "Montanha XL",     Length = 4.9f, Width = 2.05f, Height = 1.9f,  WheelRadius = 0.42f, Mass = 2100f, Engine = 14500f, MaxSpeed = 46f, Grip = 1.45f, AWD = true } },
            { VehicleKind.Taxi,    new VehicleSpec { Name = "Táxi Dev City",   Length = 4.6f, Width = 1.9f,  Height = 1.5f,  WheelRadius = 0.36f, Mass = 1450f, Engine = 10500f, MaxSpeed = 46f, Grip = 1.5f } },
            { VehicleKind.Van,     new VehicleSpec { Name = "Vanzão",          Length = 5.2f, Width = 2.1f,  Height = 2.35f, WheelRadius = 0.4f,  Mass = 2500f, Engine = 13000f, MaxSpeed = 38f, Grip = 1.35f } },
            { VehicleKind.Police,  new VehicleSpec { Name = "Viatura DCPD",    Length = 4.8f, Width = 1.95f, Height = 1.5f,  WheelRadius = 0.37f, Mass = 1650f, Engine = 15500f, MaxSpeed = 57f, Grip = 1.7f } },
            { VehicleKind.Swat,    new VehicleSpec { Name = "Blindado SWAT",   Length = 5.6f, Width = 2.3f,  Height = 2.4f,  WheelRadius = 0.48f, Mass = 3200f, Engine = 21000f, MaxSpeed = 46f, Grip = 1.5f, AWD = true, Health = 2200f } },
            { VehicleKind.Armored, new VehicleSpec { Name = "Carro-Forte",     Length = 6.4f, Width = 2.5f,  Height = 2.8f,  WheelRadius = 0.55f, Mass = 6500f, Engine = 30000f, MaxSpeed = 34f, Grip = 1.4f, Health = 3500f } },
        };

        static readonly Color[] paints =
        {
            new Color(0.75f, 0.08f, 0.08f), new Color(0.05f, 0.05f, 0.06f), new Color(0.92f, 0.92f, 0.92f), new Color(0.6f, 0.62f, 0.65f),
            new Color(0.08f, 0.2f, 0.5f), new Color(0.1f, 0.35f, 0.2f), new Color(0.95f, 0.6f, 0.1f), new Color(0.35f, 0.15f, 0.45f),
            new Color(0.2f, 0.55f, 0.75f), new Color(0.85f, 0.75f, 0.55f), new Color(0.95f, 0.85f, 0.15f), new Color(0.3f, 0.3f, 0.32f)
        };

        public static Color RandomPaint() => paints[Random.Range(0, paints.Length)];

        public static VehicleKind RandomCivilianKind()
        {
            float r = Random.value;
            if (r < 0.42f) return VehicleKind.Sedan;
            if (r < 0.55f) return VehicleKind.Sports;
            if (r < 0.75f) return VehicleKind.SUV;
            if (r < 0.88f) return VehicleKind.Taxi;
            return VehicleKind.Van;
        }

        public static VehicleController Create(VehicleKind kind, Vector3 pos, Quaternion rot, Color? paint = null)
        {
            var spec = specs[kind];
            var go = new GameObject(spec.Name);
            go.transform.SetPositionAndRotation(pos + Vector3.up * 0.15f, rot);

            var rb = go.AddComponent<Rigidbody>();
            rb.mass = spec.Mass;
            rb.linearDamping = 0.02f;
            rb.angularDamping = 0.6f;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.interpolation = RigidbodyInterpolation.None;

            // modelo 3D de verdade (ex.: Kenney Car Kit) em Resources/Veiculos/<nome>; senão, caixas
            var model = Resources.Load<GameObject>("Veiculos/" + ModelName(kind));
            if (model != null)
            {
                var built = BuildFromModel(go, rb, kind, spec, model);
                if (built != null) return built;
            }

            float L = spec.Length, W = spec.Width, H = spec.Height, r = spec.WheelRadius;
            float clear = r * 0.7f;
            float lowerH = H * (kind == VehicleKind.Sports ? 0.42f : 0.45f);
            bool boxy = kind == VehicleKind.Van || kind == VehicleKind.Swat || kind == VehicleKind.Armored;
            float cabH = H - lowerH - clear;
            float cabL = boxy ? L * 0.78f : L * (kind == VehicleKind.Sports ? 0.42f : 0.5f);
            float cabZ = boxy ? -L * 0.08f : -L * 0.04f;

            Color baseColor = paint ?? RandomPaint();
            if (kind == VehicleKind.Taxi) baseColor = new Color(0.98f, 0.78f, 0.1f);
            if (kind == VehicleKind.Police) baseColor = new Color(0.95f, 0.95f, 0.96f);
            if (kind == VehicleKind.Swat) baseColor = new Color(0.16f, 0.17f, 0.18f);
            if (kind == VehicleKind.Armored) baseColor = new Color(0.42f, 0.46f, 0.5f);
            var paintMat = Mats.Unique(baseColor, 0.75f, 0.35f);

            var body = new GameObject("Carroceria").transform;
            body.SetParent(go.transform, false);
            var bodyRenderers = new List<Renderer>();
            var lower = Gen.Cube(body, new Vector3(0, clear + lowerH / 2f, 0), new Vector3(W, lowerH, L), paintMat);
            bodyRenderers.Add(lower.GetComponent<Renderer>());
            // capô levemente mais baixo na frente
            var hood = Gen.Cube(body, new Vector3(0, clear + lowerH + 0.02f, L * 0.33f), new Vector3(W * 0.96f, 0.06f, L * 0.3f), paintMat);
            bodyRenderers.Add(hood.GetComponent<Renderer>());

            var cabMat = boxy ? paintMat : Mats.Glass;
            var cab = Gen.Cube(body, new Vector3(0, clear + lowerH + cabH / 2f, cabZ), new Vector3(W * 0.88f, cabH, cabL), cabMat);
            if (boxy) bodyRenderers.Add(cab.GetComponent<Renderer>());
            // teto pintado
            var roof = Gen.Cube(body, new Vector3(0, clear + lowerH + cabH + 0.03f, cabZ), new Vector3(W * 0.9f, 0.07f, cabL * (boxy ? 1f : 0.82f)), paintMat);
            bodyRenderers.Add(roof.GetComponent<Renderer>());
            if (boxy)
            {
                // para-brisa e janelas laterais
                Gen.Cube(body, new Vector3(0, clear + lowerH + cabH * 0.55f, cabZ + cabL / 2f + 0.01f), new Vector3(W * 0.8f, cabH * 0.5f, 0.04f), Mats.Glass);
                if (kind != VehicleKind.Armored)
                {
                    Gen.Cube(body, new Vector3(W * 0.445f, clear + lowerH + cabH * 0.6f, cabZ + cabL * 0.3f), new Vector3(0.02f, cabH * 0.4f, cabL * 0.3f), Mats.Glass);
                    Gen.Cube(body, new Vector3(-W * 0.445f, clear + lowerH + cabH * 0.6f, cabZ + cabL * 0.3f), new Vector3(0.02f, cabH * 0.4f, cabL * 0.3f), Mats.Glass);
                }
            }
            // para-choques
            Gen.Cube(body, new Vector3(0, clear + 0.18f, L / 2f + 0.03f), new Vector3(W * 0.98f, 0.22f, 0.1f), Mats.Black);
            Gen.Cube(body, new Vector3(0, clear + 0.18f, -L / 2f - 0.03f), new Vector3(W * 0.98f, 0.22f, 0.1f), Mats.Black);

            // faróis e lanternas
            var heads = new List<Renderer>();
            var tails = new List<Renderer>();
            for (int s = -1; s <= 1; s += 2)
            {
                heads.Add(Gen.Cube(body, new Vector3(s * W * 0.36f, clear + lowerH * 0.7f, L / 2f + 0.01f), new Vector3(W * 0.2f, 0.12f, 0.04f), Mats.HeadOff).GetComponent<Renderer>());
                tails.Add(Gen.Cube(body, new Vector3(s * W * 0.38f, clear + lowerH * 0.72f, -L / 2f - 0.01f), new Vector3(W * 0.18f, 0.12f, 0.04f), Mats.TailOff).GetComponent<Renderer>());
            }

            var vc = go.AddComponent<VehicleController>();

            // extras por tipo
            if (kind == VehicleKind.Police || kind == VehicleKind.Swat)
            {
                if (kind == VehicleKind.Police)
                {
                    var black = Mats.Get(new Color(0.04f, 0.04f, 0.05f), 0.6f, 0.3f);
                    Gen.Cube(body, new Vector3(0, clear + lowerH / 2f, L * 0.38f), new Vector3(W + 0.01f, lowerH + 0.01f, L * 0.24f), black);
                    Gen.Cube(body, new Vector3(0, clear + lowerH / 2f, -L * 0.38f), new Vector3(W + 0.01f, lowerH + 0.01f, L * 0.24f), black);
                }
                float top = clear + lowerH + cabH + 0.12f;
                Gen.Cube(body, new Vector3(0, top, cabZ), new Vector3(W * 0.7f, 0.1f, 0.3f), Mats.Black);
                vc.sirenA = Gen.Cube(body, new Vector3(-W * 0.2f, top + 0.08f, cabZ), new Vector3(W * 0.3f, 0.12f, 0.25f), Mats.SirenOff).GetComponent<Renderer>();
                vc.sirenB = Gen.Cube(body, new Vector3(W * 0.2f, top + 0.08f, cabZ), new Vector3(W * 0.3f, 0.12f, 0.25f), Mats.SirenOff).GetComponent<Renderer>();
            }
            if (kind == VehicleKind.Taxi)
                Gen.Cube(body, new Vector3(0, clear + lowerH + cabH + 0.18f, cabZ), new Vector3(0.7f, 0.22f, 0.3f), Mats.Emit(new Color(1f, 0.9f, 0.5f), 1.2f));
            if (kind == VehicleKind.Sports)
                Gen.Cube(body, new Vector3(0, clear + lowerH + 0.25f, -L / 2f + 0.15f), new Vector3(W * 0.9f, 0.05f, 0.3f), Mats.Black); // aerofólio

            // rodas
            vc.wheelLocal = new Vector3[4];
            vc.wheelPivot = new Transform[4];
            vc.wheelSpin = new Transform[4];
            float wx = W / 2f - 0.14f, wz = L / 2f - r - 0.45f;
            Vector3[] wp = { new Vector3(-wx, r + 0.1f, wz), new Vector3(wx, r + 0.1f, wz), new Vector3(-wx, r + 0.1f, -wz), new Vector3(wx, r + 0.1f, -wz) };
            for (int i = 0; i < 4; i++)
            {
                vc.wheelLocal[i] = wp[i];
                var pivot = new GameObject("Roda" + i).transform;
                pivot.SetParent(go.transform, false);
                pivot.localPosition = wp[i] - Vector3.up * 0.3f;
                var spin = new GameObject("Giro").transform;
                spin.SetParent(pivot, false);
                Gen.Prim(PrimitiveType.Cylinder, spin, Vector3.zero, new Vector3(r * 2f, 0.14f, r * 2f), Mats.Tire, false, new Vector3(0, 0, 90));
                Gen.Prim(PrimitiveType.Cylinder, spin, new Vector3(Mathf.Sign(wp[i].x) * 0.02f, 0, 0), new Vector3(r * 1.3f, 0.145f, r * 1.3f), Mats.Rim, false, new Vector3(0, 0, 90));
                Gen.Cube(spin, new Vector3(Mathf.Sign(wp[i].x) * 0.15f, 0, 0), new Vector3(0.01f, r * 1.1f, 0.12f), Mats.Black);
                vc.wheelPivot[i] = pivot;
                vc.wheelSpin[i] = spin;
            }

            // colisores: chassi + cabine
            var c1 = go.AddComponent<BoxCollider>();
            c1.center = new Vector3(0, clear + lowerH / 2f + 0.05f, 0);
            c1.size = new Vector3(W, lowerH - 0.1f, L);
            var c2 = go.AddComponent<BoxCollider>();
            c2.center = new Vector3(0, clear + lowerH + cabH / 2f, cabZ);
            c2.size = new Vector3(W * 0.88f, cabH, cabL);
            var pm = new PhysicsMaterial("Lataria") { dynamicFriction = 0.25f, staticFriction = 0.3f, bounciness = 0.05f, frictionCombine = PhysicsMaterialCombine.Minimum };
            c1.sharedMaterial = pm;
            c2.sharedMaterial = pm;
            rb.centerOfMass = new Vector3(0, clear + lowerH * 0.35f, 0.05f);

            // faróis de verdade (só ligam perto da câmera)
            var lights = new List<Light>();
            for (int s = -1; s <= 1; s += 2)
            {
                var lg = new GameObject("Farol");
                lg.transform.SetParent(go.transform, false);
                lg.transform.localPosition = new Vector3(s * W * 0.36f, clear + lowerH * 0.7f, L / 2f + 0.1f);
                lg.transform.localRotation = Quaternion.Euler(8f, 0, 0);
                var l = lg.AddComponent<Light>();
                l.type = LightType.Spot;
                l.range = 35f;
                l.spotAngle = 60f;
                l.intensity = 2.2f;
                l.color = new Color(1f, 0.95f, 0.85f);
                l.enabled = false;
                lights.Add(l);
            }

            var seat = new GameObject("Banco").transform;
            seat.SetParent(go.transform, false);
            seat.localPosition = new Vector3(-W * 0.22f, clear - 0.12f, cabZ + cabL * 0.12f);

            vc.bodyRenderers = bodyRenderers.ToArray();
            vc.headRenderers = heads.ToArray();
            vc.tailRenderers = tails.ToArray();
            vc.headLights = lights.ToArray();
            vc.Setup(kind, spec, rb, seat);
            return vc;
        }

        // ===================== modelos 3D (Kenney Car Kit ou outro) =====================

        /// <summary>
        /// Nome do arquivo em Assets/Resources/Veiculos para cada tipo de veículo.
        /// Para trocar um carro, coloque outro FBX lá e mude o nome aqui.
        /// </summary>
        public static string ModelName(VehicleKind kind)
        {
            switch (kind)
            {
                case VehicleKind.Sedan: return "sedan";
                case VehicleKind.Sports: return "sedan-sports";
                case VehicleKind.SUV: return "suv";
                case VehicleKind.Taxi: return "taxi";
                case VehicleKind.Van: return "van";
                case VehicleKind.Police: return "police";
                case VehicleKind.Swat: return "truck";
                case VehicleKind.Armored: return "delivery";
                default: return "";
            }
        }

        /// <summary>
        /// Monta o carro a partir de um modelo com rodas separadas (nomes com "wheel", "front"/"back").
        /// Mede o modelo, ajusta a escala para a largura do carro, coloca a frente para +Z,
        /// liga cada roda na suspensão e cria colisor, faróis e giroflex.
        /// </summary>
        static VehicleController BuildFromModel(GameObject go, Rigidbody rb, VehicleKind kind, VehicleSpec baseSpec, GameObject prefab)
        {
            var spec = baseSpec.Clone();
            var savedRot = go.transform.rotation;
            go.transform.rotation = Quaternion.identity; // mede tudo com o carro "reto"
            Vector3 origin = go.transform.position;

            var body = new GameObject("Carroceria").transform;
            body.SetParent(go.transform, false);
            var inst = Object.Instantiate(prefab, body);
            inst.name = prefab.name;
            inst.transform.localPosition = Vector3.zero;
            foreach (var c in inst.GetComponentsInChildren<Collider>(true)) Object.Destroy(c);

            var wheels = inst.GetComponentsInChildren<Transform>(true)
                .Where(t => t.name.ToLowerInvariant().Contains("wheel") && t.GetComponent<Renderer>() != null).ToList();
            if (wheels.Count < 4)
            {
                Debug.LogWarning($"[Veículos] O modelo \"{prefab.name}\" não tem 4 rodas separadas (nomes com \"wheel\"). Usando o carro de caixas.");
                Object.DestroyImmediate(body.gameObject);
                go.transform.rotation = savedRot;
                return null;
            }

            Bounds Measure(IEnumerable<Renderer> rs)
            {
                bool first = true;
                var b = new Bounds();
                foreach (var r in rs)
                {
                    var wb = r.bounds;
                    var lb = new Bounds(wb.center - origin, wb.size);
                    if (first) { b = lb; first = false; } else b.Encapsulate(lb);
                }
                return b;
            }
            bool IsFront(Transform t) => t.name.ToLowerInvariant().Contains("front");

            // a frente do carro tem que olhar para +Z (como o resto do jogo espera)
            float zFront = wheels.Where(IsFront).Average(w => w.GetComponent<Renderer>().bounds.center.z - origin.z);
            float zBack = wheels.Where(w => !IsFront(w)).DefaultIfEmpty(wheels[0]).Average(w => w.GetComponent<Renderer>().bounds.center.z - origin.z);
            if (zFront < zBack) inst.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);

            // escala: um pouco mais largo que a tabela specs (os carros da Kenney são "gordinhos")
            var all = inst.GetComponentsInChildren<Renderer>(true);
            var bAll = Measure(all);
            if (bAll.size.x > 0.01f) inst.transform.localScale *= spec.Width * 1.12f / bAll.size.x;
            bAll = Measure(all);
            inst.transform.localPosition += Vector3.up * -bAll.min.y; // rodas encostando no chão (y = 0)
            bAll = Measure(all);

            var bodyRenderers = all.Where(r => !wheels.Contains(r.transform)).ToList();
            var bBody = Measure(bodyRenderers);
            spec.Length = bAll.size.z;
            spec.Height = bAll.size.y;
            spec.Width = bAll.size.x;

            // rodas: 0 dianteira esq., 1 dianteira dir., 2 traseira esq., 3 traseira dir.
            var vc = go.AddComponent<VehicleController>();
            vc.wheelLocal = new Vector3[4];
            vc.wheelPivot = new Transform[4];
            vc.wheelSpin = new Transform[4];
            float radiusSum = 0f;
            var used = new bool[4];
            foreach (var w in wheels)
            {
                var wb = Measure(new[] { w.GetComponent<Renderer>() });
                Vector3 c = wb.center;
                bool front = c.z > bAll.center.z;
                bool left = c.x < 0f;
                int i = (front ? 0 : 2) + (left ? 0 : 1);
                if (used[i]) continue; // modelos com rodas duplas (caminhão): fica a primeira de cada canto
                used[i] = true;
                float r = wb.extents.y;
                radiusSum += r;
                var pivot = new GameObject("Roda" + i).transform;
                pivot.SetParent(go.transform, false);
                pivot.localPosition = c;
                var spin = new GameObject("Giro").transform;
                spin.SetParent(pivot, false);
                w.SetParent(spin, true);
                vc.wheelPivot[i] = pivot;
                vc.wheelSpin[i] = spin;
                vc.wheelLocal[i] = new Vector3(c.x, c.y + 0.1f, c.z); // ponto de onde sai o raio da suspensão
            }
            if (used.Any(u => !u))
            {
                Debug.LogWarning($"[Veículos] Não achei as 4 rodas (frente/trás, esquerda/direita) em \"{prefab.name}\". Usando o carro de caixas.");
                Object.Destroy(vc);
                Object.DestroyImmediate(body.gameObject);
                foreach (Transform t in go.transform) Object.DestroyImmediate(t.gameObject);
                go.transform.rotation = savedRot;
                return null;
            }
            spec.WheelRadius = radiusSum / 4f;

            // colisor: a carroceria (desce um pouco para o raio da suspensão sair de dentro dele)
            float mountY = vc.wheelLocal.Min(p => p.y);
            float bottom = Mathf.Min(bBody.min.y, mountY - 0.05f);
            var col = go.AddComponent<BoxCollider>();
            col.center = new Vector3(bBody.center.x, (bottom + bBody.max.y) / 2f, bBody.center.z);
            col.size = new Vector3(bBody.size.x, bBody.max.y - bottom, bBody.size.z);
            col.sharedMaterial = new PhysicsMaterial("Lataria") { dynamicFriction = 0.25f, staticFriction = 0.3f, bounciness = 0.05f, frictionCombine = PhysicsMaterialCombine.Minimum };
            rb.centerOfMass = new Vector3(0f, bottom + (bBody.max.y - bottom) * 0.3f, 0.05f);

            // faróis e lanternas (acendem por cima das luzes desenhadas na textura)
            var heads = new List<Renderer>();
            var tails = new List<Renderer>();
            float lightY = bBody.min.y + bBody.size.y * 0.35f;
            for (int s = -1; s <= 1; s += 2)
            {
                heads.Add(Gen.Cube(body, new Vector3(s * bBody.size.x * 0.33f, lightY, bBody.max.z + 0.01f), new Vector3(bBody.size.x * 0.18f, 0.1f, 0.03f), Mats.HeadOff).GetComponent<Renderer>());
                tails.Add(Gen.Cube(body, new Vector3(s * bBody.size.x * 0.35f, lightY, bBody.min.z - 0.01f), new Vector3(bBody.size.x * 0.16f, 0.1f, 0.03f), Mats.TailOff).GetComponent<Renderer>());
            }
            var lights = new List<Light>();
            for (int s = -1; s <= 1; s += 2)
            {
                var lg = new GameObject("Farol");
                lg.transform.SetParent(go.transform, false);
                lg.transform.localPosition = new Vector3(s * bBody.size.x * 0.33f, lightY, bBody.max.z + 0.1f);
                lg.transform.localRotation = Quaternion.Euler(8f, 0, 0);
                var l = lg.AddComponent<Light>();
                l.type = LightType.Spot; l.range = 35f; l.spotAngle = 60f; l.intensity = 2.2f;
                l.color = new Color(1f, 0.95f, 0.85f);
                l.enabled = false;
                lights.Add(l);
            }
            if (kind == VehicleKind.Police || kind == VehicleKind.Swat)
            {
                float top = bAll.max.y + 0.06f;
                vc.sirenA = Gen.Cube(body, new Vector3(-0.22f, top, bBody.center.z - 0.1f), new Vector3(0.4f, 0.12f, 0.22f), Mats.SirenOff).GetComponent<Renderer>();
                vc.sirenB = Gen.Cube(body, new Vector3(0.22f, top, bBody.center.z - 0.1f), new Vector3(0.4f, 0.12f, 0.22f), Mats.SirenOff).GetComponent<Renderer>();
            }

            var seat = new GameObject("Banco").transform;
            seat.SetParent(go.transform, false);
            seat.localPosition = new Vector3(-bBody.size.x * 0.22f, bottom + 0.05f, bBody.center.z + bBody.size.z * 0.05f);

            vc.bodyRenderers = bodyRenderers.ToArray();
            vc.headRenderers = heads.ToArray();
            vc.tailRenderers = tails.ToArray();
            vc.headLights = lights.ToArray();
            go.transform.rotation = savedRot;
            vc.Setup(kind, spec, rb, seat);
            return vc;
        }

        public static void Repaint(VehicleController v, Color c)
        {
            foreach (var r in v.bodyRenderers)
                if (r != null) r.material.color = c;
        }
    }
}

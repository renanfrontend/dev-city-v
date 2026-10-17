using UnityEngine;

namespace DevCity
{
    /// <summary>
    /// Câmera em terceira pessoa estilo GTA V: órbita livre com o mouse, mira por cima do ombro,
    /// câmera de perseguição atrás do carro (volta sozinha para trás), colisão com paredes,
    /// tremor de explosão, recuo de arma e sobrevoo cinematográfico na tela de título.
    /// </summary>
    public class CameraRig : MonoBehaviour
    {
        public Camera Camera { get; private set; }
        public float Yaw { get; private set; }
        public float Pitch { get; private set; } = 12f;
        public float Distance { get; private set; } = 4f;
        public bool TitleMode;
        public bool Override;   // outro sistema controla a câmera (ex.: câmera do céu na troca de personagem)

        PlayerController player;
        float shake, recoil, lastMouse, titleT, fov = 62f;
        Vector3 smoothPivot;

        public static CameraRig Create(PlayerController p)
        {
            var go = new GameObject("Camera");
            go.tag = "MainCamera";
            var rig = go.AddComponent<CameraRig>();
            rig.Camera = go.AddComponent<Camera>();
            rig.Camera.nearClipPlane = 0.08f;
            rig.Camera.farClipPlane = 2500f;
            rig.Camera.fieldOfView = 62f;
            go.AddComponent<AudioListener>();
            var amb = Sfx.Loop(go, Sfx.Ambience, 0.25f);
            amb.spatialBlend = 0f;
            rig.player = p;
            rig.Yaw = p.transform.eulerAngles.y;
            rig.smoothPivot = p.transform.position + Vector3.up * 1.6f;
            return rig;
        }

        public void Shake(float amount) => shake = Mathf.Max(shake, amount);

        /// <summary>Coloca a câmera atrás do jogador sem suavização (depois de um corte).</summary>
        public void SnapTo(float yaw, float pitch)
        {
            Yaw = yaw;
            Pitch = pitch;
            smoothPivot = player.transform.position + Vector3.up * 1.6f;
            Distance = 3.9f;
        }
        public void Recoil(float deg) { recoil += deg; }

        /// <summary>Gira a câmera suavemente na direção de um ponto (mira assistida).</summary>
        public void LookToward(Vector3 target, float speed)
        {
            Vector3 to = target - transform.position;
            float wantYaw = Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg;
            float wantPitch = -Mathf.Asin(Mathf.Clamp(to.normalized.y, -1f, 1f)) * Mathf.Rad2Deg;
            Yaw = Mathf.LerpAngle(Yaw, wantYaw, Time.deltaTime * speed);
            Pitch = Mathf.Lerp(Pitch, wantPitch, Time.deltaTime * speed);
        }

        void LateUpdate()
        {
            var gm = GameManager.I;
            if (gm == null || player == null) return;
            float dt = Time.unscaledDeltaTime;

            if (TitleMode) { TitleFlyover(dt); return; }
            if (Override) return;

            bool paused = gm.State == GameState.Paused || gm.State == GameState.Map || gm.State == GameState.Shop;
            var v = player.Vehicle;
            bool aiming = player.Weapons.Aiming;

            // mouse
            if (!paused && gm.State != GameState.Wasted && gm.State != GameState.Busted)
            {
                float sens = aiming ? 1.4f : 2.4f;
                if (aiming && player.Weapons.Def.Zoom > 0f && player.Weapons.Def.Zoom < 20f) sens = 0.4f;
                float mx = Input.GetAxisRaw("Mouse X") * sens;
                float my = Input.GetAxisRaw("Mouse Y") * sens;
                if (Mathf.Abs(mx) + Mathf.Abs(my) > 0.01f) lastMouse = Time.time;
                Yaw += mx;
                Pitch = Mathf.Clamp(Pitch - my, -55f, 75f);
            }

            // pivô e distância desejada
            Vector3 pivot;
            float dist;
            Vector3 side = Vector3.zero;
            float wantFov = 62f;
            if (v != null)
            {
                float speed = v.Body.linearVelocity.magnitude;
                pivot = v.transform.position + Vector3.up * (v.CameraHeight);
                dist = v.CameraDistance + speed * 0.04f;
                wantFov = 62f + Mathf.Clamp(speed * 0.35f, 0f, 14f);
                // sem mexer no mouse, a câmera volta para trás do carro (como no GTA)
                if (Time.time - lastMouse > 1.3f && speed > 3f && !aiming)
                {
                    Vector3 vel = v.Body.linearVelocity;
                    float forwardDot = Vector3.Dot(vel, v.transform.forward);
                    float behindYaw = v.transform.eulerAngles.y + (forwardDot < -2f ? 180f : 0f);
                    Yaw = Mathf.LerpAngle(Yaw, behindYaw, dt * 2.2f);
                    Pitch = Mathf.Lerp(Pitch, 10f, dt * 1.5f);
                }
                if (aiming) { dist = 4.5f; side = Quaternion.Euler(0, Yaw, 0) * Vector3.right * 0.6f; }
            }
            else
            {
                pivot = player.transform.position + Vector3.up * (player.Swimming ? 0.9f : 1.6f);
                dist = 3.9f;
                if (aiming)
                {
                    dist = 1.7f;
                    side = Quaternion.Euler(0, Yaw, 0) * Vector3.right * 0.62f;
                    pivot += Vector3.up * 0.05f;
                    wantFov = player.Weapons.Def.Zoom > 0f ? player.Weapons.Def.Zoom : 50f;
                }
            }
            smoothPivot = Vector3.Lerp(smoothPivot, pivot, 1f - Mathf.Exp(-dt * (v != null ? 14f : 20f)));
            if ((smoothPivot - pivot).sqrMagnitude > 100f) smoothPivot = pivot; // teleporte

            recoil = Mathf.Lerp(recoil, 0f, dt * 10f);
            Quaternion rot = Quaternion.Euler(Pitch - recoil, Yaw, 0f);
            Vector3 origin = smoothPivot + side;
            Vector3 wanted = origin - rot * Vector3.forward * dist;

            // colisão: não atravessa paredes
            float d = dist;
            if (Physics.SphereCast(origin, 0.22f, (wanted - origin).normalized, out var hit, dist, ~(1 << 2), QueryTriggerInteraction.Ignore))
                if (hit.rigidbody == null || hit.rigidbody.isKinematic) d = Mathf.Max(0.4f, hit.distance - 0.05f);
            Distance = Mathf.Lerp(Distance, d, d < Distance ? 1f : dt * 6f);

            Vector3 pos = origin - rot * Vector3.forward * Distance;
            if (shake > 0f)
            {
                shake = Mathf.Max(0f, shake - dt * 1.8f);
                pos += Random.insideUnitSphere * shake * 0.35f;
            }
            transform.SetPositionAndRotation(pos, rot);
            fov = Mathf.Lerp(fov, wantFov, dt * 8f);
            Camera.fieldOfView = fov;

            // câmera de morte: afasta devagar
            if (gm.State == GameState.Wasted) { Pitch = Mathf.Lerp(Pitch, 50f, dt * 0.6f); Yaw += dt * 12f; }
        }

        void TitleFlyover(float dt)
        {
            titleT += dt * 0.035f;
            Vector3 c = new Vector3(CityGenerator.Size / 2f, 0, CityGenerator.Size / 2f);
            Vector3 pos = c + new Vector3(Mathf.Cos(titleT) * 330f, 140f + Mathf.Sin(titleT * 2f) * 25f, Mathf.Sin(titleT) * 330f);
            transform.position = pos;
            transform.rotation = Quaternion.LookRotation(c + Vector3.up * 30f - pos);
            Camera.fieldOfView = 55f;
        }
    }
}

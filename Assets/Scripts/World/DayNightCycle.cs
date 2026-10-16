using UnityEngine;
using UnityEngine.Rendering;

namespace DevCity
{
    /// <summary>
    /// Ciclo de dia e noite no ritmo do GTA V: 1 minuto do jogo = 2 segundos reais (o dia dura 48 minutos).
    /// Controla o sol, o céu, a neblina, as janelas acesas, os postes e os semáforos.
    /// </summary>
    public class DayNightCycle : MonoBehaviour
    {
        public float Hour = 9f;
        public const float HoursPerSecond = 1f / 120f; // 0,5 min de jogo por segundo
        public Light Sun;
        public bool IsNight => Hour < 6.3f || Hour > 19.7f;
        public float Night01 { get; private set; }

        Material sky;
        float tlTimer;
        int tlPhase;
        static readonly float[] tlDur = { 12f, 2.5f, 12f, 2.5f };

        public void Init()
        {
            var go = new GameObject("Sol");
            go.transform.SetParent(transform, false);
            Sun = go.AddComponent<Light>();
            Sun.type = LightType.Directional;
            Sun.shadows = LightShadows.Soft;
            Sun.shadowStrength = 0.85f;
            RenderSettings.sun = Sun;

            sky = new Material(Shader.Find("Skybox/Procedural"));
            sky.SetFloat("_SunSize", 0.045f);
            sky.SetFloat("_AtmosphereThickness", 1.05f);
            RenderSettings.skybox = sky;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = 180f;
            RenderSettings.fogEndDistance = 900f;
            Mats.SetTrafficLights(0);
            Apply();
        }

        public void Advance(float hours) { Hour = Mathf.Repeat(Hour + hours, 24f); Apply(); }

        /// <summary>Semáforo: 0 = norte-sul verde, 1 = NS amarelo, 2 = leste-oeste verde, 3 = LO amarelo.</summary>
        public int TrafficPhase => tlPhase;
        public bool IsGreenFor(int axis) => (axis == 0 && tlPhase == 0) || (axis == 1 && tlPhase == 2);
        public bool IsYellowFor(int axis) => (axis == 0 && tlPhase == 1) || (axis == 1 && tlPhase == 3);

        void Update()
        {
            var gm = GameManager.I;
            if (gm == null) return;
            Hour = Mathf.Repeat(Hour + Time.deltaTime * HoursPerSecond, 24f);

            tlTimer += Time.deltaTime;
            if (tlTimer > tlDur[tlPhase])
            {
                tlTimer = 0f;
                tlPhase = (tlPhase + 1) % 4;
                Mats.SetTrafficLights(tlPhase);
            }
            Apply();
        }

        void Apply()
        {
            if (Sun == null) return;
            // o sol nasce às 6h no leste e se põe às 18h no oeste
            float t = (Hour - 6f) / 12f;                 // 0 = nascer, 1 = pôr
            float elev = Mathf.Sin(t * Mathf.PI) * 70f;  // altura em graus
            float azim = Mathf.Lerp(-80f, 80f, t) + 180f;
            bool day = Hour > 6f && Hour < 18f;
            if (!day) elev = -Mathf.Abs(elev) * 0.3f;
            Sun.transform.rotation = Quaternion.Euler(Mathf.Max(elev, 8f), azim, 0f);

            float daylight = Mathf.Clamp01(Mathf.Sin(t * Mathf.PI) * 2.2f);
            if (!day) daylight = 0f;
            float golden = day ? Mathf.Clamp01(1f - Mathf.Sin(t * Mathf.PI) * 2.5f) : 0f;

            Color dayCol = Color.Lerp(new Color(1f, 0.96f, 0.88f), new Color(1f, 0.6f, 0.35f), golden);
            Color nightCol = new Color(0.35f, 0.45f, 0.75f);
            Sun.color = Color.Lerp(nightCol, dayCol, daylight);
            Sun.intensity = Mathf.Lerp(0.12f, 1.25f, daylight);
            Sun.shadowStrength = Mathf.Lerp(0.3f, 0.85f, daylight);

            RenderSettings.ambientSkyColor = Color.Lerp(new Color(0.08f, 0.1f, 0.18f), new Color(0.55f, 0.65f, 0.8f), daylight);
            RenderSettings.ambientEquatorColor = Color.Lerp(new Color(0.07f, 0.07f, 0.12f), Color.Lerp(new Color(0.55f, 0.55f, 0.5f), new Color(0.7f, 0.45f, 0.35f), golden), daylight);
            RenderSettings.ambientGroundColor = Color.Lerp(new Color(0.03f, 0.03f, 0.05f), new Color(0.3f, 0.28f, 0.24f), daylight);
            RenderSettings.fogColor = Color.Lerp(new Color(0.05f, 0.06f, 0.11f), Color.Lerp(new Color(0.7f, 0.78f, 0.86f), new Color(0.9f, 0.6f, 0.45f), golden), daylight);
            if (sky != null)
            {
                sky.SetFloat("_Exposure", Mathf.Lerp(0.15f, 1.25f, daylight));
                sky.SetColor("_SkyTint", Color.Lerp(new Color(0.2f, 0.25f, 0.45f), new Color(0.5f, 0.55f, 0.65f), daylight));
                sky.SetColor("_GroundColor", Color.Lerp(new Color(0.05f, 0.05f, 0.08f), new Color(0.45f, 0.42f, 0.4f), daylight));
            }

            float night = 1f - Mathf.Clamp01(daylight * 1.6f);
            if (Mathf.Abs(night - Night01) > 0.01f || Time.frameCount < 3)
            {
                Night01 = night;
                Mats.SetNight(night);
            }
        }

        public string Clock => $"{Mathf.FloorToInt(Hour):00}:{Mathf.FloorToInt(Mathf.Repeat(Hour, 1f) * 60f):00}";
    }
}

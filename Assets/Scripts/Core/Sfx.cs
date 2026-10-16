using UnityEngine;

namespace DevCity
{
    /// <summary>
    /// Sons sintetizados na hora (não há arquivos de áudio no projeto): tiros, explosões,
    /// motor, sirene, buzina, dinheiro e as vinhetas de missão.
    /// </summary>
    public static class Sfx
    {
        public static AudioClip Pistol, Smg, Shotgun, Rifle, Sniper, Punch, Explosion, Engine, Siren, Horn, Cash, Pickup, Wasted, Passed, Failed, Crash, Heli, Click, Reload, Ambience;

        const int Rate = 44100;
        static AudioSource[] pool;
        static int next;
        static AudioSource ui;
        static System.Random rnd = new System.Random(1);

        public static void Init()
        {
            Pistol = Shot("pistola", 0.22f, 0.55f, 900f);
            Smg = Shot("smg", 0.14f, 0.5f, 1300f);
            Shotgun = Shot("escopeta", 0.45f, 0.75f, 500f);
            Rifle = Shot("fuzil", 0.26f, 0.65f, 800f);
            Sniper = Shot("sniper", 0.7f, 0.9f, 400f);
            Punch = Shot("soco", 0.09f, 0.4f, 200f);
            Explosion = Boom("explosao", 2.2f);
            Crash = Shot("batida", 0.35f, 0.6f, 300f);
            Engine = EngineLoop();
            Siren = SirenLoop();
            Horn = Tones("buzina", 0.5f, new[] { 392f, 494f }, 0.25f, square: true);
            Cash = Arp("dinheiro", new[] { 1318f, 1760f, 2093f }, 0.07f);
            Pickup = Arp("pegar", new[] { 880f, 1320f }, 0.08f);
            Passed = Arp("missao", new[] { 523f, 659f, 784f, 1046f, 784f, 1046f }, 0.14f);
            Failed = Arp("falhou", new[] { 440f, 392f, 330f, 262f }, 0.22f);
            Wasted = Boom("wasted", 2.5f, 0.35f);
            Heli = HeliLoop();
            Click = Shot("clique", 0.03f, 0.3f, 3000f);
            Reload = Arp("recarga", new[] { 300f, 220f }, 0.06f);
            Ambience = AmbienceLoop();

            var root = new GameObject("Audio");
            Object.DontDestroyOnLoad(root);
            pool = new AudioSource[24];
            for (int i = 0; i < pool.Length; i++)
            {
                var go = new GameObject("fonte" + i);
                go.transform.SetParent(root.transform);
                var s = go.AddComponent<AudioSource>();
                s.spatialBlend = 1f;
                s.rolloffMode = AudioRolloffMode.Linear;
                s.minDistance = 4f;
                s.maxDistance = 160f;
                s.dopplerLevel = 0f;
                pool[i] = s;
            }
            ui = root.AddComponent<AudioSource>();
            ui.spatialBlend = 0f;
            ui.ignoreListenerPause = true;
        }

        public static void PlayAt(AudioClip clip, Vector3 pos, float volume = 1f, float pitch = 1f, float maxDistance = 160f)
        {
            if (clip == null || pool == null) return;
            var s = pool[next];
            next = (next + 1) % pool.Length;
            s.transform.position = pos;
            s.pitch = pitch * Random.Range(0.94f, 1.06f);
            s.volume = volume;
            s.maxDistance = maxDistance;
            s.clip = clip;
            s.Play();
        }

        public static void Play2D(AudioClip clip, float volume = 1f)
        {
            if (clip != null && ui != null) ui.PlayOneShot(clip, volume);
        }

        /// <summary>Cria um AudioSource em loop preso a um objeto (motor, sirene, hélice).</summary>
        public static AudioSource Loop(GameObject owner, AudioClip clip, float volume, float maxDistance = 120f)
        {
            var s = owner.AddComponent<AudioSource>();
            s.clip = clip;
            s.loop = true;
            s.volume = volume;
            s.spatialBlend = 1f;
            s.rolloffMode = AudioRolloffMode.Linear;
            s.minDistance = 5f;
            s.maxDistance = maxDistance;
            s.dopplerLevel = 0.3f;
            s.Play();
            return s;
        }

        // ---------- síntese ----------

        static float Noise() => (float)rnd.NextDouble() * 2f - 1f;

        static AudioClip Make(string name, float[] data)
        {
            var c = AudioClip.Create(name, data.Length, 1, Rate, false);
            c.SetData(data, 0);
            return c;
        }

        /// <summary>Tiro: estalo de ruído com decaimento rápido + "corpo" grave.</summary>
        static AudioClip Shot(string name, float len, float vol, float body)
        {
            int n = (int)(Rate * len);
            var d = new float[n];
            float lp = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float env = Mathf.Exp(-t * (12f / len));
                lp += (Noise() - lp) * 0.35f;
                float thump = Mathf.Sin(2f * Mathf.PI * body * 0.12f * t) * Mathf.Exp(-t * 30f);
                d[i] = (lp * 0.85f + thump * 0.6f) * env * vol;
            }
            return Make(name, d);
        }

        static AudioClip Boom(string name, float len, float vol = 0.9f)
        {
            int n = (int)(Rate * len);
            var d = new float[n];
            float lp = 0f, lp2 = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float env = Mathf.Exp(-t * 2.4f) * Mathf.Clamp01(t * 200f);
                lp += (Noise() - lp) * 0.08f;
                lp2 += (lp - lp2) * 0.15f;
                float rumble = Mathf.Sin(2f * Mathf.PI * 38f * t) * Mathf.Exp(-t * 3f);
                d[i] = Mathf.Clamp((lp2 * 3.5f + rumble * 0.5f) * env * vol, -1f, 1f);
            }
            return Make(name, d);
        }

        static AudioClip EngineLoop()
        {
            // 1 segundo exato de motor a 60 Hz (loop sem emenda); o pitch muda com a rotação
            int n = Rate;
            var d = new float[n];
            float lp = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float ph = Mathf.Repeat(t * 60f, 1f);
                float saw = ph * 2f - 1f;
                float pulse = Mathf.Sin(2f * Mathf.PI * 30f * t) > 0.6f ? 0.6f : 0f;
                lp += (Noise() - lp) * 0.1f;
                d[i] = (saw * 0.35f + pulse * 0.3f + lp * 0.25f) * 0.5f;
            }
            return Make("motor", d);
        }

        static AudioClip SirenLoop()
        {
            int n = Rate * 2;
            var d = new float[n];
            float phase = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float f = 700f + 650f * (0.5f - 0.5f * Mathf.Cos(t * Mathf.PI)); // sobe e desce em 2 s
                phase += f / Rate;
                float s = Mathf.Sin(2f * Mathf.PI * phase);
                d[i] = (s + 0.3f * Mathf.Sign(s)) * 0.22f;
            }
            return Make("sirene", d);
        }

        static AudioClip HeliLoop()
        {
            int n = Rate;
            var d = new float[n];
            float lp = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                lp += (Noise() - lp) * 0.06f;
                float chop = Mathf.Pow(Mathf.Abs(Mathf.Sin(Mathf.PI * 12f * t)), 6f);
                d[i] = lp * (0.25f + chop * 1.4f) * 0.8f;
            }
            return Make("helicoptero", d);
        }

        static AudioClip AmbienceLoop()
        {
            int n = Rate * 4;
            var d = new float[n];
            float lp = 0f;
            for (int i = 0; i < n; i++)
            {
                lp += (Noise() - lp) * 0.02f;
                d[i] = lp * 0.25f;
            }
            // suaviza a emenda do loop
            for (int i = 0; i < 2000; i++) { float k = i / 2000f; d[i] *= k; d[n - 1 - i] *= k; }
            return Make("cidade", d);
        }

        static AudioClip Tones(string name, float len, float[] freqs, float vol, bool square = false)
        {
            int n = (int)(Rate * len);
            var d = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float s = 0f;
                foreach (var f in freqs)
                {
                    float v = Mathf.Sin(2f * Mathf.PI * f * t);
                    s += square ? Mathf.Sign(v) * 0.6f : v;
                }
                float env = Mathf.Clamp01(t * 80f) * Mathf.Clamp01((len - t) * 30f);
                d[i] = s / freqs.Length * env * vol;
            }
            return Make(name, d);
        }

        static AudioClip Arp(string name, float[] notes, float noteLen)
        {
            int per = (int)(Rate * noteLen);
            var d = new float[per * notes.Length + Rate / 4];
            for (int k = 0; k < notes.Length; k++)
                for (int i = 0; i < per * 3 && k * per + i < d.Length; i++)
                {
                    float t = i / (float)Rate;
                    float env = Mathf.Exp(-t * 7f) * Mathf.Clamp01(t * 300f);
                    d[k * per + i] += (Mathf.Sin(2f * Mathf.PI * notes[k] * t) + 0.3f * Mathf.Sin(4f * Mathf.PI * notes[k] * t)) * env * 0.3f;
                }
            return Make(name, d);
        }
    }
}

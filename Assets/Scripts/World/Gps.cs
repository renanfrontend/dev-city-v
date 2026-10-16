using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace DevCity
{
    /// <summary>
    /// GPS: calcula a rota pelas ruas até o objetivo da missão (amarelo) ou o waypoint marcado
    /// no mapa (roxo). O radar desenha essa rota.
    /// </summary>
    public class Gps : MonoBehaviour
    {
        public Vector3? Waypoint;
        public Transform MissionTargetTransform;
        public Vector3? MissionTarget;
        public Color MissionColor = new Color(1f, 0.85f, 0.2f);

        public readonly List<Vector3> Route = new List<Vector3>();
        public Color RouteColor { get; private set; }
        public int Version { get; private set; }

        float timer;
        Vector2Int lastA = new Vector2Int(-1, -1), lastB = new Vector2Int(-1, -1);
        bool hadRoute;

        public Vector3? Destination
        {
            get
            {
                if (MissionTargetTransform != null) return MissionTargetTransform.position;
                if (MissionTarget.HasValue) return MissionTarget;
                return Waypoint;
            }
        }

        public void SetWaypoint(Vector3? p)
        {
            Waypoint = p;
            timer = 0f;
            lastB = new Vector2Int(-1, -1);
        }

        void Update()
        {
            var gm = GameManager.I;
            if (gm == null || gm.Player == null) return;
            timer -= Time.unscaledDeltaTime;
            if (timer > 0f) return;
            timer = 0.4f;

            if (Waypoint.HasValue && Gen.FlatDist(gm.PlayerPos, Waypoint.Value) < 15f) Waypoint = null;

            var dest = Destination;
            if (!dest.HasValue)
            {
                if (hadRoute) { Route.Clear(); Version++; hadRoute = false; lastA = lastB = new Vector2Int(-1, -1); }
                return;
            }
            bool mission = MissionTargetTransform != null || MissionTarget.HasValue;
            var a = gm.City.NearestNode(gm.PlayerPos);
            var b = gm.City.NearestNode(dest.Value);
            Color c = mission ? MissionColor : new Color(0.75f, 0.35f, 1f);
            if (a == lastA && b == lastB && c == RouteColor && hadRoute) return;
            lastA = a; lastB = b;
            RouteColor = c;
            Route.Clear();
            foreach (var n in gm.City.FindPath(a, b)) Route.Add(CityGenerator.NodePos(n));
            Route.Add(new Vector3(dest.Value.x, 0f, dest.Value.z));
            hadRoute = true;
            Version++;
        }
    }

    /// <summary>Marcadores no mundo: a "corona" amarela no chão e a seta flutuante sobre alvos.</summary>
    public class Marker : MonoBehaviour
    {
        Transform follow;
        float height;
        Vector3 basePos;
        bool arrow;

        public static Marker Corona(Vector3 pos, Color color, float radius = 1.3f)
        {
            var go = new GameObject("Marcador");
            go.transform.position = pos;
            var m = go.AddComponent<Marker>();
            m.basePos = pos;
            var mat = Mats.Transparent(color, 0.28f);
            Gen.Prim(PrimitiveType.Cylinder, go.transform, new Vector3(0, 0.4f, 0), new Vector3(radius * 2f, 0.4f, radius * 2f), mat);
            Gen.Prim(PrimitiveType.Cylinder, go.transform, new Vector3(0, 0.02f, 0), new Vector3(radius * 2.1f, 0.01f, radius * 2.1f), Mats.Emit(color, 1.5f));
            var l = new GameObject("Luz").AddComponent<Light>();
            l.transform.SetParent(go.transform, false);
            l.transform.localPosition = Vector3.up * 1.2f;
            l.type = LightType.Point;
            l.color = color;
            l.range = radius * 4f;
            l.intensity = 1.5f;
            return m;
        }

        public static Marker Arrow(Transform target, Color color, float height = 2.6f)
        {
            var go = new GameObject("SetaAlvo");
            var m = go.AddComponent<Marker>();
            m.follow = target;
            m.height = height;
            m.arrow = true;
            var mat = Mats.Emit(color, 1.8f);
            // seta para baixo feita de dois blocos
            Gen.Cube(go.transform, new Vector3(0, 0.45f, 0), new Vector3(0.25f, 0.6f, 0.25f), mat);
            Gen.Cube(go.transform, new Vector3(0, 0.05f, 0), new Vector3(0.6f, 0.25f, 0.25f), mat).transform.localRotation = Quaternion.identity;
            Gen.Cube(go.transform, new Vector3(0, -0.1f, 0), new Vector3(0.3f, 0.2f, 0.25f), mat);
            return m;
        }

        public void MoveTo(Vector3 p) { basePos = p; transform.position = p; }

        void Update()
        {
            if (arrow)
            {
                if (follow == null) { Destroy(gameObject); return; }
                transform.position = follow.position + Vector3.up * (height + Mathf.Sin(Time.time * 3f) * 0.2f);
                transform.Rotate(0, 120f * Time.deltaTime, 0);
            }
            else
            {
                float s = 1f + Mathf.Sin(Time.time * 2.5f) * 0.05f;
                transform.localScale = new Vector3(s, 1f, s);
            }
        }
    }

    [System.Serializable]
    public class SaveData
    {
        public int money;
        public int missionsDone;
        public float raceBest;
        public float hour;
        public bool[] owned;
        public int[] ammo;
        public int[] clip;
        public float armor;
        public Color shirt, pants;
    }

    /// <summary>Salva em JSON ao lado do executável; lê saves antigos do PlayerPrefs.</summary>
    public static class SaveSystem
    {
        const string Key = "DevCityV_Save";
        static string FilePath => Path.Combine(Path.GetDirectoryName(Application.dataPath), "DevCityV_Save.json");

        public static bool Save()
        {
            var gm = GameManager.I;
            var w = gm.Player.Weapons;
            var d = new SaveData
            {
                money = gm.Money,
                missionsDone = gm.Missions.Done,
                raceBest = gm.Missions.RaceBest,
                hour = gm.DayNight.Hour,
                owned = w.Owned,
                ammo = w.Ammo,
                clip = w.Clip,
                armor = gm.Player.Armor,
                shirt = gm.Player.Outfit.Shirt,
                pants = gm.Player.Outfit.Pants,
            };
            try
            {
                File.WriteAllText(FilePath, JsonUtility.ToJson(d));
                return true;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("Não foi possível salvar o jogo: " + e.Message);
                return false;
            }
        }

        public static void Load()
        {
            var gm = GameManager.I;
            try
            {
                string json;
                if (File.Exists(FilePath)) json = File.ReadAllText(FilePath);
                else if (PlayerPrefs.HasKey(Key)) json = PlayerPrefs.GetString(Key);
                else return;
                var d = JsonUtility.FromJson<SaveData>(json);
                if (d == null) throw new InvalidDataException("Arquivo de save vazio.");
                gm.Money = d.money;
                gm.Missions.Done = d.missionsDone;
                gm.Missions.RaceBest = d.raceBest;
                gm.DayNight.Hour = d.hour;
                var w = gm.Player.Weapons;
                if (d.owned != null && d.owned.Length == w.Owned.Length)
                {
                    w.Owned = d.owned; w.Ammo = d.ammo; w.Clip = d.clip;
                }
                gm.Player.Armor = d.armor;
                var o = gm.Player.Outfit;
                if (d.shirt.a > 0f) { o.Shirt = d.shirt; o.Pants = d.pants; gm.Player.SetOutfit(o); }
                gm.Missions.RefreshGivers();
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("Save inválido, começando do zero: " + e.Message);
            }
        }

        public static void Delete()
        {
            try { if (File.Exists(FilePath)) File.Delete(FilePath); }
            catch (System.Exception e) { Debug.LogWarning("Não foi possível apagar o save: " + e.Message); }
            try { PlayerPrefs.DeleteKey(Key); }
            catch (System.Exception e) { Debug.LogWarning("Não foi possível apagar o save antigo: " + e.Message); }
        }
    }
}

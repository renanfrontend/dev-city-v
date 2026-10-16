using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace DevCity
{
    /// <summary>
    /// Radar do GTA V: gira com a câmera, aproxima/afasta com a velocidade, mostra a rota do GPS,
    /// ícones com letras, polícia piscando com cones de visão (quando você está escondido)
    /// e o mapa grande (M) onde você clica para marcar o destino.
    /// </summary>
    public class MinimapSystem : MonoBehaviour
    {
        const float W = 340f, H = 220f;

        HUD hud;
        Image frame;
        RectTransform pivot, mapRT, blipLayer;
        RawImage routeImg;
        Texture2D routeTex;
        Color32[] routeClear;
        int routeVersion = -1;
        Image playerArrow;
        readonly List<(Image img, Text txt)> pool = new List<(Image, Text)>();
        int used;

        GameObject bigRoot;
        RectTransform bigMap, bigBlips;
        readonly List<(Image img, Text txt)> bigPool = new List<(Image, Text)>();
        int bigUsed;
        Image bigPlayer;

        float pxPerMeter;

        public void Build(HUD h)
        {
            hud = h;
            var city = GameManager.I.City;
            pxPerMeter = CityGenerator.MapRes / city.MapSize;

            frame = h.Img("Radar", h.Root, Vector2.zero, Vector2.zero, new Vector2(40, 40), new Vector2(W, H), new Color(0.05f, 0.06f, 0.07f, 0.85f), Sprites.Rounded);
            frame.gameObject.AddComponent<Mask>().showMaskGraphic = true;

            pivot = h.Rect("Pivo", frame.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            var map = new GameObject("Mapa", typeof(RectTransform)).AddComponent<RawImage>();
            map.transform.SetParent(pivot, false);
            map.texture = city.MapTexture;
            map.raycastTarget = false;
            mapRT = map.rectTransform;
            mapRT.sizeDelta = new Vector2(CityGenerator.MapRes, CityGenerator.MapRes);
            map.color = new Color(1, 1, 1, 0.92f);

            routeTex = new Texture2D(512, 512, TextureFormat.RGBA32, false);
            routeTex.wrapMode = TextureWrapMode.Clamp;
            routeClear = new Color32[512 * 512];
            routeTex.SetPixels32(routeClear);
            routeTex.Apply();
            routeImg = new GameObject("Rota", typeof(RectTransform)).AddComponent<RawImage>();
            routeImg.transform.SetParent(mapRT, false);
            routeImg.texture = routeTex;
            routeImg.raycastTarget = false;
            routeImg.rectTransform.anchorMin = Vector2.zero;
            routeImg.rectTransform.anchorMax = Vector2.one;
            routeImg.rectTransform.sizeDelta = Vector2.zero;

            blipLayer = h.Stretch("Blips", frame.transform);
            playerArrow = h.Img("Jogador", frame.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(22, 22), Color.white, Sprites.Arrow);

            BuildBigMap(city);
        }

        void BuildBigMap(CityGenerator city)
        {
            bigRoot = hud.Stretch("MapaGrande", hud.Root).gameObject;
            var C = new Vector2(0.5f, 0.5f);
            hud.Img("Fundo", bigRoot.transform, C, C, Vector2.zero, new Vector2(4000, 3000), new Color(0.02f, 0.03f, 0.05f, 0.92f));
            var map = new GameObject("Mapa", typeof(RectTransform)).AddComponent<RawImage>();
            map.transform.SetParent(bigRoot.transform, false);
            map.texture = city.MapTexture;
            bigMap = map.rectTransform;
            bigMap.anchoredPosition = new Vector2(-120, 0);
            bigMap.sizeDelta = new Vector2(980, 980);
            var route = new GameObject("Rota", typeof(RectTransform)).AddComponent<RawImage>();
            route.transform.SetParent(bigMap, false);
            route.texture = routeTex;
            route.raycastTarget = false;
            route.rectTransform.anchorMin = Vector2.zero; route.rectTransform.anchorMax = Vector2.one; route.rectTransform.sizeDelta = Vector2.zero;
            bigBlips = hud.Stretch("Blips", bigMap);
            bigPlayer = hud.Img("Jogador", bigMap, C, C, Vector2.zero, new Vector2(26, 26), Color.white, Sprites.Arrow);

            var legend = hud.Txt("Legenda", bigRoot.transform, C, new Vector2(0, 0.5f), new Vector2(400, 0), new Vector2(520, 900), 24, TextAnchor.MiddleLeft, Color.white);
            legend.text =
                "<size=44><b>DEV CITY</b></size>\n\n" +
                "<color=#ffd84a>●</color> Objetivo / missão\n" +
                "<color=#c07aff>●</color> Destino marcado\n" +
                "<b>D N G P</b> Contatos com missão\n" +
                "<b>CR</b> Racha da Orla\n" +
                "<b>A</b> Arsenal (armas)\n" +
                "<b>$</b> Mercadinho 24h\n" +
                "<b>R</b> Moda Marginal (roupas)\n" +
                "<b>P</b> Pinta & Foge (some com as estrelas)\n" +
                "<b>C</b> Sua casa (salvar)\n" +
                "<b>H</b> Hospital   <b>D</b> Delegacia\n" +
                "<color=#ff4040>●</color><color=#4080ff>●</color> Polícia\n\n" +
                "<b>Clique</b> para marcar um destino no GPS\n<b>Botão direito</b> apaga o destino\n<b>M / Esc</b> fecha o mapa";
            bigRoot.SetActive(false);
        }

        public void ShowBigMap(bool on)
        {
            bigRoot.SetActive(on);
        }

        // ===================== ícones =====================

        (Image img, Text txt) Get(List<(Image img, Text txt)> list, ref int count, RectTransform parent)
        {
            if (count >= list.Count)
            {
                var img = hud.Img("Blip", parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(16, 16), Color.white, Sprites.Circle);
                var txt = hud.Txt("Letra", img.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(40, 30), 15, TextAnchor.MiddleCenter, Color.white, true, false);
                list.Add((img, txt));
            }
            var e = list[count++];
            e.img.gameObject.SetActive(true);
            e.img.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            e.img.rectTransform.localRotation = Quaternion.identity;
            e.txt.text = "";
            return e;
        }

        Vector2 ToRadar(Vector3 world, Vector3 center, float yawDeg, float zoom)
        {
            float dx = (world.x - center.x) * pxPerMeter * zoom;
            float dz = (world.z - center.z) * pxPerMeter * zoom;
            float a = yawDeg * Mathf.Deg2Rad;
            return new Vector2(dx * Mathf.Cos(a) - dz * Mathf.Sin(a), dx * Mathf.Sin(a) + dz * Mathf.Cos(a));
        }

        void Blip(Vector3 world, Vector3 center, float yaw, float zoom, Sprite s, Color c, float size, bool edge, string letter = null)
        {
            Vector2 p = ToRadar(world, center, yaw, zoom);
            float hx = W / 2f - 10f, hy = H / 2f - 10f;
            bool outside = Mathf.Abs(p.x) > hx || Mathf.Abs(p.y) > hy;
            if (outside)
            {
                if (!edge) return;
                float k = Mathf.Min(hx / Mathf.Max(Mathf.Abs(p.x), 0.01f), hy / Mathf.Max(Mathf.Abs(p.y), 0.01f));
                p *= k;
            }
            var e = Get(pool, ref used, blipLayer);
            e.img.sprite = s;
            e.img.color = c;
            e.img.rectTransform.anchoredPosition = p;
            e.img.rectTransform.sizeDelta = new Vector2(size, size);
            if (letter != null) { e.txt.text = letter; e.txt.fontSize = letter.Length > 1 ? 12 : 15; }
        }

        void Cone(Vector3 world, float headingDeg, Vector3 center, float yaw, float zoom, float rangeM)
        {
            Vector2 p = ToRadar(world, center, yaw, zoom);
            if (Mathf.Abs(p.x) > W || Mathf.Abs(p.y) > H) return;
            var e = Get(pool, ref used, blipLayer);
            e.img.sprite = Sprites.Cone;
            e.img.color = new Color(0.35f, 0.55f, 1f, 0.7f);
            e.img.rectTransform.pivot = new Vector2(0.5f, 0f);
            e.img.rectTransform.anchoredPosition = p;
            float s = rangeM * pxPerMeter * zoom;
            e.img.rectTransform.sizeDelta = new Vector2(s * 1.25f, s);
            e.img.rectTransform.localRotation = Quaternion.Euler(0, 0, -(headingDeg - yaw));
            e.img.transform.SetAsFirstSibling();
        }

        // ===================== atualização =====================

        void LateUpdate()
        {
            var gm = GameManager.I;
            if (gm == null || gm.Player == null) return;
            bool show = gm.State != GameState.Title;
            frame.gameObject.SetActive(show);
            if (!show) return;

            var city = gm.City;
            var player = gm.Player;
            Vector3 center = player.Position;
            float yaw = gm.Cam.Yaw;
            float speed = player.Vehicle != null ? player.Vehicle.Speed : 0f;
            float zoom = Mathf.Lerp(2.6f, 1.25f, Mathf.Clamp01(speed / 40f));

            Vector2 uv = city.WorldToMap01(center);
            mapRT.anchoredPosition = -(uv - new Vector2(0.5f, 0.5f)) * CityGenerator.MapRes;
            pivot.localScale = Vector3.one * zoom;
            pivot.localEulerAngles = new Vector3(0, 0, yaw);

            float heading = player.Vehicle != null ? player.Vehicle.transform.eulerAngles.y : player.transform.eulerAngles.y;
            playerArrow.rectTransform.localRotation = Quaternion.Euler(0, 0, -(heading - yaw));
            playerArrow.color = gm.Wanted.Searching && !gm.Wanted.Seen ? new Color(0.75f, 0.75f, 0.75f) : Color.white;

            // radar pisca vermelho/azul durante a perseguição
            bool chase = gm.Wanted.Stars > 0 && gm.Wanted.Seen;
            frame.color = chase ? (Mathf.Repeat(Time.time * 2f, 1f) < 0.5f ? new Color(0.35f, 0.05f, 0.08f, 0.9f) : new Color(0.05f, 0.1f, 0.4f, 0.9f)) : new Color(0.05f, 0.06f, 0.07f, 0.85f);

            DrawRoute(gm);

            used = 0;
            float t = Time.time;
            bool flash = Mathf.Repeat(t * 3f, 1f) < 0.5f;

            // cones de visão da polícia (só quando ela perdeu você de vista)
            if (gm.Wanted.Searching)
            {
                foreach (var u in gm.Police.Units) if (u != null && u.HasNpcDriver) Cone(u.transform.position, u.transform.eulerAngles.y, center, yaw, zoom, 85f);
                foreach (var c in gm.Police.FootCops) if (c != null && !c.IsDead) Cone(c.transform.position, c.transform.eulerAngles.y, center, yaw, zoom, 60f);
            }

            // lugares fixos
            foreach (var pt in new[] { PlaceType.GunShop, PlaceType.Store, PlaceType.Clothes, PlaceType.PaySpray, PlaceType.Safehouse, PlaceType.Hospital, PlaceType.Police })
            {
                var pl = city.Places[pt];
                Blip(pl.Door, center, yaw, zoom, Sprites.Circle, pl.BlipColor * 0.9f, 20f, false, pl.Letter);
            }
            // contatos com missão e racha
            foreach (var pt in gm.Missions.ActiveGiverPlaces())
            {
                var pl = city.Places[pt];
                Blip(pl.Door, center, yaw, zoom, Sprites.Circle, pl.BlipColor, 24f, true, pt == PlaceType.Race ? "CR" : pl.Letter);
            }
            // inimigos de missão
            foreach (var e in gm.Missions.EnemyBlips) if (e != null) Blip(e.position, center, yaw, zoom, Sprites.Circle, new Color(1f, 0.25f, 0.25f), 11f, false);
            // polícia
            foreach (var u in gm.Police.Units) if (u != null && !u.Destroyed) Blip(u.transform.position, center, yaw, zoom, Sprites.Circle, flash ? new Color(1f, 0.2f, 0.2f) : new Color(0.25f, 0.45f, 1f), 12f, false);
            foreach (var c in gm.Police.FootCops) if (c != null && !c.IsDead) Blip(c.transform.position, center, yaw, zoom, Sprites.Circle, flash ? new Color(0.25f, 0.45f, 1f) : new Color(1f, 0.2f, 0.2f), 9f, false);
            foreach (var hl in gm.Police.Helis) if (hl != null && !hl.IsDead) Blip(hl.transform.position, center, yaw, zoom, Sprites.Diamond, flash ? new Color(1f, 0.2f, 0.2f) : new Color(0.25f, 0.45f, 1f), 18f, true);
            // destino
            var gps = gm.Gps;
            if (gps.Waypoint.HasValue) Blip(gps.Waypoint.Value, center, yaw, zoom, Sprites.Circle, new Color(0.75f, 0.45f, 1f), 16f, true);
            var dest = gps.MissionTargetTransform != null ? gps.MissionTargetTransform.position : gps.MissionTarget;
            if (dest.HasValue) Blip(dest.Value, center, yaw, zoom, Sprites.Circle, gps.MissionColor, 18f, true);

            for (int i = used; i < pool.Count; i++) pool[i].img.gameObject.SetActive(false);

            if (bigRoot.activeSelf) BigMapUpdate(gm);
        }

        void DrawRoute(GameManager gm)
        {
            var gps = gm.Gps;
            if (gps.Version == routeVersion) return;
            routeVersion = gps.Version;
            var px = (Color32[])routeClear.Clone();
            Color32 c = gps.RouteColor;
            var city = gm.City;
            for (int i = 0; i + 1 < gps.Route.Count; i++)
            {
                Vector2 a = city.WorldToMap01(gps.Route[i]) * 512f;
                Vector2 b = city.WorldToMap01(gps.Route[i + 1]) * 512f;
                float len = Vector2.Distance(a, b);
                int steps = Mathf.Max(1, Mathf.CeilToInt(len));
                for (int s = 0; s <= steps; s++)
                {
                    Vector2 p = Vector2.Lerp(a, b, s / (float)steps);
                    for (int oy = -1; oy <= 1; oy++)
                        for (int ox = -1; ox <= 1; ox++)
                        {
                            int x = (int)p.x + ox, y = (int)p.y + oy;
                            if (x >= 0 && y >= 0 && x < 512 && y < 512) px[y * 512 + x] = c;
                        }
                }
            }
            routeTex.SetPixels32(px);
            routeTex.Apply();
        }

        // ===================== mapa grande =====================

        void BigMapUpdate(GameManager gm)
        {
            var city = gm.City;
            float size = bigMap.sizeDelta.x;
            Vector2 Pos(Vector3 w) => (city.WorldToMap01(w) - new Vector2(0.5f, 0.5f)) * size;

            bigPlayer.rectTransform.anchoredPosition = Pos(gm.PlayerPos);
            float heading = gm.Player.Vehicle != null ? gm.Player.Vehicle.transform.eulerAngles.y : gm.Player.transform.eulerAngles.y;
            bigPlayer.rectTransform.localRotation = Quaternion.Euler(0, 0, -heading);

            bigUsed = 0;
            void B(Vector3 w, Color c, float s, string letter = null)
            {
                var e = Get(bigPool, ref bigUsed, bigBlips);
                e.img.sprite = Sprites.Circle;
                e.img.color = c;
                e.img.rectTransform.anchoredPosition = Pos(w);
                e.img.rectTransform.sizeDelta = new Vector2(s, s);
                if (letter != null) { e.txt.text = letter; e.txt.fontSize = letter.Length > 1 ? 13 : 17; }
            }
            foreach (var pl in city.Places.Values)
            {
                if (pl.Type == PlaceType.Bar || pl.Type == PlaceType.GangPark || pl.Type == PlaceType.Garage || pl.Type == PlaceType.Race) continue;
                if (pl.Type >= PlaceType.MissionD) continue;
                B(pl.Door, pl.BlipColor * 0.9f, 26f, pl.Letter);
            }
            foreach (var pt in gm.Missions.ActiveGiverPlaces())
            {
                var pl = city.Places[pt];
                B(pl.Door, pl.BlipColor, 30f, pt == PlaceType.Race ? "CR" : pl.Letter);
            }
            var gps = gm.Gps;
            if (gps.Waypoint.HasValue) B(gps.Waypoint.Value, new Color(0.75f, 0.45f, 1f), 20f);
            var dest = gps.MissionTargetTransform != null ? gps.MissionTargetTransform.position : gps.MissionTarget;
            if (dest.HasValue) B(dest.Value, gps.MissionColor, 22f);
            bool flash = Mathf.Repeat(Time.unscaledTime * 3f, 1f) < 0.5f;
            foreach (var u in gm.Police.Units) if (u != null && !u.Destroyed) B(u.transform.position, flash ? Color.red : Color.blue, 12f);
            for (int i = bigUsed; i < bigPool.Count; i++) bigPool[i].img.gameObject.SetActive(false);

            // clique marca o destino (waypoint) no GPS
            if (Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1))
            {
                if (RectTransformUtility.ScreenPointToLocalPointInRectangle(bigMap, Input.mousePosition, null, out var local))
                {
                    Vector2 uv = local / size + new Vector2(0.5f, 0.5f);
                    if (uv.x >= 0 && uv.y >= 0 && uv.x <= 1 && uv.y <= 1)
                    {
                        if (Input.GetMouseButtonDown(1)) gps.SetWaypoint(null);
                        else { gps.SetWaypoint(city.Map01ToWorld(uv)); Sfx.Play2D(Sfx.Click, 0.6f); }
                    }
                }
            }
        }
    }
}

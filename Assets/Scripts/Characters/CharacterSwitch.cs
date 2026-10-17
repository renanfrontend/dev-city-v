using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace DevCity
{
    /// <summary>Tudo o que é "de cada protagonista": dinheiro, vida, armas, roupa e onde ele parou.</summary>
    [System.Serializable]
    public class CharacterProfile
    {
        public string Name;
        public string ModelName;      // prefab em Resources/Personagens/<ModelName> (Mixamo)
        public PlaceType Home;
        public Color Accent;          // cor do personagem na interface
        public int Money;
        public float Health = PlayerController.MaxHealth;
        public float Armor;
        public bool[] Owned;
        public int[] Ammo, Clip;
        public int Current;
        public Color Skin, Hair, Shirt, Pants, Shoes;
        public Vector3 Position;
        public float Yaw;
        public bool HasPosition;

        public Outfit GetOutfit() => new Outfit { Skin = Skin, Hair = Hair, Shirt = Shirt, Pants = Pants, Shoes = Shoes };

        public void SetOutfit(Outfit o)
        {
            Skin = o.Skin; Hair = o.Hair; Shirt = o.Shirt; Pants = o.Pants; Shoes = o.Shoes;
        }
    }

    /// <summary>
    /// Troca de protagonista no estilo GTA V (Trevor e Jacob).
    /// Segure Alt, mova o mouse para o lado do personagem e solte: a câmera sobe para o céu,
    /// atravessa a cidade e desce no outro protagonista, que estava vivendo a vida dele.
    /// Não dá para trocar durante missões nem com a polícia atrás de você (como no GTA V).
    /// </summary>
    public class CharacterSwitch : MonoBehaviour
    {
        public CharacterProfile[] Profiles;
        public int Active;
        public bool Switching { get; private set; }
        public CharacterProfile Current => Profiles[Active];

        GameObject panel;
        Image[] cards;
        Text[] names, infos;
        bool open;
        float wheelX;
        int selected;

        // ===================== criação =====================

        public void Init()
        {
            Weapons.Init();
            var gm = GameManager.I;
            var trevor = new CharacterProfile
            {
                Name = "Trevor", ModelName = "Trevor", Home = PlaceType.Safehouse, Accent = new Color(1f, 0.6f, 0.2f),
                Money = gm.Money,
                Skin = new Color(0.93f, 0.78f, 0.66f), Hair = new Color(0.35f, 0.3f, 0.27f),
                Shirt = new Color(0.8f, 0.77f, 0.68f), Pants = new Color(0.32f, 0.26f, 0.2f), Shoes = new Color(0.25f, 0.2f, 0.15f),
            };
            var jacob = new CharacterProfile
            {
                Name = "Jacob", ModelName = "Jacob", Home = PlaceType.SafehouseJacob, Accent = new Color(0.35f, 0.75f, 1f),
                Money = 300,
                Skin = new Color(0.45f, 0.3f, 0.2f), Hair = new Color(0.05f, 0.04f, 0.04f),
                Shirt = new Color(0.12f, 0.3f, 0.55f), Pants = new Color(0.15f, 0.15f, 0.18f), Shoes = new Color(0.95f, 0.95f, 0.95f),
            };
            // armas iniciais: Trevor fica com o que o jogador já tem; Jacob começa só com a pistola
            var w = gm.Player.Weapons;
            trevor.Owned = (bool[])w.Owned.Clone(); trevor.Ammo = (int[])w.Ammo.Clone(); trevor.Clip = (int[])w.Clip.Clone();
            int n = Weapons.All.Length;
            jacob.Owned = new bool[n]; jacob.Ammo = new int[n]; jacob.Clip = new int[n];
            jacob.Owned[0] = true; jacob.Owned[1] = true; jacob.Clip[1] = Weapons.Pistol.Mag; jacob.Ammo[1] = 24;

            Profiles = new[] { trevor, jacob };
            Active = 0;
            LoadActive(false);
            BuildUI();
        }

        // ===================== guardar / carregar o protagonista =====================

        /// <summary>Copia o estado do jogador para o perfil ativo.</summary>
        public void StoreActive()
        {
            var gm = GameManager.I;
            var p = Profiles[Active];
            var pl = gm.Player;
            var w = pl.Weapons;
            p.Money = gm.Money;
            p.Health = pl.Health;
            p.Armor = pl.Armor;
            p.Owned = (bool[])w.Owned.Clone();
            p.Ammo = (int[])w.Ammo.Clone();
            p.Clip = (int[])w.Clip.Clone();
            p.Current = w.Current;
            p.SetOutfit(pl.Outfit);
            p.Position = pl.Position;
            p.Yaw = pl.transform.eulerAngles.y;
            p.HasPosition = true;
        }

        /// <summary>Aplica o perfil ativo no jogador (modelo, roupa, dinheiro, armas).</summary>
        public void LoadActive(bool teleport)
        {
            var gm = GameManager.I;
            var p = Profiles[Active];
            var pl = gm.Player;
            var w = pl.Weapons;
            gm.Money = p.Money;
            pl.Health = p.Health > 0f ? p.Health : PlayerController.MaxHealth;
            pl.Armor = p.Armor;
            if (p.Owned != null && p.Owned.Length == w.Owned.Length)
            {
                w.Owned = (bool[])p.Owned.Clone();
                w.Ammo = (int[])p.Ammo.Clone();
                w.Clip = (int[])p.Clip.Clone();
            }
            w.Select(w.Owned[p.Current] ? p.Current : 0);
            pl.SetCharacter(p.ModelName, p.GetOutfit());
            if (teleport)
            {
                Vector3 pos = p.HasPosition ? p.Position : gm.City.SpawnPoint(p.Home);
                if (gm.City.IsWater(pos)) pos = gm.City.SpawnPoint(p.Home);
                pl.Teleport(pos + Vector3.up * 0.1f);
                pl.transform.rotation = Quaternion.Euler(0f, p.Yaw, 0f);
            }
        }

        // ===================== troca =====================

        string BlockReason()
        {
            var gm = GameManager.I;
            if (gm.Missions.InMission) return "Não dá para trocar de personagem durante uma missão.";
            if (gm.Wanted.Stars > 0) return "Despiste a polícia antes de trocar de personagem.";
            if (gm.Player.IsDead || gm.Player.Busy) return "Agora não dá para trocar.";
            return null;
        }

        public void SwitchTo(int index)
        {
            if (Switching || index == Active || index < 0 || index >= Profiles.Length) return;
            StartCoroutine(SwitchRoutine(index));
        }

        IEnumerator SwitchRoutine(int to)
        {
            Switching = true;
            var gm = GameManager.I;
            var cam = gm.Cam;
            var pl = gm.Player;
            if (pl.Vehicle != null) pl.ExitVehicle(false);
            StoreActive();
            gm.Pause(GameState.Cutscene);
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            cam.Override = true;

            Vector3 from = pl.Position;
            var next = Profiles[to];
            Vector3 dest = next.HasPosition ? next.Position : gm.City.SpawnPoint(next.Home);
            Quaternion down = Quaternion.Euler(88f, cam.Yaw, 0f);

            // sobe em 3 saltos, como a câmera do céu do GTA V
            float[] heights = { 35f, 130f, 300f };
            Vector3 pos = cam.transform.position;
            Quaternion rot = cam.transform.rotation;
            foreach (var h in heights)
            {
                Sfx.Play2D(Sfx.Click, 0.5f);
                yield return Move(cam.transform, pos, rot, from + Vector3.up * h, down, 0.32f);
                pos = from + Vector3.up * h;
                rot = down;
                yield return new WaitForSecondsRealtime(0.12f);
            }

            // troca o personagem lá em cima (ninguém vê)
            Active = to;
            LoadActive(true);
            gm.Hud.ShowVehicleName(null);

            // atravessa a cidade e desce
            Vector3 top = dest + Vector3.up * 300f;
            yield return Move(cam.transform, pos, down, top, down, 1.1f);
            pos = top;
            for (int i = heights.Length - 2; i >= 0; i--)
            {
                Sfx.Play2D(Sfx.Click, 0.5f);
                Vector3 p2 = dest + Vector3.up * heights[i];
                yield return Move(cam.transform, pos, down, p2, down, 0.32f);
                pos = p2;
                yield return new WaitForSecondsRealtime(0.12f);
            }
            // último trecho: para atrás do personagem
            float yaw = gm.Player.transform.eulerAngles.y;
            Quaternion behind = Quaternion.Euler(12f, yaw, 0f);
            Vector3 final = gm.Player.Position + Vector3.up * 1.6f - behind * Vector3.forward * 3.9f;
            yield return Move(cam.transform, pos, down, final, behind, 0.45f);

            cam.SnapTo(yaw, 12f);
            cam.Override = false;
            gm.SetPlaying();
            gm.Hud.MissionTitle(next.Name, gm.City.AreaName(gm.Player.Position));
            Switching = false;
        }

        static IEnumerator Move(Transform t, Vector3 a, Quaternion ra, Vector3 b, Quaternion rb, float time)
        {
            for (float k = 0f; k < time; k += Time.unscaledDeltaTime)
            {
                float e = Mathf.SmoothStep(0f, 1f, k / time);
                t.SetPositionAndRotation(Vector3.Lerp(a, b, e), Quaternion.Slerp(ra, rb, e));
                yield return null;
            }
            t.SetPositionAndRotation(b, rb);
        }

        // ===================== seletor (segurar Alt) =====================

        void BuildUI()
        {
            var hud = GameManager.I.Hud;
            var C = new Vector2(0.5f, 0.5f);
            panel = hud.Stretch("TrocaPersonagem", hud.Root).gameObject;
            hud.Img("Escurecer", panel.transform, C, C, Vector2.zero, new Vector2(4000, 3000), new Color(0, 0, 0, 0.35f));
            var title = hud.Txt("Titulo", panel.transform, C, C, new Vector2(0, 200), new Vector2(1000, 60), 40, TextAnchor.MiddleCenter, Color.white, true);
            title.text = "TROCAR DE PERSONAGEM";
            var hint = hud.Txt("Dica", panel.transform, C, C, new Vector2(0, -170), new Vector2(1000, 40), 24, TextAnchor.MiddleCenter, new Color(1, 1, 1, 0.8f));
            hint.text = "Mova o mouse para escolher e solte o Alt";
            cards = new Image[Profiles.Length];
            names = new Text[Profiles.Length];
            infos = new Text[Profiles.Length];
            for (int i = 0; i < Profiles.Length; i++)
            {
                float x = (i - (Profiles.Length - 1) / 2f) * 330f;
                cards[i] = hud.Img("Card" + i, panel.transform, C, C, new Vector2(x, 10), new Vector2(300, 220), new Color(0, 0, 0, 0.75f), Sprites.Rounded);
                hud.Img("Retrato", cards[i].transform, C, C, new Vector2(0, 40), new Vector2(110, 110), Profiles[i].Accent, Sprites.Circle);
                var letter = hud.Txt("Letra", cards[i].transform, C, C, new Vector2(0, 40), new Vector2(110, 110), 64, TextAnchor.MiddleCenter, Color.white, true);
                letter.text = Profiles[i].Name.Substring(0, 1);
                names[i] = hud.Txt("Nome", cards[i].transform, C, C, new Vector2(0, -42), new Vector2(280, 46), 34, TextAnchor.MiddleCenter, Color.white, true);
                infos[i] = hud.Txt("Info", cards[i].transform, C, C, new Vector2(0, -80), new Vector2(280, 34), 20, TextAnchor.MiddleCenter, new Color(1, 1, 1, 0.8f), false, false);
            }
            panel.SetActive(false);
        }

        void Update()
        {
            var gm = GameManager.I;
            if (gm == null || Profiles == null || Switching) return;

            if (!open && gm.Playing && Input.GetKeyDown(KeyCode.LeftAlt))
            {
                string reason = BlockReason();
                if (reason != null) { gm.Hud.Help(reason, 2.5f); return; }
                open = true;
                wheelX = 0f;
                selected = Active;
                panel.SetActive(true);
                Time.timeScale = 0.3f;
            }
            if (!open) return;

            wheelX = Mathf.Clamp(wheelX + Input.GetAxisRaw("Mouse X") * 6f, -60f, 60f);
            if (wheelX < -25f) selected = 0;
            if (wheelX > 25f) selected = Profiles.Length - 1;

            for (int i = 0; i < Profiles.Length; i++)
            {
                bool sel = i == selected;
                cards[i].color = sel ? new Color(1f, 1f, 1f, 0.92f) : new Color(0, 0, 0, 0.75f);
                names[i].color = sel ? Color.black : Color.white;
                infos[i].color = sel ? new Color(0, 0, 0, 0.7f) : new Color(1, 1, 1, 0.8f);
                names[i].text = Profiles[i].Name;
                int money = i == Active ? gm.Money : Profiles[i].Money;
                infos[i].text = i == Active ? $"${money:N0}  •  atual" : $"${money:N0}";
            }

            if (!Input.GetKey(KeyCode.LeftAlt))
            {
                open = false;
                panel.SetActive(false);
                if (gm.State == GameState.Playing) Time.timeScale = 1f;
                if (selected != Active) SwitchTo(selected);
            }
        }
    }
}

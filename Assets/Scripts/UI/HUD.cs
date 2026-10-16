using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DevCity
{
    public class MenuItem
    {
        public string Label;
        public string Right;
        public bool Disabled;
        public System.Action OnSelect;
    }

    /// <summary>
    /// Interface no estilo GTA V, montada por código (uGUI): radar + vida/colete (canto inferior esquerdo),
    /// arma/estrelas/dinheiro (superior direito), caixa de ajuda (superior esquerdo), feed de notificações,
    /// legenda de objetivo, nome do carro e do bairro, mira, WASTED/BUSTED, título, pausa, lojas e roda de armas.
    /// </summary>
    public class HUD : MonoBehaviour
    {
        public RectTransform Root { get; private set; }
        public Font Font { get; private set; }

        // elementos fixos
        Text weaponText, ammoText, moneyText, moneyPopup, objective, vehicleName, areaName, helpText, clockText;
        Image helpBox, crosshair, damage, fade, deathTint, healthBar, armorBar, healthBg, armorBg;
        Image[] stars;
        RectTransform starsRoot, feedRoot, scope;
        GameObject titleScreen, pauseScreen, deathScreen, bigRoot, menuRoot, wheelRoot, missionCard;
        Text deathTitle, deathReason, bigTitle, bigSub, missionTitle, missionGiver, menuTitle, menuSub;
        RectTransform menuList;

        float helpT, vehicleT, areaT, bigT, damageT, popupT, cardT, starPulse;
        string lastArea = "";
        int lastMoney = int.MinValue;

        // menu
        System.Func<List<MenuItem>> menuBuilder;
        List<MenuItem> menuItems = new List<MenuItem>();
        readonly List<(Image bg, Text label, Text right)> menuRows = new List<(Image, Text, Text)>();
        int menuIndex;

        // roda de armas
        readonly List<(Image bg, Text name, Text ammo)> wheelSlots = new List<(Image, Text, Text)>();
        Vector2 wheelVec;
        int wheelSel = -1;
        bool wheelOpen;

        static readonly Color Panel = new Color(0f, 0f, 0f, 0.72f);

        // ===================== fábrica de UI =====================

        public RectTransform Rect(string name, Transform parent, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return rt;
        }

        public RectTransform Stretch(string name, Transform parent)
        {
            var rt = Rect(name, parent, Vector2.zero, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            return rt;
        }

        public Image Img(string name, Transform parent, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size, Color c, Sprite s = null)
        {
            var rt = Rect(name, parent, anchor, pivot, pos, size);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = c;
            img.raycastTarget = false;
            if (s != null) { img.sprite = s; if (s.border != Vector4.zero) img.type = Image.Type.Sliced; }
            return img;
        }

        public Text Txt(string name, Transform parent, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size, int fontSize, TextAnchor align, Color c, bool bold = false, bool outline = true)
        {
            var rt = Rect(name, parent, anchor, pivot, pos, size);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = Font;
            t.fontSize = fontSize;
            t.alignment = align;
            t.color = c;
            t.supportRichText = true;
            t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            if (bold) t.fontStyle = FontStyle.Bold;
            if (outline)
            {
                var o = rt.gameObject.AddComponent<Outline>();
                o.effectColor = new Color(0, 0, 0, 0.85f);
                o.effectDistance = new Vector2(2f, -2f);
            }
            return t;
        }

        Button Btn(string label, Transform parent, Vector2 pos, System.Action onClick)
        {
            var img = Img("Botao_" + label, parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, new Vector2(420, 64), new Color(0.12f, 0.12f, 0.14f, 0.95f), Sprites.Rounded);
            img.raycastTarget = true;
            var b = img.gameObject.AddComponent<Button>();
            var colors = b.colors;
            colors.highlightedColor = new Color(1f, 0.85f, 0.3f);
            colors.pressedColor = new Color(1f, 0.7f, 0.2f);
            b.colors = colors;
            b.onClick.AddListener(() => onClick());
            Txt("Texto", img.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(400, 60), 30, TextAnchor.MiddleCenter, Color.white, true);
            return b;
        }

        // ===================== construção =====================

        public void Build()
        {
            Font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            var canvasGo = new GameObject("Canvas", typeof(RectTransform));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();
            Root = (RectTransform)canvasGo.transform;

            if (FindAnyObjectByType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem");
                es.AddComponent<EventSystem>();
                es.AddComponent<StandaloneInputModule>();
            }

            Vector2 TL = new Vector2(0, 1), TR = new Vector2(1, 1), BL = new Vector2(0, 0), BR = new Vector2(1, 0), C = new Vector2(0.5f, 0.5f), BC = new Vector2(0.5f, 0);

            // efeitos de tela (atrás de tudo)
            damage = Img("Dano", Root, C, C, Vector2.zero, Vector2.zero, new Color(0.8f, 0f, 0f, 0f));
            damage.rectTransform.anchorMin = Vector2.zero; damage.rectTransform.anchorMax = Vector2.one; damage.rectTransform.sizeDelta = Vector2.zero;
            BuildScope();

            // mira
            crosshair = Img("Mira", Root, C, C, Vector2.zero, new Vector2(7, 7), Color.white, Sprites.Circle);

            // vida e colete (embaixo do radar, como no GTA V)
            healthBg = Img("VidaFundo", Root, BL, BL, new Vector2(40, 22), new Vector2(166, 11), new Color(0.1f, 0.3f, 0.1f, 0.8f));
            healthBar = Img("Vida", healthBg.transform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), Vector2.zero, new Vector2(166, 11), new Color(0.35f, 0.75f, 0.35f));
            armorBg = Img("ColeteFundo", Root, BL, BL, new Vector2(210, 22), new Vector2(166, 11), new Color(0.1f, 0.2f, 0.35f, 0.8f));
            armorBar = Img("Colete", armorBg.transform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), Vector2.zero, new Vector2(166, 11), new Color(0.3f, 0.6f, 0.95f));

            // canto superior direito: arma, estrelas, dinheiro
            weaponText = Txt("Arma", Root, TR, TR, new Vector2(-40, -26), new Vector2(600, 36), 26, TextAnchor.UpperRight, Color.white, true);
            ammoText = Txt("Municao", Root, TR, TR, new Vector2(-40, -58), new Vector2(600, 34), 26, TextAnchor.UpperRight, Color.white);
            starsRoot = Rect("Estrelas", Root, TR, TR, new Vector2(-40, -98), new Vector2(5 * 44, 42));
            stars = new Image[5];
            for (int i = 0; i < 5; i++)
                stars[i] = Img("Estrela" + i, starsRoot, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-i * 44, 0), new Vector2(40, 40), new Color(1, 1, 1, 0.15f), Sprites.Star);
            moneyText = Txt("Dinheiro", Root, TR, TR, new Vector2(-40, -140), new Vector2(500, 50), 40, TextAnchor.UpperRight, new Color(0.45f, 0.9f, 0.45f), true);
            moneyPopup = Txt("DinheiroPopup", Root, TR, TR, new Vector2(-40, -186), new Vector2(500, 40), 30, TextAnchor.UpperRight, Color.white, true);
            clockText = Txt("Relogio", Root, TR, TR, new Vector2(-40, -224), new Vector2(300, 30), 22, TextAnchor.UpperRight, new Color(1, 1, 1, 0.7f));

            // ajuda (superior esquerdo)
            helpBox = Img("Ajuda", Root, TL, TL, new Vector2(40, -40), new Vector2(560, 90), Panel, Sprites.Rounded);
            helpText = Txt("Texto", helpBox.transform, TL, TL, new Vector2(18, -14), new Vector2(524, 200), 25, TextAnchor.UpperLeft, Color.white, false, false);
            helpBox.gameObject.SetActive(false);

            // notificações (acima do radar)
            feedRoot = Rect("Feed", Root, BL, BL, new Vector2(40, 270), new Vector2(560, 400));
            var vl = feedRoot.gameObject.AddComponent<VerticalLayoutGroup>();
            vl.childAlignment = TextAnchor.LowerLeft;
            vl.spacing = 8f;
            vl.childControlHeight = true;
            vl.childControlWidth = true;
            vl.childForceExpandHeight = false;

            // objetivo (legenda)
            objective = Txt("Objetivo", Root, BC, BC, new Vector2(0, 90), new Vector2(1300, 120), 31, TextAnchor.LowerCenter, Color.white);

            // carro e bairro (inferior direito)
            vehicleName = Txt("Veiculo", Root, BR, BR, new Vector2(-40, 82), new Vector2(700, 60), 46, TextAnchor.LowerRight, Color.white, true);
            areaName = Txt("Bairro", Root, BR, BR, new Vector2(-40, 36), new Vector2(700, 46), 34, TextAnchor.LowerRight, new Color(1f, 1f, 1f, 0.95f));

            // mensagens grandes
            bigRoot = Rect("Grande", Root, C, C, new Vector2(0, 80), new Vector2(1600, 300)).gameObject;
            var band = Img("Faixa", bigRoot.transform, C, C, Vector2.zero, new Vector2(2400, 170), new Color(0, 0, 0, 0.55f));
            bigTitle = Txt("Titulo", bigRoot.transform, C, C, new Vector2(0, 25), new Vector2(1600, 120), 92, TextAnchor.MiddleCenter, new Color(1f, 0.8f, 0.2f), true);
            bigSub = Txt("Sub", bigRoot.transform, C, C, new Vector2(0, -55), new Vector2(1600, 120), 34, TextAnchor.UpperCenter, Color.white);
            bigRoot.SetActive(false);

            missionCard = Rect("CartaoMissao", Root, C, C, new Vector2(0, 200), new Vector2(1600, 200)).gameObject;
            missionTitle = Txt("Titulo", missionCard.transform, C, C, new Vector2(0, 20), new Vector2(1600, 100), 78, TextAnchor.MiddleCenter, Color.white, true);
            missionGiver = Txt("Contato", missionCard.transform, C, C, new Vector2(0, -50), new Vector2(1600, 50), 32, TextAnchor.MiddleCenter, new Color(1f, 0.82f, 0.3f));
            missionCard.SetActive(false);

            BuildDeath();
            BuildMenu();
            BuildWheel();
            BuildPause();
            BuildTitle();

            fade = Img("Fade", Root, C, C, Vector2.zero, Vector2.zero, new Color(0, 0, 0, 0));
            fade.rectTransform.anchorMin = Vector2.zero; fade.rectTransform.anchorMax = Vector2.one; fade.rectTransform.sizeDelta = Vector2.zero;
        }

        void BuildScope()
        {
            scope = Stretch("Luneta", Root);
            var C = new Vector2(0.5f, 0.5f);
            var black = new Color(0, 0, 0, 1f);
            Img("Cima", scope, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(4000, 1080 / 2f - 380), black);
            Img("Baixo", scope, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(4000, 1080 / 2f - 380), black);
            Img("Esq", scope, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(1920 / 2f - 380 + 600, 2000), black);
            Img("Dir", scope, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), Vector2.zero, new Vector2(1920 / 2f - 380 + 600, 2000), black);
            Img("Anel", scope, C, C, Vector2.zero, new Vector2(780, 780), new Color(0, 0, 0, 0.9f), Sprites.Ring);
            Img("LinhaH", scope, C, C, Vector2.zero, new Vector2(760, 2), black);
            Img("LinhaV", scope, C, C, Vector2.zero, new Vector2(2, 760), black);
            scope.gameObject.SetActive(false);
        }

        void BuildDeath()
        {
            deathScreen = Stretch("Morte", Root).gameObject;
            deathTint = Img("Tinta", deathScreen.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(4000, 3000), new Color(0.25f, 0.25f, 0.25f, 0.55f));
            deathTitle = Txt("Titulo", deathScreen.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 30), new Vector2(1600, 200), 150, TextAnchor.MiddleCenter, new Color(0.85f, 0.1f, 0.1f), true);
            deathReason = Txt("Motivo", deathScreen.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -90), new Vector2(1600, 60), 34, TextAnchor.MiddleCenter, Color.white);
            deathScreen.SetActive(false);
        }

        void BuildTitle()
        {
            titleScreen = Stretch("Titulo", Root).gameObject;
            var C = new Vector2(0.5f, 0.5f);
            Img("Escurecer", titleScreen.transform, C, C, Vector2.zero, new Vector2(4000, 3000), new Color(0.02f, 0.02f, 0.05f, 0.45f));
            Img("Faixa", titleScreen.transform, C, C, new Vector2(0, 170), new Vector2(4000, 300), new Color(0, 0, 0, 0.5f));
            var logo = Txt("Logo", titleScreen.transform, C, C, new Vector2(-40, 190), new Vector2(1400, 220), 190, TextAnchor.MiddleCenter, Color.white, true);
            logo.text = "DEV CITY";
            var v = Txt("V", titleScreen.transform, C, C, new Vector2(480, 175), new Vector2(300, 260), 260, TextAnchor.MiddleCenter, new Color(0.35f, 0.85f, 0.45f), true);
            v.text = "V";
            var sub = Txt("Sub", titleScreen.transform, C, C, new Vector2(0, 60), new Vector2(1400, 50), 30, TextAnchor.MiddleCenter, new Color(1f, 0.85f, 0.35f));
            sub.text = "Um mundo aberto inspirado em Grand Theft Auto V";
            var ctrl = Txt("Controles", titleScreen.transform, C, C, new Vector2(0, -170), new Vector2(1500, 300), 25, TextAnchor.MiddleCenter, new Color(1, 1, 1, 0.9f));
            ctrl.text = ControlsText();
            var press = Txt("Enter", titleScreen.transform, C, C, new Vector2(0, -380), new Vector2(1400, 60), 38, TextAnchor.MiddleCenter, Color.white, true);
            press.text = "Pressione ENTER para jogar";
            press.gameObject.AddComponent<Blink>();
        }

        public static string ControlsText()
        {
            return "<b>WASD</b> andar/dirigir   <b>Mouse</b> câmera   <b>Shift</b> correr   <b>Espaço</b> pular / freio de mão   <b>F</b> entrar/sair do veículo\n" +
                   "<b>Botão direito</b> mirar   <b>Botão esquerdo</b> atirar   <b>R</b> recarregar   <b>Tab</b> (segure) roda de armas   <b>1-7 / rodinha</b> trocar arma\n" +
                   "<b>E</b> interagir / buzina   <b>H</b> faróis   <b>G</b> sirene (viatura)   <b>M</b> mapa (clique para marcar destino)   <b>ESC</b> pausa";
        }

        void BuildPause()
        {
            pauseScreen = Stretch("Pausa", Root).gameObject;
            var C = new Vector2(0.5f, 0.5f);
            Img("Fundo", pauseScreen.transform, C, C, Vector2.zero, new Vector2(4000, 3000), new Color(0, 0, 0, 0.7f));
            var t = Txt("Titulo", pauseScreen.transform, C, C, new Vector2(0, 330), new Vector2(1200, 120), 96, TextAnchor.MiddleCenter, Color.white, true);
            t.text = "PAUSADO";
            Btn("Continuar", pauseScreen.transform, new Vector2(0, 170), () => { ShowPause(false); GameManager.I.SetPlaying(); });
            Btn("Mapa", pauseScreen.transform, new Vector2(0, 90), () => { ShowPause(false); GameManager.I.State = GameState.Map; GameManager.I.Minimap.ShowBigMap(true); });
            Btn("Salvar", pauseScreen.transform, new Vector2(0, 10), () => { Notify(SaveSystem.Save() ? "Jogo salvo." : "Não foi possível salvar o jogo."); });
            Btn("Sair do jogo", pauseScreen.transform, new Vector2(0, -70), () => { SaveSystem.Save(); Application.Quit(); });
            var ctrl = Txt("Controles", pauseScreen.transform, C, C, new Vector2(0, -280), new Vector2(1700, 260), 24, TextAnchor.MiddleCenter, new Color(1, 1, 1, 0.85f));
            ctrl.text = ControlsText();
            pauseScreen.SetActive(false);
        }

        void BuildMenu()
        {
            menuRoot = Rect("Loja", Root, new Vector2(0, 1), new Vector2(0, 1), new Vector2(60, -60), new Vector2(620, 900)).gameObject;
            var header = Img("Cabecalho", menuRoot.transform, new Vector2(0, 1), new Vector2(0, 1), Vector2.zero, new Vector2(620, 120), new Color(0.75f, 0.1f, 0.1f, 0.95f));
            menuTitle = Txt("Titulo", header.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 8), new Vector2(600, 80), 44, TextAnchor.MiddleCenter, Color.white, true);
            menuSub = Txt("Sub", menuRoot.transform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, -120), new Vector2(620, 44), 22, TextAnchor.MiddleLeft, new Color(0.7f, 0.85f, 1f), false, false);
            Img("SubFundo", menuRoot.transform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, -120), new Vector2(620, 44), new Color(0, 0, 0, 0.9f)).transform.SetAsFirstSibling();
            menuList = Rect("Lista", menuRoot.transform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, -164), new Vector2(620, 700));
            var hint = Txt("Dica", menuRoot.transform, new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, -10), new Vector2(620, 40), 20, TextAnchor.MiddleLeft, new Color(1, 1, 1, 0.8f));
            hint.text = "↑↓ / W S escolher   Enter comprar   Esc sair";
            menuRoot.SetActive(false);
        }

        void BuildWheel()
        {
            wheelRoot = Stretch("RodaDeArmas", Root).gameObject;
            var C = new Vector2(0.5f, 0.5f);
            Img("Escurecer", wheelRoot.transform, C, C, Vector2.zero, new Vector2(4000, 3000), new Color(0, 0, 0, 0.35f));
            Img("Centro", wheelRoot.transform, C, C, Vector2.zero, new Vector2(150, 150), new Color(0, 0, 0, 0.7f), Sprites.Circle);
            for (int i = 0; i < 8; i++)
            {
                float a = (90f - i * 45f) * Mathf.Deg2Rad;
                Vector2 p = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 250f;
                var bg = Img("Slot" + i, wheelRoot.transform, C, C, p, new Vector2(230, 120), new Color(0, 0, 0, 0.75f), Sprites.Rounded);
                var name = Txt("Nome", bg.transform, C, C, new Vector2(0, 14), new Vector2(220, 70), 22, TextAnchor.MiddleCenter, Color.white, true, false);
                var ammo = Txt("Mun", bg.transform, C, C, new Vector2(0, -34), new Vector2(220, 30), 20, TextAnchor.MiddleCenter, new Color(1, 1, 1, 0.75f), false, false);
                wheelSlots.Add((bg, name, ammo));
            }
            wheelRoot.SetActive(false);
        }

        // ===================== API usada pelo jogo =====================

        public void ShowTitle(bool on) { titleScreen.SetActive(on); }

        public void ShowPause(bool on) { pauseScreen.SetActive(on); }

        public void Help(string text, float duration)
        {
            helpText.text = text;
            helpBox.gameObject.SetActive(true);
            helpBox.rectTransform.sizeDelta = new Vector2(560, Mathf.Max(64f, helpText.preferredHeight + 30f));
            helpT = duration;
        }

        public void Notify(string text)
        {
            var box = Img("Notificacao", feedRoot, new Vector2(0, 0), new Vector2(0, 0), Vector2.zero, new Vector2(560, 70), Panel, Sprites.Rounded);
            var le = box.gameObject.AddComponent<LayoutElement>();
            var t = Txt("Texto", box.transform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(16, 0), new Vector2(528, 200), 23, TextAnchor.MiddleLeft, Color.white, false, false);
            t.text = text;
            le.preferredHeight = Mathf.Max(56f, t.preferredHeight + 22f);
            le.preferredWidth = 560f;
            while (feedRoot.childCount > 4) DestroyImmediate(feedRoot.GetChild(0).gameObject);
            StartCoroutine(FadeOutAndDestroy(box, 7f));
        }

        IEnumerator FadeOutAndDestroy(Image box, float after)
        {
            yield return new WaitForSecondsRealtime(after);
            if (box == null) yield break;
            var cg = box.gameObject.AddComponent<CanvasGroup>();
            for (float t = 0; t < 0.6f; t += Time.unscaledDeltaTime) { if (box == null) yield break; cg.alpha = 1f - t / 0.6f; yield return null; }
            if (box != null) Destroy(box.gameObject);
        }

        public void Objective(string text) { objective.text = text ?? ""; }

        public void ShowVehicleName(string name)
        {
            vehicleName.text = name ?? "";
            vehicleT = name != null ? 4f : 0f;
            if (name != null) { lastArea = ""; }
        }

        public void MoneyPopup(int amount)
        {
            if (amount == 0) return;
            moneyPopup.text = amount > 0 ? $"<color=#9cff9c>+${amount:N0}</color>" : $"<color=#ff8a8a>-${-amount:N0}</color>";
            popupT = 2.5f;
        }

        public void FlashStars() { starPulse = 1f; }

        public void DamageFlash() { damageT = 0.35f; }

        public void BigMessage(string title, string sub, Color color, float duration)
        {
            bigTitle.text = title;
            bigTitle.color = color;
            bigSub.text = sub;
            bigRoot.SetActive(true);
            bigT = duration;
        }

        public void MissionTitle(string title, string giver)
        {
            missionTitle.text = title;
            missionGiver.text = giver;
            missionCard.SetActive(true);
            cardT = 3.5f;
        }

        public void ShowDeath(string title, string reason)
        {
            deathTitle.text = title;
            deathTitle.color = title == "BUSTED" ? Color.white : new Color(0.85f, 0.1f, 0.1f);
            deathReason.text = reason;
            deathScreen.SetActive(true);
            objective.text = "";
        }

        public void HideDeath() { deathScreen.SetActive(false); }

        public IEnumerator FadeRoutine(float to, float time)
        {
            float from = fade.color.a;
            for (float t = 0; t < time; t += Time.unscaledDeltaTime)
            {
                fade.color = new Color(0, 0, 0, Mathf.Lerp(from, to, t / time));
                yield return null;
            }
            fade.color = new Color(0, 0, 0, to);
        }

        // ----- menu de loja -----

        public void OpenMenu(string title, string subtitle, System.Func<List<MenuItem>> builder)
        {
            var gm = GameManager.I;
            menuBuilder = builder;
            menuTitle.text = title;
            menuSub.text = "  " + subtitle;
            menuIndex = 0;
            menuRoot.SetActive(true);
            RebuildMenu();
            gm.Pause(GameState.Shop);
        }

        public void CloseMenu()
        {
            if (!menuRoot.activeSelf) return;
            menuRoot.SetActive(false);
            if (GameManager.I.State == GameState.Shop) GameManager.I.SetPlaying();
        }

        void RebuildMenu()
        {
            menuItems = menuBuilder();
            foreach (var r in menuRows) Destroy(r.bg.gameObject);
            menuRows.Clear();
            for (int i = 0; i < menuItems.Count; i++)
            {
                int idx = i;
                var bg = Img("Item" + i, menuList, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, -i * 52), new Vector2(620, 50), new Color(0, 0, 0, 0.8f));
                bg.raycastTarget = true;
                var b = bg.gameObject.AddComponent<Button>();
                b.transition = Selectable.Transition.None;
                b.onClick.AddListener(() => { menuIndex = idx; Activate(); });
                var label = Txt("Nome", bg.transform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(16, 0), new Vector2(440, 46), 24, TextAnchor.MiddleLeft, Color.white, false, false);
                var right = Txt("Preco", bg.transform, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-16, 0), new Vector2(160, 46), 24, TextAnchor.MiddleRight, Color.white, true, false);
                label.text = menuItems[i].Label;
                right.text = menuItems[i].Right;
                menuRows.Add((bg, label, right));
            }
            menuIndex = Mathf.Clamp(menuIndex, 0, Mathf.Max(0, menuItems.Count - 1));
        }

        void Activate()
        {
            if (menuIndex < 0 || menuIndex >= menuItems.Count) return;
            var it = menuItems[menuIndex];
            if (it.Disabled) { Sfx.Play2D(Sfx.Click, 0.5f); return; }
            it.OnSelect?.Invoke();
            if (menuRoot.activeSelf) RebuildMenu();
        }

        void MenuInput()
        {
            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Backspace)) { CloseMenu(); return; }
            if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S)) menuIndex = (menuIndex + 1) % Mathf.Max(1, menuItems.Count);
            if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W)) menuIndex = (menuIndex - 1 + menuItems.Count) % Mathf.Max(1, menuItems.Count);
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.E)) Activate();
            for (int i = 0; i < menuRows.Count; i++)
            {
                bool sel = i == menuIndex;
                bool dis = menuItems[i].Disabled;
                menuRows[i].bg.color = sel ? new Color(0.95f, 0.95f, 0.95f, 0.95f) : new Color(0, 0, 0, 0.8f);
                Color tc = sel ? Color.black : (dis ? new Color(1, 1, 1, 0.4f) : Color.white);
                menuRows[i].label.color = tc;
                menuRows[i].right.color = tc;
            }
        }

        // ===================== atualização =====================

        void Update()
        {
            var gm = GameManager.I;
            if (gm == null || gm.Player == null) return;
            float dt = Time.unscaledDeltaTime;
            var p = gm.Player;
            bool inGame = gm.State != GameState.Title;

            if (menuRoot.activeSelf) MenuInput();

            // ajuda
            if (helpT > 0f) { helpT -= dt; if (helpT <= 0f) helpBox.gameObject.SetActive(false); }

            // vida/colete
            healthBar.rectTransform.sizeDelta = new Vector2(166f * Mathf.Clamp01(p.Health / PlayerController.MaxHealth), 11f);
            healthBar.color = p.Health < PlayerController.MaxHealth * 0.25f ? new Color(0.85f, 0.25f, 0.2f) : new Color(0.35f, 0.75f, 0.35f);
            armorBar.rectTransform.sizeDelta = new Vector2(166f * Mathf.Clamp01(p.Armor / PlayerController.MaxArmor), 11f);
            healthBg.gameObject.SetActive(inGame);
            armorBg.gameObject.SetActive(inGame);

            // arma
            var w = p.Weapons;
            weaponText.text = inGame ? w.Def.Name : "";
            ammoText.text = inGame ? w.AmmoText() : "";

            // dinheiro
            if (gm.Money != lastMoney) { lastMoney = gm.Money; moneyText.text = $"${gm.Money:N0}"; }
            moneyText.gameObject.SetActive(inGame);
            if (popupT > 0f) { popupT -= dt; if (popupT <= 0f) moneyPopup.text = ""; }
            clockText.text = inGame ? gm.DayNight.Clock : "";

            // estrelas (piscam quando a polícia perdeu você de vista)
            var wanted = gm.Wanted;
            starPulse = Mathf.Max(0f, starPulse - dt * 1.5f);
            bool blink = wanted.Searching && Mathf.Repeat(Time.unscaledTime * 2.2f, 1f) < 0.5f;
            for (int i = 0; i < 5; i++)
            {
                bool on = i < wanted.Stars;
                stars[i].color = on ? (blink ? new Color(1f, 1f, 1f, 0.35f) : Color.white) : new Color(1f, 1f, 1f, wanted.Stars > 0 ? 0.18f : 0f);
                stars[i].rectTransform.localScale = Vector3.one * (1f + (on ? starPulse * 0.4f : 0f));
            }

            // mira e luneta
            bool aiming = w.Aiming && gm.Playing;
            bool sniper = aiming && w.Def.Zoom > 0f && w.Def.Zoom < 20f && p.Vehicle == null;
            scope.gameObject.SetActive(sniper);
            crosshair.gameObject.SetActive(gm.Playing && (aiming || (!w.Def.Melee && p.Vehicle == null)) && !sniper);
            crosshair.color = w.TargetUnderCrosshair ? new Color(1f, 0.2f, 0.2f) : Color.white;
            crosshair.rectTransform.sizeDelta = aiming ? new Vector2(8, 8) : new Vector2(5, 5);

            // dano
            if (damageT > 0f) damageT -= dt;
            float lowHealth = p.Health < PlayerController.MaxHealth * 0.25f && !p.IsDead ? 0.12f + Mathf.Sin(Time.unscaledTime * 4f) * 0.05f : 0f;
            damage.color = new Color(0.7f, 0f, 0f, Mathf.Max(damageT, lowHealth));

            // nome do carro / bairro
            if (vehicleT > 0f) { vehicleT -= dt; if (vehicleT <= 0f) vehicleName.text = ""; }
            if (gm.Playing)
            {
                string area = gm.City.AreaName(p.Position);
                if (area != lastArea) { lastArea = area; areaName.text = area; areaT = 5f; }
            }
            if (areaT > 0f) { areaT -= dt; areaName.color = new Color(1, 1, 1, Mathf.Clamp01(areaT)); }

            if (bigT > 0f) { bigT -= dt; if (bigT <= 0f) bigRoot.SetActive(false); }
            if (cardT > 0f) { cardT -= dt; if (cardT <= 0f) missionCard.SetActive(false); }

            WeaponWheel(gm);
        }

        // ===================== roda de armas =====================

        void WeaponWheel(GameManager gm)
        {
            bool want = Input.GetKey(KeyCode.Tab) && (gm.Playing || wheelOpen) && !gm.Player.IsDead;
            var w = gm.Player.Weapons;
            if (want && !wheelOpen)
            {
                wheelOpen = true;
                wheelVec = Vector2.zero;
                wheelSel = w.Current;
                wheelRoot.SetActive(true);
                Time.timeScale = 0.25f; // câmera lenta, como no GTA V
            }
            if (!want && wheelOpen)
            {
                wheelOpen = false;
                wheelRoot.SetActive(false);
                if (gm.State == GameState.Playing) Time.timeScale = 1f;
                if (wheelSel >= 0) w.Select(wheelSel);
                return;
            }
            if (!wheelOpen) return;

            wheelVec += new Vector2(Input.GetAxisRaw("Mouse X"), Input.GetAxisRaw("Mouse Y")) * 6f;
            if (wheelVec.magnitude > 60f)
            {
                wheelVec = wheelVec.normalized * 60f;
                float ang = Mathf.Atan2(wheelVec.x, wheelVec.y) * Mathf.Rad2Deg; // 0 = cima, horário
                int slot = Mathf.RoundToInt(Mathf.Repeat(ang, 360f) / 45f) % 8;
                if (slot < w.Owned.Length && w.Owned[slot]) wheelSel = slot;
            }
            for (int i = 0; i < 8; i++)
            {
                var s = wheelSlots[i];
                bool exists = i < Weapons.All.Length;
                bool owned = exists && w.Owned[i];
                s.name.text = exists ? Weapons.All[i].Name : "—";
                s.ammo.text = owned && !Weapons.All[i].Melee ? (Weapons.All[i].Thrown ? $"x{w.Clip[i]}" : $"{w.Clip[i] + w.Ammo[i]}") : "";
                s.bg.color = i == wheelSel ? new Color(1f, 1f, 1f, 0.9f) : new Color(0, 0, 0, owned ? 0.75f : 0.35f);
                s.name.color = i == wheelSel ? Color.black : (owned ? Color.white : new Color(1, 1, 1, 0.3f));
                s.ammo.color = i == wheelSel ? new Color(0, 0, 0, 0.7f) : new Color(1, 1, 1, 0.7f);
            }
        }
    }

    /// <summary>Faz um texto piscar devagar.</summary>
    public class Blink : MonoBehaviour
    {
        Text t;
        void Awake() { t = GetComponent<Text>(); }
        void Update()
        {
            var c = t.color;
            c.a = 0.45f + 0.55f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 2.2f));
            t.color = c;
        }
    }
}

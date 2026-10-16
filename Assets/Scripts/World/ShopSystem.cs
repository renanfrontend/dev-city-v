using System.Collections.Generic;
using UnityEngine;

namespace DevCity
{
    /// <summary>
    /// Lojas e serviços: arsenal (armas/munição/colete), roupas, mercadinho (lanche e assalto),
    /// Pinta & Foge (repinta, conserta e some com as estrelas se a polícia não estiver vendo) e a casa (salvar/dormir).
    /// </summary>
    public class ShopSystem : MonoBehaviour
    {
        public System.Action OnStoreRobbed;
        public System.Action OnRobberyCashCollected;

        Ped clerk;
        float robT, robCooldownUntil = -1f, sprayCooldown;
        bool robbing;
        readonly List<Marker> markers = new List<Marker>();

        static readonly (string name, Color shirt, Color pants, int price)[] outfits =
        {
            ("Original", new Color(0.92f, 0.92f, 0.9f), new Color(0.22f, 0.28f, 0.42f), 0),
            ("Esportivo", new Color(0.1f, 0.45f, 0.85f), new Color(0.1f, 0.1f, 0.12f), 180),
            ("Praia del Código", new Color(0.95f, 0.55f, 0.2f), new Color(0.85f, 0.8f, 0.65f), 120),
            ("Social", new Color(0.12f, 0.12f, 0.15f), new Color(0.12f, 0.12f, 0.15f), 650),
            ("Motoqueiro", new Color(0.25f, 0.2f, 0.15f), new Color(0.15f, 0.15f, 0.25f), 420),
            ("Verde Gangue", new Color(0.15f, 0.55f, 0.2f), new Color(0.2f, 0.2f, 0.2f), 260),
        };

        public void Init()
        {
            var city = GameManager.I.City;
            foreach (var t in new[] { PlaceType.GunShop, PlaceType.Store, PlaceType.Clothes, PlaceType.Safehouse })
                markers.Add(Marker.Corona(city.Places[t].Door, new Color(1f, 0.82f, 0.2f), 0.9f));
            markers.Add(Marker.Corona(city.Places[PlaceType.PaySpray].Inside + Vector3.up * 0.05f, new Color(0.3f, 0.6f, 1f), 3f));
            SpawnClerk();
        }

        void SpawnClerk()
        {
            var store = GameManager.I.City.Places[PlaceType.Store];
            clerk = Ped.Spawn(store.Inside, PedRole.Clerk);
            clerk.transform.rotation = Quaternion.LookRotation(Vector3.back);
        }

        public bool StoreRobbedRecently => Time.time < robCooldownUntil;
        public void ResetRobbery() { robCooldownUntil = -1f; }

        void Update()
        {
            var gm = GameManager.I;
            if (gm == null || !gm.Playing) return;
            var player = gm.Player;
            var city = gm.City;
            if (clerk == null || (clerk.IsDead && Time.time - clerk.DeadTime > 60f)) { if (clerk != null) Destroy(clerk.gameObject); SpawnClerk(); }

            bool onFoot = player.Vehicle == null && !player.Busy;
            Vector3 p = player.Position;

            // ----- portas das lojas -----
            if (onFoot)
            {
                if (Near(p, city.Places[PlaceType.GunShop].Door)) Prompt("Aperte <b>E</b> para entrar no <b>Arsenal Bala Perdida</b>.", OpenGunShop);
                else if (Near(p, city.Places[PlaceType.Clothes].Door)) Prompt("Aperte <b>E</b> para entrar na <b>Moda Marginal</b>.", OpenClothes);
                else if (Near(p, city.Places[PlaceType.Store].Door) && !robbing) Prompt("Aperte <b>E</b> para comprar no <b>Mercadinho 24h</b>.", OpenStore);
                else if (Near(p, city.Places[PlaceType.Safehouse].Door)) Prompt("Aperte <b>E</b> para entrar em <b>casa</b> (salvar / dormir).", OpenSafehouse);
            }

            // ----- Pinta & Foge -----
            sprayCooldown -= Time.deltaTime;
            var spray = city.Places[PlaceType.PaySpray];
            if (player.Vehicle != null && sprayCooldown <= 0f && Gen.FlatDist(p, spray.Inside) < 3.5f && player.Vehicle.Speed < 4f)
            {
                sprayCooldown = 8f;
                StartCoroutine(Respray(player.Vehicle));
            }

            Robbery(gm, player);
        }

        static bool Near(Vector3 p, Vector3 door) => Gen.FlatDist(p, door) < 1.8f && Mathf.Abs(p.y - door.y) < 2f;

        void Prompt(string text, System.Action open)
        {
            GameManager.I.Hud.Help(text, 0.15f);
            if (Input.GetKeyDown(KeyCode.E)) open();
        }

        // ===================== Pinta & Foge =====================

        System.Collections.IEnumerator Respray(VehicleController v)
        {
            var gm = GameManager.I;
            if (gm.Wanted.Stars > 0 && gm.Wanted.Seen)
            {
                gm.Hud.Help("A polícia está vendo você! Despiste-a antes de entrar no <b>Pinta & Foge</b>.", 3f);
                yield break;
            }
            if (!gm.Spend(150))
            {
                gm.Hud.Help("O serviço custa <b>$150</b>.", 3f);
                yield break;
            }
            v.SetInput(0f, 0f, true);
            yield return gm.Hud.FadeRoutine(1f, 0.4f);
            VehicleFactory.Repaint(v, VehicleFactory.RandomPaint());
            v.Health = v.Spec.Health;
            bool hadStars = gm.Wanted.Stars > 0;
            gm.Wanted.Clear();
            gm.Police.StandDown();
            Sfx.Play2D(Sfx.Cash, 0.7f);
            yield return new WaitForSeconds(0.6f);
            yield return gm.Hud.FadeRoutine(0f, 0.5f);
            gm.Hud.Notify(hadStars ? "Carro pintado e consertado. <b>A polícia perdeu a sua pista.</b>" : "Carro pintado e consertado.");
        }

        // ===================== assalto ao mercadinho =====================

        void Robbery(GameManager gm, PlayerController player)
        {
            if (clerk == null || clerk.IsDead || StoreRobbedRecently) { robbing = false; return; }
            var cam = gm.Cam.transform;
            Vector3 to = clerk.Chest - cam.position;
            bool aimedAt = player.Weapons.Aiming && player.Vehicle == null && to.magnitude < 16f && Vector3.Angle(cam.forward, to) < 9f;
            if (aimedAt)
            {
                if (!robbing) { robbing = true; robT = 0f; }
                clerk.HandsUp(4f);
                robT += Time.deltaTime;
                gm.Hud.Help("Continue apontando para o atendente...", 0.15f);
                if (robT > 2.6f) CompleteRobbery(gm);
            }
            else if (robbing)
            {
                robT = Mathf.Max(0f, robT - Time.deltaTime * 0.5f);
            }
        }

        void CompleteRobbery(GameManager gm)
        {
            robbing = false;
            robCooldownUntil = Time.time + 240f;
            var store = gm.City.Places[PlaceType.Store];
            int cash = Random.Range(350, 950);
            var pick = Pickup.Create(store.Inside + new Vector3(1.2f, 1.1f, -1.2f), new Color(0.3f, 1f, 0.4f), cash, "maleta");
            pick.Radius = 2.2f;
            pick.OnCollect = () => OnRobberyCashCollected?.Invoke();
            clerk.State = PedState.Cower;
            // o atendente aperta o alarme / liga para a polícia
            StartCoroutine(Alarm(store.Door));
            OnStoreRobbed?.Invoke();
            gm.Hud.Help("O atendente colocou o dinheiro no balcão. <b>Pegue e fuja!</b>", 3f);
        }

        System.Collections.IEnumerator Alarm(Vector3 pos)
        {
            yield return new WaitForSeconds(3.5f);
            if (clerk != null && !clerk.IsDead) GameManager.I.Wanted.SetStars(Mathf.Max(GameManager.I.Wanted.Stars, 2), pos);
            GameManager.I.Hud.Notify("O alarme do mercadinho disparou!");
        }

        // ===================== menus =====================

        void OpenGunShop()
        {
            var gm = GameManager.I;
            gm.Hud.OpenMenu("ARSENAL BALA PERDIDA", "Armas, munição e proteção", () =>
            {
                var w = gm.Player.Weapons;
                var items = new List<MenuItem>();
                for (int i = 1; i < Weapons.All.Length; i++)
                {
                    var def = Weapons.All[i];
                    bool owned = w.Owned[i] && !def.Thrown;
                    if (!owned)
                        items.Add(new MenuItem { Label = def.Name, Right = $"${def.Price:N0}", OnSelect = () => { if (gm.Spend(def.Price)) { w.Give(def, def.StartAmmo); Sfx.Play2D(Sfx.Cash, 0.6f); } else Sfx.Play2D(Sfx.Click); } });
                    else
                        items.Add(new MenuItem { Label = $"Munição — {def.Name} (+{def.AmmoPack})", Right = $"${def.AmmoPrice:N0}", OnSelect = () => { if (gm.Spend(def.AmmoPrice)) { w.Give(def, def.AmmoPack); Sfx.Play2D(Sfx.Reload, 0.6f); } else Sfx.Play2D(Sfx.Click); } });
                }
                items.Add(new MenuItem
                {
                    Label = "Colete à prova de balas",
                    Right = gm.Player.Armor >= PlayerController.MaxArmor ? "cheio" : "$500",
                    Disabled = gm.Player.Armor >= PlayerController.MaxArmor,
                    OnSelect = () => { if (gm.Spend(500)) { gm.Player.Armor = PlayerController.MaxArmor; Sfx.Play2D(Sfx.Pickup); } }
                });
                return items;
            });
        }

        void OpenClothes()
        {
            var gm = GameManager.I;
            gm.Hud.OpenMenu("MODA MARGINAL", "Roupa nova, vida nova", () =>
            {
                var items = new List<MenuItem>();
                foreach (var o in outfits)
                {
                    var oo = o;
                    bool wearing = gm.Player.Outfit.Shirt == oo.shirt && gm.Player.Outfit.Pants == oo.pants;
                    items.Add(new MenuItem
                    {
                        Label = oo.name,
                        Right = wearing ? "vestindo" : oo.price == 0 ? "grátis" : $"${oo.price}",
                        Disabled = wearing,
                        OnSelect = () =>
                        {
                            if (!gm.Spend(oo.price)) { Sfx.Play2D(Sfx.Click); return; }
                            var outfit = gm.Player.Outfit;
                            outfit.Shirt = oo.shirt; outfit.Pants = oo.pants;
                            gm.Player.SetOutfit(outfit);
                            Sfx.Play2D(Sfx.Pickup);
                        }
                    });
                }
                return items;
            });
        }

        void OpenStore()
        {
            var gm = GameManager.I;
            gm.Hud.OpenMenu("MERCADINHO 24H", "Dica: dá para assaltar apontando a arma para o atendente.", () => new List<MenuItem>
            {
                new MenuItem { Label = "Salgadinho Crocante (+60 vida)", Right = "$15", OnSelect = () => { if (gm.Spend(15)) { gm.Player.Health = Mathf.Min(PlayerController.MaxHealth, gm.Player.Health + 60f); Sfx.Play2D(Sfx.Pickup); } } },
                new MenuItem { Label = "Energético Turbo (estamina cheia)", Right = "$8", OnSelect = () => { if (gm.Spend(8)) { gm.Player.Stamina = 100f; Sfx.Play2D(Sfx.Pickup); } } },
                new MenuItem { Label = "Kit de primeiros socorros (vida cheia)", Right = "$90", OnSelect = () => { if (gm.Spend(90)) { gm.Player.Health = PlayerController.MaxHealth; Sfx.Play2D(Sfx.Pickup); } } },
            });
        }

        void OpenSafehouse()
        {
            var gm = GameManager.I;
            gm.Hud.OpenMenu("CASA", "Seu canto em Dev City", () => new List<MenuItem>
            {
                new MenuItem { Label = "Salvar o jogo", Right = "", OnSelect = () => { bool saved = SaveSystem.Save(); gm.Hud.CloseMenu(); gm.Hud.Notify(saved ? "Jogo salvo." : "Não foi possível salvar o jogo."); } },
                new MenuItem
                {
                    Label = "Dormir 6 horas (salva e recupera a vida)", Right = gm.Wanted.Stars >= 3 ? "procurado demais" : "",
                    Disabled = gm.Wanted.Stars >= 3,
                    OnSelect = () => { gm.Hud.CloseMenu(); StartCoroutine(Sleep()); }
                },
            });
        }

        System.Collections.IEnumerator Sleep()
        {
            var gm = GameManager.I;
            gm.Pause(GameState.Cutscene);
            yield return gm.Hud.FadeRoutine(1f, 0.6f);
            gm.DayNight.Advance(6f);
            gm.Player.Health = PlayerController.MaxHealth;
            gm.Wanted.Clear();
            gm.Police.DespawnAll();
            SaveSystem.Save();
            yield return new WaitForSecondsRealtime(0.8f);
            gm.SetPlaying();
            yield return gm.Hud.FadeRoutine(0f, 0.6f);
            gm.Hud.Notify($"Você dormiu. São <b>{gm.DayNight.Clock}</b>. Jogo salvo.");
        }
    }
}

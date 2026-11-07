using System.Collections.Generic;
using System.Linq;
using Oxide.Game.Rust.Cui;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Scrap Shop", "Uncle Moe", "1.0.0")]
    [Description("/shop opens a click-to-buy shop window: weapons, ammo, armor, meds, resources, base parts and raid gear, paid with scrap")]
    class ScrapShop : RustPlugin
    {
        const string Ui = "ScrapShopUI";

        class Offer
        {
            public string Item;
            public int Amount, Price;
            public Offer(string item, int amount, int price) { Item = item; Amount = amount; Price = price; }
        }

        // Prices in scrap, set for a 10x server where loot gives 3x scrap
        static readonly KeyValuePair<string, Offer[]>[] Categories =
        {
            new KeyValuePair<string, Offer[]>("Weapons", new[]
            {
                new Offer("rifle.ak", 1, 500), new Offer("rifle.lr300", 1, 450), new Offer("rifle.bolt", 1, 400), new Offer("smg.mp5", 1, 250),
                new Offer("smg.thompson", 1, 200), new Offer("rifle.semiauto", 1, 150), new Offer("pistol.python", 1, 150), new Offer("shotgun.pump", 1, 120),
                new Offer("pistol.semiauto", 1, 60), new Offer("crossbow", 1, 40), new Offer("weapon.mod.holosight", 1, 80), new Offer("weapon.mod.8x.scope", 1, 150),
            }),
            new KeyValuePair<string, Offer[]>("Ammo", new[]
            {
                new Offer("ammo.rifle", 128, 40), new Offer("ammo.rifle.hv", 64, 50), new Offer("ammo.pistol", 128, 25), new Offer("ammo.pistol.hv", 64, 30),
                new Offer("ammo.shotgun", 64, 25), new Offer("ammo.shotgun.slug", 32, 25), new Offer("arrow.wooden", 64, 10), new Offer("arrow.hv", 32, 15),
            }),
            new KeyValuePair<string, Offer[]>("Armor", new[]
            {
                new Offer("metal.facemask", 1, 150), new Offer("metal.plate.torso", 1, 150), new Offer("roadsign.kilt", 1, 60), new Offer("roadsign.jacket", 1, 60),
                new Offer("coffeecan.helmet", 1, 40), new Offer("hoodie", 1, 15), new Offer("pants", 1, 15), new Offer("shoes.boots", 1, 15),
                new Offer("roadsign.gloves", 1, 20), new Offer("hazmatsuit", 1, 60),
            }),
            new KeyValuePair<string, Offer[]>("Meds", new[]
            {
                new Offer("syringe.medical", 5, 30), new Offer("largemedkit", 2, 30), new Offer("bandage", 5, 10), new Offer("antiradpills", 5, 10),
            }),
            new KeyValuePair<string, Offer[]>("Resources", new[]
            {
                new Offer("wood", 10000, 50), new Offer("stones", 10000, 50), new Offer("metal.fragments", 5000, 100), new Offer("metal.refined", 200, 120),
                new Offer("sulfur", 5000, 200), new Offer("charcoal", 5000, 50), new Offer("gunpowder", 1000, 150), new Offer("lowgradefuel", 500, 40),
                new Offer("cloth", 1000, 40), new Offer("leather", 500, 30),
            }),
            new KeyValuePair<string, Offer[]>("Base", new[]
            {
                new Offer("door.double.hinged.toptier", 1, 200), new Offer("door.hinged.toptier", 1, 150), new Offer("wall.frame.garagedoor", 1, 80), new Offer("lock.code", 5, 30),
                new Offer("cupboard.tool", 1, 20), new Offer("box.wooden.large", 2, 20), new Offer("autoturret", 1, 300), new Offer("furnace.large", 1, 100),
                new Offer("workbench3", 1, 500), new Offer("jackhammer", 1, 100), new Offer("chainsaw", 1, 100), new Offer("wall.external.high.stone", 2, 60),
            }),
            new KeyValuePair<string, Offer[]>("Raid", new[]
            {
                new Offer("explosive.timed", 1, 600), new Offer("ammo.rocket.basic", 1, 300), new Offer("rocket.launcher", 1, 300), new Offer("explosive.satchel", 2, 200),
                new Offer("grenade.beancan", 5, 80), new Offer("ammo.rifle.explosive", 64, 150),
            }),
        };

        int ScrapId => ItemManager.FindItemDefinition("scrap").itemid;

        void Unload()
        {
            foreach (BasePlayer player in BasePlayer.activePlayerList) CuiHelper.DestroyUi(player, Ui);
        }

        [ChatCommand("shop")]
        void CmdShop(BasePlayer player, string command, string[] args) => Open(player, 0);

        [ConsoleCommand("scrapshop.cat")]
        void CcCategory(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Player();
            if (player != null) Open(player, Mathf.Clamp(arg.GetInt(0), 0, Categories.Length - 1));
        }

        [ConsoleCommand("scrapshop.close")]
        void CcClose(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Player();
            if (player != null) CuiHelper.DestroyUi(player, Ui);
        }

        [ConsoleCommand("scrapshop.buy")]
        void CcBuy(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Player();
            if (player == null) return;
            int cat = arg.GetInt(0), index = arg.GetInt(1);
            if (cat < 0 || cat >= Categories.Length || index < 0 || index >= Categories[cat].Value.Length) return;
            Offer offer = Categories[cat].Value[index];
            ItemDefinition def = ItemManager.FindItemDefinition(offer.Item);
            if (def == null) return;

            if (player.inventory.GetAmount(ScrapId) < offer.Price)
                SendReply(player, $"You need <color=#ffcc00>{offer.Price} scrap</color> for {def.displayName.english}. Loot barrels and crates for scrap (3x here).");
            else
            {
                player.inventory.Take(null, ScrapId, offer.Price);
                Item item = ItemManager.Create(def, offer.Amount);
                player.GiveItem(item);
                SendReply(player, $"Bought <color=#9fd0ff>{offer.Amount} x {def.displayName.english}</color> for {offer.Price} scrap.");
            }
            Open(player, cat);
        }

        void Open(BasePlayer player, int cat)
        {
            CuiHelper.DestroyUi(player, Ui);
            var ui = new CuiElementContainer();
            ui.Add(new CuiPanel
            {
                Image = { Color = "0.08 0.07 0.06 0.96" },
                RectTransform = { AnchorMin = "0.18 0.14", AnchorMax = "0.82 0.88" },
                CursorEnabled = true,
            }, "Overlay", Ui);

            ui.Add(new CuiLabel
            {
                Text = { Text = $"<color=#e0b030>RAID O'CLOCK SHOP</color>   you have <color=#ffcc00>{player.inventory.GetAmount(ScrapId)} scrap</color>", FontSize = 18, Align = TextAnchor.MiddleLeft },
                RectTransform = { AnchorMin = "0.02 0.92", AnchorMax = "0.8 0.99" },
            }, Ui);
            ui.Add(new CuiButton
            {
                Button = { Command = "scrapshop.close", Color = "0.7 0.2 0.15 1" },
                Text = { Text = "CLOSE", FontSize = 14, Align = TextAnchor.MiddleCenter },
                RectTransform = { AnchorMin = "0.88 0.93", AnchorMax = "0.98 0.985" },
            }, Ui);

            // Category tabs
            float w = 0.96f / Categories.Length;
            for (int i = 0; i < Categories.Length; i++)
                ui.Add(new CuiButton
                {
                    Button = { Command = $"scrapshop.cat {i}", Color = i == cat ? "0.8 0.38 0.17 1" : "0.22 0.2 0.18 1" },
                    Text = { Text = Categories[i].Key.ToUpper(), FontSize = 13, Align = TextAnchor.MiddleCenter },
                    RectTransform = { AnchorMin = $"{0.02f + i * w + 0.003f} 0.84", AnchorMax = $"{0.02f + (i + 1) * w - 0.003f} 0.9" },
                }, Ui);

            // Item tiles: 4 per row
            Offer[] offers = Categories[cat].Value;
            const int cols = 4;
            for (int n = 0; n < offers.Length; n++)
            {
                ItemDefinition def = ItemManager.FindItemDefinition(offers[n].Item);
                if (def == null) continue;
                int row = n / cols, col = n % cols;
                float x0 = 0.02f + col * 0.24f, x1 = x0 + 0.23f, y1 = 0.82f - row * 0.27f, y0 = y1 - 0.25f;
                string tile = $"{Ui}.tile{n}";
                ui.Add(new CuiPanel { Image = { Color = "0.16 0.14 0.12 1" }, RectTransform = { AnchorMin = $"{x0} {y0}", AnchorMax = $"{x1} {y1}" } }, Ui, tile);
                ui.Add(new CuiElement
                {
                    Parent = tile,
                    Components = { new CuiImageComponent { ItemId = def.itemid }, new CuiRectTransformComponent { AnchorMin = "0.05 0.3", AnchorMax = "0.4 0.95" } },
                });
                ui.Add(new CuiLabel
                {
                    Text = { Text = $"{def.displayName.english}\n<color=#a39886>x{offers[n].Amount}</color>", FontSize = 12, Align = TextAnchor.MiddleLeft },
                    RectTransform = { AnchorMin = "0.43 0.35", AnchorMax = "0.98 0.95" },
                }, tile);
                ui.Add(new CuiButton
                {
                    Button = { Command = $"scrapshop.buy {cat} {n}", Color = "0.3 0.55 0.25 1" },
                    Text = { Text = $"BUY  {offers[n].Price} scrap", FontSize = 12, Align = TextAnchor.MiddleCenter },
                    RectTransform = { AnchorMin = "0.05 0.06", AnchorMax = "0.95 0.27" },
                }, tile);
            }
            CuiHelper.AddUi(player, ui);
        }
    }
}

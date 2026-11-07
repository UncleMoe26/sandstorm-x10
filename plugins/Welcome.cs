using System.Collections.Generic;
using Oxide.Core;
using Oxide.Game.Rust.Cui;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Welcome", "Uncle Moe", "1.9.0")]
    [Description("Greets players, shows a welcome popup, reminds about kits and posts the online count")]
    class Welcome : RustPlugin
    {
        const string Message =
            "<color=#e0b030>Welcome to RAID O'CLOCK 10x!</color> x10 gather, no BPs, fast craft, fast smelt, boosted loot. <color=#ffcc00>Wipes once a month: the first Thursday, 21:00 Lebanon time.</color>\n" +
            "<color=#9fd0ff>/shop</color>  <color=#9fd0ff>/skin</color>  <color=#9fd0ff>/kit</color> starter kit  <color=#9fd0ff>/sethome</color> <color=#9fd0ff>/home</color> <color=#9fd0ff>/tpr</color> teleport  <color=#9fd0ff>/backpack</color>  <color=#9fd0ff>/mymini</color>  <color=#9fd0ff>/remove</color>  <color=#9fd0ff>/grade hqm</color>\n" +
            "<color=#ffcc00>Your TC repairs your base by itself!</color> Locked crate or airdrop event every hour. Bring your friends with <color=#9fd0ff>/invite</color> and you both get free loot!";

        const string KitReminder =
            "<color=#ffcc00>FREE KITS!</color> Type <color=#9fd0ff>/kit</color> to open the kit menu. The starter kit can be claimed every 20 minutes.";

        const string PopupText =
            "<color=#e0b030>SERVER</color>\n" +
            "x10 gather  •  <color=#ffcc00>No BPs: every blueprint unlocked</color>  •  fast craft (3s max)  •  fast smelt and recycle  •  boosted loot\n" +
            "Scientists, heavies, animals and the Bradley at Launch Site  •  <color=#ffcc00>loot events every hour</color> (locked crates, airdrops)\n" +
            "<color=#ffcc00>Wipes: monthly, the first Thursday 21:00 Lebanon time</color>  •  max 6 per team\n" +
            "<color=#ffcc00>TC AUTO-REPAIR:</color> your cupboard repairs your base by itself, 30 s after the last hit (paid from the TC, keep it stocked)\n\n" +
            "<color=#e0b030>COMMANDS</color>\n" +
            "<color=#9fd0ff>/kit</color>  free kits (starter kit every 20 minutes)\n" +
            "<color=#9fd0ff>/sethome name</color>  then  <color=#9fd0ff>/home name</color>  teleport home (3 homes)\n" +
            "<color=#9fd0ff>/tpr player</color>  teleport to a friend (they accept with <color=#9fd0ff>/tpa</color>)\n" +
            "<color=#9fd0ff>/outpost</color>  <color=#9fd0ff>/bandit</color>  teleport to the safe zones    <color=#9fd0ff>/mymini</color>  your own minicopter\n" +
            "<color=#9fd0ff>/backpack</color>  extra storage    <color=#9fd0ff>/remove</color>  remove your own building parts\n" +
            "<color=#9fd0ff>/shop</color>  buy guns, armor and resources with scrap    <color=#9fd0ff>/skin</color>  free skins    <color=#9fd0ff>/grade hqm</color>  auto upgrade\n" +
            "<color=#9fd0ff>/invite</color>  your invite code: when a friend plays 30 min you BOTH get free loot\n" +
            "<color=#9fd0ff>/wipekit</color>  free starter pack in the first 2 hours after every wipe\n\n" +
            "<color=#e0b030>RULES</color>\n" +
            "No cheating, no exploiting, no racism. Have fun!\n\n" +
            "<color=#e0b030>DISCORD</color>  <color=#9fd0ff>discord.gg/5GhkemXHP</color>  wipe alerts, find a team, report cheaters";

        // Seconds between online count announcements
        const float OnlineInterval = 600f;
        // Seconds between kit reminders (first one is offset so it doesn't land on the online count)
        const float KitInterval = 900f;
        // The owner doesn't get the popup on join
        const ulong OwnerId = 0;
        const string PopupName = "WelcomePopup";
        // Popup closes by itself after this long
        const float PopupSeconds = 60f;

        readonly HashSet<ulong> greeted = new HashSet<ulong>();
        // Every player that has ever joined, so first-timers can be announced
        HashSet<ulong> seen;

        void OnServerInitialized()
        {
            seen = Interface.Oxide.DataFileSystem.ReadObject<HashSet<ulong>>("Welcome_Seen") ?? new HashSet<ulong>();
            // Anyone already on the map (awake or sleeping) has played before
            foreach (var p in BasePlayer.allPlayerList)
                if (p != null && !p.IsNpc && p.userID.IsSteamId()) seen.Add(p.userID);
            SaveSeen();
            // Serialize the popup once at boot: the first CUI serialization is slow (~300 ms) and would freeze the first join
            CuiHelper.ToJson(BuildPopup("warmup"));
            timer.Every(OnlineInterval, AnnounceOnline);
            timer.Once(KitInterval / 2f, () =>
            {
                AnnounceKits();
                timer.Every(KitInterval, AnnounceKits);
            });
        }

        void Unload()
        {
            foreach (var p in BasePlayer.activePlayerList) CuiHelper.DestroyUi(p, PopupName);
        }

        void SaveSeen() => Interface.Oxide.DataFileSystem.WriteObject("Welcome_Seen", seen);

        void OnPlayerConnected(BasePlayer player)
        {
            if (player == null || !seen.Add(player.userID)) return;
            SaveSeen();
            PrintToChat($"<color=#e0b030>Everyone welcome</color> <color=#9fd0ff>{player.displayName}</color> <color=#e0b030>to RAID O'CLOCK 10x!</color> First time on the server.");
        }

        void OnPlayerSleepEnded(BasePlayer player)
        {
            if (player == null || player.IsNpc) return;
            if (player.HasPlayerFlag(BasePlayer.PlayerFlags.ReceivingSnapshot)) return;
            if (!greeted.Add(player.userID)) return;
            player.ChatMessage(Message);
            if (player.userID != OwnerId) ShowPopup(player);
        }

        void OnPlayerDisconnected(BasePlayer player) => greeted.Remove(player.userID);

        void AnnounceOnline()
        {
            var online = BasePlayer.activePlayerList.Count;
            if (online == 0) return;
            PrintToChat($"<color=#e0b030>Players online:</color> {online}/{ConVar.Server.maxplayers}. Invite your friends!");
        }

        void AnnounceKits()
        {
            if (BasePlayer.activePlayerList.Count == 0) return;
            PrintToChat(KitReminder);
        }

        void ShowPopup(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, PopupName);
            CuiHelper.AddUi(player, BuildPopup(player.displayName.Replace("<", "").Replace(">", "")));
            timer.Once(PopupSeconds, () =>
            {
                if (player != null && player.IsConnected) CuiHelper.DestroyUi(player, PopupName);
            });
        }

        CuiElementContainer BuildPopup(string name)
        {
            var c = new CuiElementContainer();
            c.Add(new CuiPanel
            {
                Image = { Color = "0.05 0.05 0.05 0.92" },
                RectTransform = { AnchorMin = "0.26 0.16", AnchorMax = "0.74 0.84" },
                CursorEnabled = true
            }, "Overlay", PopupName);
            c.Add(new CuiPanel
            {
                Image = { Color = "0.88 0.69 0.19 1" },
                RectTransform = { AnchorMin = "0 0.988", AnchorMax = "1 1" }
            }, PopupName);
            c.Add(new CuiLabel
            {
                Text = { Text = "WELCOME TO RAID O'CLOCK 10x", FontSize = 26, Font = "robotocondensed-bold.ttf", Align = TextAnchor.MiddleCenter, Color = "0.88 0.69 0.19 1" },
                RectTransform = { AnchorMin = "0.03 0.87", AnchorMax = "0.97 0.97" }
            }, PopupName);
            c.Add(new CuiLabel
            {
                Text = { Text = $"Hey {name}, glad to have you here!", FontSize = 15, Align = TextAnchor.MiddleCenter, Color = "1 1 1 0.8" },
                RectTransform = { AnchorMin = "0.03 0.81", AnchorMax = "0.97 0.87" }
            }, PopupName);
            c.Add(new CuiLabel
            {
                Text = { Text = PopupText, FontSize = 14, Align = TextAnchor.UpperLeft, Color = "1 1 1 0.95" },
                RectTransform = { AnchorMin = "0.06 0.15", AnchorMax = "0.94 0.79" }
            }, PopupName);
            c.Add(new CuiButton
            {
                Button = { Command = "welcome.close", Color = "0.30 0.55 0.18 1" },
                RectTransform = { AnchorMin = "0.38 0.04", AnchorMax = "0.62 0.11" },
                Text = { Text = "LET'S PLAY", FontSize = 18, Font = "robotocondensed-bold.ttf", Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" }
            }, PopupName);
            return c;
        }

        [ConsoleCommand("welcome.close")]
        void CmdClose(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player != null) CuiHelper.DestroyUi(player, PopupName);
        }

        // Lets an admin see the popup that new players get
        [ChatCommand("welcomepreview")]
        void CmdPreview(BasePlayer player, string command, string[] args)
        {
            if (!player.IsAdmin) return;
            ShowPopup(player);
        }
    }
}

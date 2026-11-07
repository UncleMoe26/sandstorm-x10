using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Oxide.Core;

namespace Oxide.Plugins
{
    [Info("Wipe Event", "Uncle Moe", "1.2.0")]
    [Description("Wipe night: the server name shows WIPE TONIGHT on wipe day (the first Thursday of the month) and JUST WIPED for a day after, and /wipekit gives a free starter pack in the first 2 hours of a wipe")]
    class WipeEvent : RustPlugin
    {
        // The wipe (scheduler.sh): once a month, the first Thursday (Facepunch's update day), 20:00 Amsterdam = 21:00 Lebanon
        const int WipeHour = 20;
        const double WipeKitHours = 2;
        const double JustWipedHours = 24;
        // Seconds between wipe kit announcements while it's available
        const float AnnounceInterval = 600f;
        static readonly Regex WipedPart = new Regex(@"Wiped \d{2}/\d{2}");

        // "shortname:amount"
        static readonly string[] WipeKit =
        {
            "pickaxe:1", "hatchet:1", "wood:10000", "stones:10000", "metal.fragments:2000", "furnace:1",
            "pistol.revolver:1", "ammo.pistol:64", "bandage:10", "syringe.medical:2",
        };

        class StoredData
        {
            public string Wipe = "";
            public HashSet<ulong> Claimed = new HashSet<ulong>();
        }

        StoredData data;
        string baseName;
        TimeZoneInfo amsterdam;

        void OnServerInitialized()
        {
            baseName = ConVar.Server.hostname;
            try { amsterdam = TimeZoneInfo.FindSystemTimeZoneById("Europe/Amsterdam"); }
            catch { amsterdam = null; }
            data = Interface.Oxide.DataFileSystem.ReadObject<StoredData>("WipeEvent") ?? new StoredData();
            string wipe = WipeTime.ToString("o");
            if (data.Wipe != wipe)
            {
                data = new StoredData { Wipe = wipe };
                Interface.Oxide.DataFileSystem.WriteObject("WipeEvent", data);
            }
            UpdateName();
            timer.Every(60f, UpdateName);
            timer.Every(AnnounceInterval, Announce);
        }

        // Put the normal name back so a reload doesn't pick up a modified one
        void Unload() => ConVar.Server.hostname = baseName;

        static DateTime WipeTime => SaveRestore.SaveCreatedTime.ToUniversalTime();
        static double HoursSinceWipe => (DateTime.UtcNow - WipeTime).TotalHours;
        bool WipeKitOpen => HoursSinceWipe < WipeKitHours;

        DateTime AmsterdamNow()
        {
            if (amsterdam != null) return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, amsterdam);
            return DateTime.UtcNow.AddHours(1);
        }

        static bool FirstThursday(DateTime d) => d.DayOfWeek == DayOfWeek.Thursday && d.Day <= 7;

        // The next first Thursday of a month, from the given day on
        static DateTime NextWipe(DateTime from)
        {
            DateTime d = from.Date;
            while (!FirstThursday(d) || (d == from.Date && from.Hour >= WipeHour)) d = d.AddDays(1);
            return d;
        }

        void UpdateName()
        {
            string name = baseName;
            DateTime local = AmsterdamNow();
            if (HoursSinceWipe < JustWipedHours)
                name = WipedPart.Replace(baseName, "JUST WIPED");
            else if (FirstThursday(local) && local.Hour < WipeHour)
                name = WipedPart.Replace(baseName, "WIPE TONIGHT 21:00 LB");
            if (ConVar.Server.hostname != name) ConVar.Server.hostname = name;
        }

        void Announce()
        {
            if (!WipeKitOpen || BasePlayer.activePlayerList.Count == 0) return;
            int left = (int)Math.Ceiling((WipeKitHours - HoursSinceWipe) * 60);
            PrintToChat($"<color=#ffcc00>WIPE DAY!</color> Type <color=#9fd0ff>/wipekit</color> for a free starter pack (tools, wood, stone, metal, a furnace and a revolver). Only {left} more minutes!");
        }

        void OnPlayerSleepEnded(BasePlayer player)
        {
            if (player == null || !WipeKitOpen || data.Claimed.Contains(player.userID)) return;
            player.ChatMessage("<color=#ffcc00>WIPE DAY!</color> Type <color=#9fd0ff>/wipekit</color> for a free starter pack. Only in the first 2 hours after the wipe.");
        }

        [ChatCommand("wipekit")]
        void CmdWipeKit(BasePlayer player, string command, string[] args)
        {
            if (!WipeKitOpen)
            {
                player.ChatMessage("The wipe kit is only available in the first 2 hours after a wipe. Next wipe: <color=#ffcc00>{NextWipe(AmsterdamNow()):dddd d MMMM} 21:00 Lebanon time</color> (once a month).");
                return;
            }
            if (!data.Claimed.Add(player.userID))
            {
                player.ChatMessage("You've already claimed your wipe kit this wipe.");
                return;
            }
            Interface.Oxide.DataFileSystem.WriteObject("WipeEvent", data);
            foreach (string entry in WipeKit)
            {
                string[] parts = entry.Split(':');
                Item item = ItemManager.CreateByName(parts[0], int.Parse(parts[1]));
                if (item == null) { PrintWarning($"Unknown wipe kit item {parts[0]}"); continue; }
                if (!player.inventory.GiveItem(item)) item.Drop(player.GetDropPosition(), player.GetDropVelocity());
            }
            player.ChatMessage("<color=#55ff55>Wipe kit claimed!</color> Good luck this wipe.");
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Oxide.Core;

namespace Oxide.Plugins
{
    [Info("Discord Announce", "Uncle Moe", "1.2.0")]
    [Description("Posts wipe countdowns, 'just wiped' and player-count milestones to a Discord channel through a webhook")]
    class DiscordAnnounce : RustPlugin
    {
        // The wipe (scheduler.sh): once a month, the first Thursday, 20:00 Amsterdam = 21:00 Lebanon
        const int WipeHour = 20;
        const string Connect = "client.connect 2.59.169.7:28015";
        static readonly int[] Milestones = { 5, 10, 20, 30, 50, 75, 100 };
        const int Orange = 0xEC7A2E;

        class PluginConfig
        {
            [JsonProperty("Discord webhook URL (Channel settings > Integrations > Webhooks)")]
            public string WebhookUrl = "";
            [JsonProperty("Discord invite link shown in the posts (optional)")]
            public string InviteUrl = "";
            [JsonProperty("Website")]
            public string WebsiteUrl = "https://unclemoe26.github.io/sandstorm-x10/";
        }

        class StoredData
        {
            // Announcements already sent, so a restart doesn't repeat them
            public HashSet<string> Sent = new HashSet<string>();
        }

        PluginConfig config;
        StoredData data;
        TimeZoneInfo amsterdam;

        protected override void LoadDefaultConfig() => config = new PluginConfig();

        protected override void LoadConfig()
        {
            base.LoadConfig();
            config = Config.ReadObject<PluginConfig>() ?? new PluginConfig();
            SaveConfig();
        }

        protected override void SaveConfig() => Config.WriteObject(config);

        bool Ready => config.WebhookUrl.StartsWith("https://discord.com/api/webhooks/") || config.WebhookUrl.StartsWith("https://discordapp.com/api/webhooks/");

        void OnServerInitialized()
        {
            data = Interface.Oxide.DataFileSystem.ReadObject<StoredData>("DiscordAnnounce") ?? new StoredData();
            try { amsterdam = TimeZoneInfo.FindSystemTimeZoneById("Europe/Amsterdam"); }
            catch { amsterdam = null; }
            if (!Ready)
            {
                PrintWarning("Set the Discord webhook URL in oxide/config/DiscordAnnounce.json, then reload the plugin.");
                return;
            }
            timer.Every(60f, Check);
            Check();
        }

        DateTime AmsterdamNow() => amsterdam != null ? TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, amsterdam) : DateTime.UtcNow.AddHours(1);

        void Check()
        {
            DateTime local = AmsterdamNow();
            string day = local.ToString("yyyy-MM-dd");
            double hoursSinceWipe = (DateTime.UtcNow - SaveRestore.SaveCreatedTime.ToUniversalTime()).TotalHours;

            // The map is fresh: once per wipe
            if (hoursSinceWipe < 1)
                Once("wiped:" + SaveRestore.SaveCreatedTime.ToUniversalTime().ToString("o"), "🟢 JUST WIPED",
                    "Fresh map is live right now. Free **/wipekit** for the first 2 hours, so get in early!");

            // Wipe-day countdown
            if (local.DayOfWeek == DayOfWeek.Thursday && local.Day <= 7 && hoursSinceWipe > 12)
            {
                if (local.Hour >= 12 && local.Hour < WipeHour - 2)
                    Once("today:" + day, "⏰ WIPE TONIGHT", "Fresh map tonight at **21:00 Lebanon / 20:00 CET**. Free **/wipekit** for the first 2 hours after the wipe.");
                if (local.Hour == WipeHour - 2)
                    Once("2h:" + day, "⏰ WIPE IN 2 HOURS", "Fresh map at **21:00 Lebanon / 20:00 CET**. Get your squad ready.");
                if (local.Hour == WipeHour - 1 && local.Minute >= 45)
                    Once("15m:" + day, "⏰ WIPE IN 15 MINUTES", "Be online at **21:00 Lebanon / 20:00 CET** and claim your free **/wipekit**.");
            }

            // Player milestones, once per day each
            int online = BasePlayer.activePlayerList.Count;
            int reached = Milestones.Where(m => online >= m).DefaultIfEmpty(0).Max();
            if (reached > 0)
                Once($"players:{reached}:{day}", $"🔥 {online} PLAYERS ONLINE", $"RAID O'CLOCK 10x is busy right now with **{online}/{ConVar.Server.maxplayers}** players. Jump in!");
        }

        void Once(string key, string title, string text)
        {
            if (data.Sent.Contains(key)) return;
            data.Sent.Add(key);
            // Keep the list short: only this week's keys matter
            if (data.Sent.Count > 200) data.Sent = new HashSet<string>(data.Sent.Skip(data.Sent.Count - 100));
            Interface.Oxide.DataFileSystem.WriteObject("DiscordAnnounce", data);
            Post(title, text);
        }

        void Post(string title, string text)
        {
            string links = $"**Join:** `{Connect}` (Modded tab, search \"Raid O'Clock\")\n**Website:** {config.WebsiteUrl}" +
                           (string.IsNullOrEmpty(config.InviteUrl) ? "" : $"\n**Discord:** {config.InviteUrl}");
            var payload = new
            {
                username = "RAID O'CLOCK 10x",
                avatar_url = config.WebsiteUrl + "logo.png",
                embeds = new[]
                {
                    new
                    {
                        title,
                        url = config.WebsiteUrl,
                        description = text + "\n\n" + links,
                        color = Orange,
                        thumbnail = new { url = config.WebsiteUrl + "logo.png" },
                    },
                },
            };
            webrequest.Enqueue(config.WebhookUrl, JsonConvert.SerializeObject(payload), (code, response) =>
            {
                if (code < 200 || code >= 300) PrintWarning($"Discord answered {code}: {response}");
            }, this, Core.Libraries.RequestMethod.POST, new Dictionary<string, string> { ["Content-Type"] = "application/json" });
        }

        // Server console: discordannounce.test posts a test message
        [ConsoleCommand("discordannounce.test")]
        void CmdTest(ConsoleSystem.Arg arg)
        {
            if (arg.Connection != null) return;
            if (!Ready) { Puts("Webhook not set"); return; }
            Post("✅ RAID O'CLOCK 10x is connected", "Wipe countdowns, 'just wiped' and player milestones will be posted here automatically.");
            Puts("Test message sent");
        }
    }
}

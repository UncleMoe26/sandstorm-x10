using System.Collections.Generic;
using Newtonsoft.Json;

namespace Oxide.Plugins
{
    [Info("Vote Rewards", "Uncle Moe", "1.0.0")]
    [Description("/vote shows where to vote for the server on rust-servers.net; /claimvote gives a reward once a day for a vote")]
    class VoteRewards : RustPlugin
    {
        // Checks and claims a vote in one call: 0 = no vote in the last 24 h, 1 = voted (now claimed), 2 = already claimed
        const string RewardUrl = "https://rust-servers.net/api/?object=plugin&element=reward&key={0}&steamid={1}";
        // Seconds between "vote for us" reminders
        const float ReminderInterval = 2700f;

        class PluginConfig
        {
            [JsonProperty("rust-servers.net server key (from the server's Edit page)")]
            public string ApiKey = "";
            [JsonProperty("Vote page link shown to players")]
            public string VoteUrl = "";
            [JsonProperty("Reward (shortname:amount)")]
            public List<string> Reward = new List<string> { "scrap:300", "syringe.medical:3", "ammo.rifle:64" };
        }

        PluginConfig config;
        // Players with a claim in flight, so spamming the command doesn't send many requests
        readonly HashSet<ulong> checking = new HashSet<ulong>();

        protected override void LoadDefaultConfig() => config = new PluginConfig();

        protected override void LoadConfig()
        {
            base.LoadConfig();
            config = Config.ReadObject<PluginConfig>() ?? new PluginConfig();
            SaveConfig();
        }

        protected override void SaveConfig() => Config.WriteObject(config);

        bool Ready => !string.IsNullOrEmpty(config.ApiKey) && !string.IsNullOrEmpty(config.VoteUrl);

        void OnServerInitialized()
        {
            if (!Ready) PrintWarning("Set the rust-servers.net server key and vote link in oxide/config/VoteRewards.json, then reload the plugin.");
            timer.Every(ReminderInterval, () =>
            {
                if (Ready && BasePlayer.activePlayerList.Count > 0)
                    PrintToChat("<color=#ffcc00>VOTE FOR US</color> once a day and get free loot: type <color=#9fd0ff>/vote</color>. Every vote pushes the server up the rankings so more players find us!");
            });
        }

        [ChatCommand("vote")]
        void CmdVote(BasePlayer player, string command, string[] args)
        {
            if (!Ready)
            {
                player.ChatMessage("Voting isn't set up yet. Check back soon!");
                return;
            }
            player.ChatMessage($"<color=#e0b030>Vote for RAID O'CLOCK 10x:</color> <color=#9fd0ff>{config.VoteUrl}</color>\n" +
                               "Log in with Steam when you vote, then come back and type <color=#9fd0ff>/claimvote</color> for your reward. You can vote once a day.");
        }

        [ChatCommand("claimvote")]
        void CmdClaimVote(BasePlayer player, string command, string[] args)
        {
            if (!Ready)
            {
                player.ChatMessage("Voting isn't set up yet. Check back soon!");
                return;
            }
            if (!checking.Add(player.userID)) return;
            webrequest.Enqueue(string.Format(RewardUrl, config.ApiKey, player.UserIDString), null, (code, response) =>
            {
                checking.Remove(player.userID);
                if (player == null || !player.IsConnected) return;
                string result = (response ?? "").Trim();
                if (code != 200)
                {
                    player.ChatMessage("Couldn't reach the vote site, try again in a minute.");
                    PrintWarning($"rust-servers.net answered {code}: {result}");
                    return;
                }
                if (result == "1")
                {
                    GiveReward(player);
                    player.ChatMessage("<color=#55ff55>Thanks for voting!</color> Your reward is in your inventory. Vote again tomorrow.");
                    PrintToChat($"<color=#9fd0ff>{player.displayName}</color> voted for the server and got free loot. Type <color=#9fd0ff>/vote</color> to do the same!");
                }
                else if (result == "2") player.ChatMessage("You've already claimed today's vote reward. Vote again tomorrow!");
                else player.ChatMessage($"No vote found for you in the last 24 hours. Vote here (log in with Steam): <color=#9fd0ff>{config.VoteUrl}</color>");
            }, this);
        }

        void GiveReward(BasePlayer player)
        {
            foreach (string entry in config.Reward)
            {
                string[] parts = entry.Split(':');
                int amount;
                Item item = parts.Length == 2 && int.TryParse(parts[1], out amount) ? ItemManager.CreateByName(parts[0], amount) : null;
                if (item == null) { PrintWarning($"Bad reward entry {entry}"); continue; }
                if (!player.inventory.GiveItem(item)) item.Drop(player.GetDropPosition(), player.GetDropVelocity());
            }
        }
    }
}

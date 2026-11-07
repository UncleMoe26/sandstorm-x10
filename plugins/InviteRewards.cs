using System.Collections.Generic;
using System.Linq;
using Oxide.Core;

namespace Oxide.Plugins
{
    [Info("Invite Rewards", "Uncle Moe", "1.0.0")]
    [Description("/invite gives every player a code; a new player who types /invited <code> and plays 30 minutes earns a reward for both of them")]
    class InviteRewards : RustPlugin
    {
        // Minutes the invited friend has to play before both rewards pay out
        const int MinutesToQualify = 30;
        // New players have this many minutes of playtime to enter a code
        const int ClaimWindowMinutes = 120;
        // Paid-out invites per player, to stop alt-account farming
        const int MaxRewardsPerInviter = 20;
        // Seconds between "bring a friend" reminders
        const float ReminderInterval = 1200f;
        const string CodeAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

        // "shortname:amount"
        static readonly string[] InviterReward = { "scrap:500", "rifle.semiauto:1", "ammo.rifle:128", "syringe.medical:5" };
        static readonly string[] FriendReward = { "scrap:300", "pistol.semiauto:1", "ammo.pistol:128", "bandage:5", "wood:5000", "stones:5000" };

        class StoredData
        {
            public Dictionary<ulong, string> Codes = new Dictionary<ulong, string>();
            // Players who were already on the server before they could be invited
            public HashSet<ulong> Veterans = new HashSet<ulong>();
            // Playtime in minutes of new players
            public Dictionary<ulong, int> Minutes = new Dictionary<ulong, int>();
            // Friend -> the player who invited them
            public Dictionary<ulong, ulong> InvitedBy = new Dictionary<ulong, ulong>();
            // Friends whose invite has paid out
            public HashSet<ulong> PaidOut = new HashSet<ulong>();
            // Inviter -> invites paid out
            public Dictionary<ulong, int> Recruits = new Dictionary<ulong, int>();
            // Inviter -> rewards waiting because they were offline
            public Dictionary<ulong, int> Pending = new Dictionary<ulong, int>();
            public Dictionary<ulong, string> Names = new Dictionary<ulong, string>();
        }

        StoredData data;
        bool dirty;

        void OnServerInitialized()
        {
            data = Interface.Oxide.DataFileSystem.ReadObject<StoredData>("InviteRewards") ?? new StoredData();
            if (data.Veterans.Count == 0)
            {
                // First run: everyone who has played here before is a veteran and can't use a code
                var seen = Interface.Oxide.DataFileSystem.ReadObject<HashSet<ulong>>("Welcome_Seen");
                if (seen != null) data.Veterans.UnionWith(seen);
                foreach (BasePlayer p in BasePlayer.allPlayerList)
                    if (p != null && !p.IsNpc && p.userID.IsSteamId()) data.Veterans.Add(p.userID);
                dirty = true;
            }
            foreach (BasePlayer p in BasePlayer.activePlayerList) Track(p);
            timer.Every(60f, Tick);
            timer.Every(ReminderInterval, Remind);
            Save();
        }

        void Unload() => Save();
        void OnServerSave() => Save();

        void Save()
        {
            if (!dirty || data == null) return;
            Interface.Oxide.DataFileSystem.WriteObject("InviteRewards", data);
            dirty = false;
        }

        void OnPlayerConnected(BasePlayer player) => Track(player);

        void Track(BasePlayer player)
        {
            if (player == null || !player.userID.IsSteamId()) return;
            data.Names[player.userID] = player.displayName;
            if (!data.Veterans.Contains(player.userID) && !data.Minutes.ContainsKey(player.userID)) data.Minutes[player.userID] = 0;
            dirty = true;
        }

        void OnPlayerSleepEnded(BasePlayer player)
        {
            int pending;
            if (player == null || !data.Pending.TryGetValue(player.userID, out pending) || pending <= 0) return;
            for (int i = 0; i < pending; i++) Give(player, InviterReward);
            data.Pending.Remove(player.userID);
            dirty = true;
            player.ChatMessage($"<color=#55ff55>Invite reward x{pending}:</color> friends you invited played {MinutesToQualify} minutes while you were away. Thanks for growing the server!");
        }

        void Tick()
        {
            foreach (BasePlayer player in BasePlayer.activePlayerList.ToList())
            {
                int minutes;
                if (!data.Minutes.TryGetValue(player.userID, out minutes)) continue;
                data.Minutes[player.userID] = ++minutes;
                dirty = true;

                ulong inviter;
                if ((minutes == 5 || minutes == ClaimWindowMinutes - 30) && !data.InvitedBy.ContainsKey(player.userID))
                    player.ChatMessage($"Did a friend invite you? Type <color=#9fd0ff>/invited CODE</color> with their code in the next {ClaimWindowMinutes - minutes} minutes and you both get rewards.");
                if (minutes < MinutesToQualify || data.PaidOut.Contains(player.userID) || !data.InvitedBy.TryGetValue(player.userID, out inviter)) continue;
                PayOut(player, inviter);
            }
            Save();
        }

        void PayOut(BasePlayer friend, ulong inviterId)
        {
            data.PaidOut.Add(friend.userID);
            int count;
            data.Recruits.TryGetValue(inviterId, out count);
            data.Recruits[inviterId] = count + 1;
            dirty = true;

            Give(friend, FriendReward);
            friend.ChatMessage($"<color=#55ff55>Invite reward!</color> You played {MinutesToQualify} minutes, so you and {InviterName(inviterId)} both got a reward. Check your inventory.");

            BasePlayer inviter = BasePlayer.FindByID(inviterId);
            if (inviter != null && inviter.IsConnected && !inviter.IsSleeping())
            {
                Give(inviter, InviterReward);
                inviter.ChatMessage($"<color=#55ff55>Invite reward!</color> {friend.displayName} played {MinutesToQualify} minutes. You've brought {count + 1} friend(s) so far.");
            }
            else
            {
                int pending;
                data.Pending.TryGetValue(inviterId, out pending);
                data.Pending[inviterId] = pending + 1;
            }
            PrintToChat($"<color=#e0b030>{InviterName(inviterId)}</color> brought <color=#9fd0ff>{friend.displayName}</color> to RAID O'CLOCK 10x and they both got rewards! Type <color=#9fd0ff>/invite</color> to get your own code.");
        }

        void Remind()
        {
            if (BasePlayer.activePlayerList.Count == 0) return;
            PrintToChat("<color=#ffcc00>BRING A FRIEND:</color> type <color=#9fd0ff>/invite</color> for your code. When your friend plays 30 minutes you BOTH get free loot.");
        }

        [ChatCommand("invite")]
        void CmdInvite(BasePlayer player, string command, string[] args)
        {
            if (args.Length > 0 && args[0].ToLower() == "top")
            {
                var top = data.Recruits.OrderByDescending(r => r.Value).Take(5).ToList();
                player.ChatMessage(top.Count == 0
                    ? "Nobody has brought a friend yet. Be the first: /invite"
                    : "<color=#e0b030>Top recruiters</color>\n" + string.Join("\n", top.Select((r, i) => $"{i + 1}. {InviterName(r.Key)} - {r.Value} friend(s)")));
                return;
            }
            int count;
            data.Recruits.TryGetValue(player.userID, out count);
            player.ChatMessage($"<color=#e0b030>Your invite code:</color> <color=#ffcc00>{CodeFor(player.userID)}</color>\n" +
                               $"Tell your friends to join <color=#e0b030>RAID O'CLOCK 10x</color> (search \"Raid O'Clock\" in the Modded tab) and type <color=#9fd0ff>/invited {CodeFor(player.userID)}</color>.\n" +
                               $"When they've played {MinutesToQualify} minutes, you get <color=#55ff55>500 scrap, a semi-auto rifle, ammo and syringes</color>, and they get a starter pack.\n" +
                               $"Friends brought so far: {count}/{MaxRewardsPerInviter}.  <color=#9fd0ff>/invite top</color> shows the top recruiters.");
        }

        [ChatCommand("invited")]
        void CmdInvited(BasePlayer player, string command, string[] args)
        {
            if (args.Length == 0)
            {
                player.ChatMessage("Type <color=#9fd0ff>/invited CODE</color> with the code your friend gave you.");
                return;
            }
            int minutes;
            if (data.Veterans.Contains(player.userID) || !data.Minutes.TryGetValue(player.userID, out minutes))
            {
                player.ChatMessage("Invite codes are for new players only. Get your own code with <color=#9fd0ff>/invite</color> and bring your friends!");
                return;
            }
            if (data.InvitedBy.ContainsKey(player.userID))
            {
                player.ChatMessage("You've already used an invite code.");
                return;
            }
            if (minutes >= ClaimWindowMinutes)
            {
                player.ChatMessage($"Invite codes have to be used in your first {ClaimWindowMinutes / 60} hours on the server.");
                return;
            }
            string code = args[0].ToUpper();
            ulong inviterId = data.Codes.FirstOrDefault(c => c.Value == code).Key;
            if (inviterId == 0)
            {
                player.ChatMessage($"No player has the code {code}. Check it with your friend.");
                return;
            }
            if (inviterId == player.userID)
            {
                player.ChatMessage("You can't use your own code.");
                return;
            }
            int count;
            if (data.Recruits.TryGetValue(inviterId, out count) && count >= MaxRewardsPerInviter)
            {
                player.ChatMessage($"{InviterName(inviterId)} has already brought {MaxRewardsPerInviter} friends, the maximum. Welcome anyway!");
                return;
            }
            data.InvitedBy[player.userID] = inviterId;
            dirty = true;
            Save();
            int left = System.Math.Max(0, MinutesToQualify - minutes);
            player.ChatMessage($"<color=#55ff55>Code accepted!</color> Play {left} more minute(s) and you and {InviterName(inviterId)} both get a reward.");
            BasePlayer inviter = BasePlayer.FindByID(inviterId);
            if (inviter != null && inviter.IsConnected)
                inviter.ChatMessage($"<color=#55ff55>{player.displayName} used your invite code!</color> When they've played {MinutesToQualify} minutes you both get rewarded.");
        }

        string CodeFor(ulong userId)
        {
            string code;
            if (data.Codes.TryGetValue(userId, out code)) return code;
            var used = new HashSet<string>(data.Codes.Values);
            do code = new string(Enumerable.Range(0, 5).Select(_ => CodeAlphabet[UnityEngine.Random.Range(0, CodeAlphabet.Length)]).ToArray());
            while (used.Contains(code));
            data.Codes[userId] = code;
            dirty = true;
            Save();
            return code;
        }

        string InviterName(ulong userId)
        {
            string name;
            return data.Names.TryGetValue(userId, out name) ? name : userId.ToString();
        }

        void Give(BasePlayer player, string[] reward)
        {
            foreach (string entry in reward)
            {
                string[] parts = entry.Split(':');
                Item item = ItemManager.CreateByName(parts[0], int.Parse(parts[1]));
                if (item == null) { PrintWarning($"Unknown reward item {parts[0]}"); continue; }
                if (!player.inventory.GiveItem(item)) item.Drop(player.GetDropPosition(), player.GetDropVelocity());
            }
        }
    }
}

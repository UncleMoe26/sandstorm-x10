# RAID O'CLOCK 10x

**EU modded Rust server · 250 slots · wipes once a month, the first Thursday 20:00 CET (21:00 Lebanon time) · max 6 per team**

```
client.connect 2.59.169.7:28015
```

In game, open the **Modded** tab and search for **Raid O'Clock**, or press **F1** and paste the line above.

**Website:** [unclemoe26.github.io/sandstorm-x10](https://unclemoe26.github.io/sandstorm-x10/), with a one-click Join button and a countdown to the next wipe.

## Free loot

- **`/wipekit`**: a free starter pack (tools, 10k wood and stone, metal, a furnace and a revolver) in the first 2 hours after every wipe
- **`/invite`**: get your invite code and give it to a friend. When they type `/invited CODE` and play 30 minutes, **you both** get rewarded: 500 scrap, a semi-auto rifle, ammo and syringes for you, a starter pack for them. `/invite top` shows the best recruiters
- **`/kit`**: free kits all wipe long

## What's on the server

- x10 gather, crafting capped at 3 seconds, fast smelting and recycling
- **No BPs**: every blueprint is unlocked from the start
- **TC auto-repair**: your cupboard repairs your base by itself, 30 seconds after the last hit, paid from the TC like a hammer repair
- **TC upgrade button**: upgrade your whole base to stone, metal or HQM from the TC in one click, with any building skin you own
- **A whole month per map**: the map only wipes with Rust's monthly update
- `/mymini`: your own fuelled minicopter (`/nomini` puts it away)
- `/shop`: buy guns, armor, ammo, resources and raid gear with scrap
- Events every hour: a locked crate at a monument or a supply drop, marked on the map
- `/skin`: free approved skins for the item in your hand
- Max 6 players per team
- Water flows through pipes 10x faster
- Barrels and crates hold useful loot with 3x scrap, and stacks are x20 (resources up to 10,000)
- Free kits with `/kit` (starter kit every 20 minutes)
- Teleport: `/sethome`, `/home`, `/tpr` (accept with `/tpa`), `/outpost`, `/bandit`
- `/backpack`, `/remove`, `/grade hqm` auto-upgrade, furnace splitter, quick sort
- 4500 map with scientists, heavy scientists, animals and the Bradley tank at Launch Site
- A storm rolls in every hour
- Short nights: the night is still there, but it's over in 8 minutes
- Monument lights stay on and puzzles need no fuses
- Active admin

## Wipe schedule

| | |
|---|---|
| Map wipe | Once a month: the first Thursday, 20:00 CET / 21:00 Lebanon time, together with Rust's monthly update |
| Blueprints | Not needed: every blueprint is unlocked (No BPs) |

The server name shows the last wipe date, for example `Wiped 30/10`. On wipe days before the wipe it reads `WIPE TONIGHT 21:00 LB`, and for 24 hours after a wipe `JUST WIPED`.

## Rules

1. No cheating, scripting or exploiting.
2. No racism, hate speech or harassment.
3. Respect the admin's decisions. Report problems in game.

## Plugins in this repo

These are the gameplay plugins written for this server (Oxide / uMod, C#). They can be used on any Rust server: drop the `.cs` file into `oxide/plugins`.

| Plugin | What it does |
|---|---|
| [FastCrafting](plugins/FastCrafting.cs) | Caps crafting time at 3 seconds for every item |
| [FastSmelt](plugins/FastSmelt.cs) | Shortens ore cook times using Rust's own furnace logic, so it adds no load |
| [FastRecycler](plugins/FastRecycler.cs) | Recyclers process an item every second instead of every 5 |
| [FastWater](plugins/FastWater.cs) | Water moves through pipes 10x faster from player-built catchers, barrels, pumps and purifiers; monument water stays vanilla |
| [LootBoost](plugins/LootBoost.cs) | Removes junk from barrels and crates, triples scrap and adds useful loot |
| [StackSizes](plugins/StackSizes.cs) | Resources stack to at least 10,000 (never below vanilla, so water tanks still fill), weapons to 1, everything else that stacks x20 |
| [AutoGrade](plugins/AutoGrade.cs) | `/grade wood\|stone\|metal\|hqm\|off`: new building blocks are placed at that grade, paid from the player's inventory |
| [MonumentPower](plugins/MonumentPower.cs) | Keeps monument lights on and fuse boxes filled |
| [HourlyStorm](plugins/HourlyStorm.cs) | Starts a storm every hour and clears it after 10 minutes |
| [ShortNights](plugins/ShortNights.cs) | Keeps the night but speeds it up (8 real minutes by default); the day keeps its normal length |
| [UnbreakableKit](plugins/UnbreakableKit.cs) | Gear from the chosen kits (default: `admin`) never loses condition while the owner has that kit's permission. Reads the item list from the Kits plugin's data |
| [NoNPC](plugins/NoNPC.cs) | Removes hostile NPCs and wild animals; keeps safe-zone guards, shopkeepers, turrets and horses (not active on the server right now) |
| [MapCheck](plugins/MapCheck.cs) | Records which monuments the map has, so the wipe script can re-roll maps without a Launch Site |
| [TunnelDwellerCap](plugins/TunnelDwellerCap.cs) | Caps tunnel dweller NPCs to save server CPU |
| [MemoryRestart](plugins/MemoryRestart.cs) | Daily restart, and a restart when memory use gets too high, with a chat countdown |
| [Welcome](plugins/Welcome.cs) | Welcome popup and chat message on join, kit reminders, online count. Set `OwnerId` to your SteamID64 to skip the popup for yourself |
| [InviteRewards](plugins/InviteRewards.cs) | `/invite` codes: a new player who enters a friend's code and plays 30 minutes earns a reward for both. Veterans can't use codes, and each player can earn from up to 20 friends |
| [NoBlueprints](plugins/NoBlueprints.cs) | Every player knows every blueprint from the start ("No BPs") |
| [MyMini](plugins/MyMini.cs) | `/mymini` spawns your own minicopter with 200 low grade, one per player, 10 minute cooldown; `/nomini` removes it |
| [ScrapShop](plugins/ScrapShop.cs) | `/shop`: a click-to-buy window with weapons, ammo, armor, meds, resources, base parts and raid gear, paid with scrap |
| [ServerEvents](plugins/ServerEvents.cs) | Every hour, taking turns: a locked crate at a big monument or a supply drop, announced with the map grid and marked on the map. `events.now crate\|drop` in the console |
| [FreeSkins](plugins/FreeSkins.cs) | `/skin`: pick any approved workshop skin for the item in your hand from a click window, free |
| [WipeEvent](plugins/WipeEvent.cs) | Changes the server name to `WIPE TONIGHT` / `JUST WIPED` around the wipe, and gives `/wipekit` once per player in the first 2 hours |
| [VoteRewards](plugins/VoteRewards.cs) | `/vote` and `/claimvote` for [rust-servers.net](https://rust-servers.net) votes. Put your server key and vote link in `oxide/config/VoteRewards.json` |
| [DiscordAnnounce](plugins/DiscordAnnounce.cs) | Posts "wipe tonight", "wipe in 2 hours / 15 minutes", "just wiped" and player-count milestones to a Discord channel. Put a webhook URL in `oxide/config/DiscordAnnounce.json` |

The server also runs these uMod plugins by other authors (not included here): Kits, Gather Manager, NTeleportation, Remover Tool, Backpacks, Furnace Splitter, Quick Sort.

# Merged Cells privacy policy

Last changed: 19 November 2026

This policy covers every build of Merged Cells: the playtest builds on this repository's releases and the builds on Google Play.

## Who's responsible

Merged Cells is made by DefinitelyNotZac, who decides how the data described here is used (the "data controller"). For questions, or to see or delete your data, email definitely.not.zac@proton.me.

## In short

- No accounts and no names.
- Nothing about how you play leaves your device unless you say yes to it, or export a file and send it yourself.
- You can change your answers, and delete what's been recorded, at any time in Settings → Privacy and data.

## What stays on your device

Your progress (your best number of moves on each level), your settings, your answers to the questions below, the last infinite-mode setup you played, and the levels you make or import. None of it is sent anywhere. A level's share code goes only where you paste it.

## Play data (optional)

If you say yes to "Help tune the puzzles?", the game records each map you play:

- the map itself, its par, and how hard the game rates it
- your moves and when you made them (counted in time spent playing, not the time of day), your undos and restarts, and whether you won, gave up or left the map
- when the map started (date and time, in UTC), the game's build name, and the platform (Android or Windows)
- for a campaign level, its number and whether you'd finished it before; for an infinite map, the settings you chose; for a custom level, the random ID the level was given and whether you imported it
- a random ID for the record itself, so a record sent twice is only counted once
- your random player ID (see below), and which version of the question you said yes to

It never includes your name, any level's name, your device's hardware or advertising IDs, your location or your contacts.

**Why:** to tune how hard the game's maps are, by seeing how players actually do on them.

**How it reaches the developer:** the game sends records over the internet to the game's server (see "Who else sees it"), whenever it's online, and only while your answer is yes. If you turn it off and later back on, the records made in between were never recorded, and the ones waiting are sent then. Like any server, it sees your device's IP address when it receives them. You can also export them yourself (Settings → Privacy and data → Export play data) and send the file.

**Legal basis:** your consent. Say no, or turn the switch off later, and nothing more is recorded or sent. Turning it off doesn't delete what's already on your device; "Delete my data" does.

## Leaderboards (optional)

If you say yes to "Join the leaderboards?", your best number of moves on each campaign level is sent to the game's server with your random player ID, so the game can show you where you sit among other players. Each score holds the level's number, a code for its layout (so a level that changes gets a fresh leaderboard), your best number of moves, your random player ID, and when it was sent. The spread of everyone's moves also helps tune how hard levels are.

**Legal basis:** your consent. Say no and leaderboards stay hidden: nothing is sent, and the game doesn't fetch them.

## Your random player ID

The game makes it the first time you say yes to either question. It's random: it isn't your device's hardware or advertising ID, and it isn't linked to any account. On its own it doesn't say who you are, but because it ties your records together it counts as personal data under UK and EU data protection law. "Delete my data" throws it away.

## Who else sees it

- The game's database is hosted by Supabase, in London (UK), which stores it for the developer and may not use it for anything else.
- To keep strangers from writing to the database, the game signs in to it anonymously: no name, email or account, just a sign-in the server makes for your game. That sign-in keeps the IP address your game last connected from. It's deleted with your data, and one that holds nothing is deleted within 30 days.
- Nobody else. Your data is never sold, and never used for advertising.
- An exported file goes wherever you send it.

## How long it's kept

- **On your device:** until you delete it with "Delete my data", or uninstall the game.
- **With the developer:** each play record is deleted two years after it was recorded. Leaderboard scores are kept while the leaderboards run, but a player ID that sends nothing for two years is deleted with everything under it. An email you send with an exported file is deleted once the file has been saved with the other records.
- **The server's own logs,** which hold IP addresses, are kept only for the hosting service's log period.

Anything is deleted sooner if you ask.

## Deleting your data

Settings → Privacy and data → Delete my data deletes the records on your device and your random player ID, and asks the server to delete everything sent under that ID, with the anonymous sign-in that sent it. If your device is offline, it asks again each time the game starts until the server has done it. Your answers stay as they are: if recording is still on, new records start under a new ID, and if "Share my scores" is still on, your best moves are shared again under the new ID. Turn them off first to stop that.

The button can't reach files you've already exported and sent. Email definitely.not.zac@proton.me from the address you sent them from, or say which file, and they'll be deleted within one month.

## Your rights

You can ask for a copy of your data (Export play data gives you your records), have it corrected or deleted, withdraw your consent, or object to its use. If you're unhappy with how your data is handled, you can complain to the Information Commissioner's Office (ico.org.uk) in the UK, or to your local data protection authority.

## Children

Merged Cells isn't meant for children under 13. If you're under 16, check with a parent before saying yes to either question. A parent who thinks their child has sent data can email definitely.not.zac@proton.me to have it deleted.

## Changes

If what's recorded, or where it goes, changes, the game asks you again before recording or sending anything more. The date at the top shows when this policy last changed.

## For playtesters

An exported file (`merged-cells-play-data-<date>.jsonl`) holds one line for each map you played while recording was on, with the details listed under "Play data". Your random player ID is on every line, so a file you email to the developer sits next to your email address. To have files you've sent deleted, email definitely.not.zac@proton.me from the same address, or say which file. Turning recording off, or pressing "Delete my data", can't reach files you've already sent.

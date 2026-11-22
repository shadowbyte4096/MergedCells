# Merged Cells privacy policy

Last changed: 22 November 2026

This policy covers every build of Merged Cells: the playtest builds on this repository's releases and the builds on Google Play.

## Who's responsible

Merged Cells is made by DefinitelyNotZac, who decides how the data described here is used (the "data controller"). For questions, or to see or delete your data, email definitely.not.zac@proton.me.

## In short

- No accounts and no names.
- Nothing about how you play leaves your device unless you say yes to it.
- You can change your answers, and delete what's been recorded, at any time in Settings → Privacy and data.

## What stays on your device

Your progress (your best number of moves on each level), your settings, your answers to the questions below, the last infinite-mode setup you played, the levels you make or import, and which of your levels you published to the level library. None of it is sent anywhere, except a level you publish (see "Level library"). A level's share code goes only where you paste it.

## Play data (optional)

If you say yes to "Help tune the puzzles?", the game records each map you play (a map you leave or skip before your first move isn't recorded):

- the map itself, its par, and how hard the game rates it
- your moves and when you made them (counted in time spent playing, not the time of day), your undos and restarts, and whether you won, gave up or left the map
- when the map started (date and time, in UTC), the game's build name, and the platform (Android or Windows)
- for a campaign level, its number and whether you'd finished it before; for an infinite map, the settings you chose; for a custom level, the random ID the level was given and whether you imported it
- a random ID for the record itself, so a record sent twice is only counted once
- your random player ID (see below), and which version of the question you said yes to

It never includes your name, any level's name, your device's hardware or advertising IDs, your location or your contacts.

**Why:** to tune how hard the game's maps are, by seeing how players actually do on them.

**How it reaches the developer:** the game sends records over the internet to the game's server (see "Who else sees it"), whenever it's online, and only while your answer is yes. If you turn it off and later back on, the records made in between were never recorded, and the ones waiting are sent then. Like any server, it sees your device's IP address when it receives them. Records wait on your device only until they're sent: once the server has all of them, the game deletes them from your device.

**Legal basis:** your consent. Say no, or turn the switch off later, and nothing more is recorded or sent. Turning it off doesn't delete what's already on your device; "Delete my data" does.

## Leaderboards (optional)

If you say yes to "Join the leaderboards?", your best number of moves on each campaign level is sent to the game's server with your random player ID, so the game can show you where you sit among other players. Other players see only how many players took each number of moves on a level, never your player ID. Each score holds the level's number, a code for its layout (so a level that changes gets a fresh leaderboard), your best number of moves, your random player ID, and when it was sent. The spread of everyone's moves also helps tune how hard levels are.

**Legal basis:** your consent. Say no and leaderboards stay hidden: nothing is sent, and the game doesn't fetch them. Turning "Share my scores" off sends nothing more, but the scores already sent stay in the counts until you delete your data.

## Level library (optional)

If you say yes to "Join the level library?" (which also accepts the [library's terms](TERMS.md)), the game uses the game's server to let you share levels with other players. With your random player ID, it sends and keeps:

- **levels you publish:** the level itself (as its share code), its name, its size, par, how hard the game rates it and a code for its layout, and when you published it
- **levels you beat:** which library levels you've beaten, once each, so the library can show how many players have beaten a level
- **likes:** which levels you like
- **reports:** which levels you report, the reason you chose and when
- **blocks:** whose levels you've blocked (the server works out who published a level; you never see their ID)
- **moderation:** whether each of your levels was hidden after reports, checked or removed, and whether you've been stopped from publishing. A level the developer removes, or one you remove while it's hidden for checking, leaves a placeholder with its size, par, how hard it's rated and its dates, so it can't be published again under the same ID.

Browsing the library sends your random player ID too, so the server can leave out levels by players you've blocked.

**Who sees it:** everyone who uses the library sees the levels you publish, with their names, and how many players have beaten and liked each level. Nobody else sees who published, beat, liked, reported or blocked anything. The developer sees reports, and the levels they're about, in a daily email, to decide whether a level breaks the terms. The developer may also use the levels you publish anywhere, as the terms allow (in the game itself, for example), but never with your player ID.

**Legal basis:** your consent. Say no and the library stays hidden: nothing is sent. Turning "Use the level library" off sends nothing more, but levels you published stay in the library until you remove them ("Remove from library" on a level, which is sent even while the library is off) or delete your data.

## Sending a level for the game

If you send the developer a level for the game (see "Sending a level for the game" in the [terms](TERMS.md)), they keep your message: the level's share code, your agreement, the name you'd like to be credited under, and the email address or account you sent it from. It's used only to decide whether to use the level, to credit you, and as the record of your permission. If the level, or one based on it, goes in the game, the name you chose is in the game's credits, unless you said you'd rather not be credited.

**Legal basis:** the agreement you make when you send the level.

## Discord

Settings and the level library have a link to the game's Discord server. Tapping it opens Discord or your browser. The game sends nothing to Discord, and nothing about you goes with the link. Discord is run under its own [privacy policy](https://discord.com/privacy) and terms, and it's for people aged 13 and over (older in some countries). Anything you post in the server is seen by the developer, the server's moderators and its other members. A level you send the developer there is kept as "Sending a level for the game" says.

## Your random player ID

The game makes it the first time you say yes to any of the questions. It's random: it isn't your device's hardware or advertising ID, and it isn't linked to any account. On its own it doesn't say who you are, but because it ties your records together it counts as personal data under UK and EU data protection law. With it, the server keeps when it first and last saw the ID, so an ID that goes unused can be deleted. Settings → Privacy and data shows it, with a button to copy it. "Delete my data" throws it away.

## Who else sees it

- The game's database is hosted by Supabase, in London (UK), which stores it for the developer and may not use it for anything else.
- To keep strangers from writing to the database, the game signs in to it anonymously: no name, email or account, just a sign-in the server makes for your game. That sign-in keeps the IP address your game last connected from. It's deleted with your data, and one that holds nothing is deleted within 30 days.
- Other players, if you share your scores: only how many players took each number of moves on a level, never your player ID.
- Other players, if you use the level library: the levels you publish, with their names, and how many players have beaten and liked each one, never your player ID or who beat, liked, reported or blocked anything.
- Resend, an email service in the United States, when a level is reported: the developer's daily email of reported levels holds each level's name, share code, ID and publish date, how many reports it has and when the last came in, and how many levels its author has in the library, never your player ID or who published or reported it. Resend's terms include standard data protection clauses for data sent to the United States.
- Proton, the developer's email service in Switzerland, holds any email you send the developer.
- Everyone who plays the game, if a level you sent goes in it and you asked to be credited: the name you chose, in the credits.
- Nobody else. Your data is never sold, and never used for advertising.

## How long it's kept

- **On your device:** play records until the server has them. Everything else until you delete it with "Delete my data", or uninstall the game.
- **With the developer:** each play record is deleted two years after it was recorded. A published level is kept until you remove it, apart from the placeholder described under "Level library". Everything else under your player ID (leaderboard scores, finishes, likes, blocks and the moderation details) is kept until you delete your data, or until the ID has gone two years unused, which deletes everything under it, published levels and placeholders included. Turning a switch off in Settings doesn't delete any of it. A report is deleted once the developer has checked the level, and after 90 days at most. After "Delete my data", the server keeps a scrambled form of the deleted ID (a hash) for 7 days, so anything still on its way under the old ID is refused. An email you send with an exported file is deleted once the file has been saved with the other records. A message you send with a level for the game is kept while the developer is deciding whether to use the level, and while the level, or one based on it, is in the game; if it isn't used, ask and the message is deleted.
- **The server's own logs,** which hold IP addresses and the game engine's version and operating system that each request carries, are kept only for the hosting service's log period.
- **Report emails** are kept by Resend only for its own log period.

Anything is deleted sooner if you ask.

## Deleting your data

Settings → Privacy and data → Delete my data deletes the records on your device and your random player ID, and asks the server to delete everything sent under that ID (including the levels you published and your finishes, likes, reports and blocks in the library), with the anonymous sign-in that sent it. A copy of a level that the developer already has under the library's terms isn't deleted, but nothing links it to you. If your device is offline, it asks again each time the game starts until the server has done it. Your answers stay as they are: if recording is still on, new records start under a new ID, and if "Share my scores" is still on, your best moves are shared again under the new ID. Turn them off first to stop that.

Earlier playtest builds could export play data to a file. The button can't reach a file you exported and sent: email definitely.not.zac@proton.me from the address you sent it from, or say which file, and it'll be deleted within one month.

## Your rights

You have the right to a copy of your data, to have it corrected or deleted, to restrict or object to its use, and to take it elsewhere, as a file. You can withdraw your consent at any time, which doesn't affect what was done before. Email definitely.not.zac@proton.me and you'll get an answer within one month.

There are no accounts, so the developer can only find your data by your random player ID. In the game, "Delete my data" does it for you. Otherwise, put your player ID in your email (Settings → Privacy and data shows it, with a button to copy it). If you uninstall the game without deleting your data, the ID goes with it: what was sent can't be linked to you any more, and it's deleted once the ID has gone two years unused.

If you're unhappy with how your data is handled, you can complain to the Information Commissioner's Office (ico.org.uk) in the UK, or to your local data protection authority.

## Children

Merged Cells isn't meant for children under 13. If you're under 16, check with a parent before saying yes to any of the questions. A parent who thinks their child has sent data can email definitely.not.zac@proton.me to have it deleted.

## Changes

If what's recorded, or where it goes, changes, the game asks you again before recording or sending anything more. The date at the top shows when this policy last changed.

## For playtesters

Earlier playtest builds had an "Export play data" button, which saved your records to a file (`merged-cells-play-data-<date>.jsonl`). It held one line for each map you played while recording was on, with the details listed under "Play data". Your random player ID is on every line, so a file you emailed to the developer sits next to your email address.

To have files you've sent deleted, email definitely.not.zac@proton.me from the same address, or say which file. Turning recording off, or pressing "Delete my data", can't reach files you've already sent. Builds since then send records to the server instead (see "Play data"), and have no Export button.

# ProjectHighrise - TD Enhancement Pack
A mod for Project Highrise that makes placing rooms easier - Also various other fixes I discovered were needed.

Should you use this mod? Check out the [trailer](https://streamable.com/dtqnzh) (aka. a tiny demo I made). Answer: Yes. Absolutely. Without question.


## To install: ##
1.
[Get the release](https://github.com/alextd/ProjectHighrise-BetterPlacement/releases)

EZ: unzip the release into Project Highrise folder.

OR: Use BepInEx (https://github.com/BepInEx/BepInEx , x86). Put TDEnhancementPack.dll in BepInEx/plugins

2. 
Verify that running the game creates BepInEx/LogOutput.log which contains a line "[Message:TD Enhancement Pack] Wake up!"

3.
You should re-bind Pause/Game speed keys since Keyboard commands feature uses number keys

## Features: ##
### Better Placement ###
- So you don't have to click literally hundreds of times in precise locations.
- In short: Use shift-click to copy that click-action where available. 
- Shift-click floors to fill floors with rooms.
- Shift-click tenants to move in all possible tenants.
- Shift-drag-click to place many floors full of wires.

### Keyboard commands ###
- To access build menus
  1. Press 1-4 to select section of bottom row
  2. Press number key select icon X within highlighted section
  3. Press number key to select and place a thing
  - Press F-Key to select tab, if any
- e.g. press 2-1-1 to build a small office.
- e.g. press 1-2-F4-1 to place water pipes
- e.g. Click apartment icon, press 1 to place studio
- So you should re-bind the number keys which are set to speed-up
- Recommend removing keybinds for bulldoze/elevator and reuse those keys for time speed-up 
- Press \` key to cancel ( It may be a little wonky as you're unexpectedly mid-combination )

### Save/Load actions for people ###
- Yes the game literally did not even try to save that info. It's not a bug, it simply wasn't a feature to save current action and script, and relied on them to re-start what they were doing - and even that didn't make any attempt to save progress, let alone the scripts that simply won't work when restarted, let alone the scripts that were programatically added and won't be restarted to begin with.
- e.g. Hotel guests who haven't checked in would turn around leave, workers building things would stop and go back to the office, before going back to work, which had progress wiped
  - TODO: People dining at restaurants isn't perfect - their timer is reset on load (since that info is stored outside the person and linked to them via a callback function which is hard to save to file)

### Notification popups ###
- Complaints, missing power, etc, show up immediately as a notification. Click to zoom to problem.
- Bookable performers at hotels shows a notification. Shift-click the notification to auto-book.

### Sorting ###
- Services sort by need and if you have them already
- Performers sort by availability

### Little things: ###
- Various dialogs are bigger, and scroll faster (I assume you have a 1080p+ screen)
- Allow relocation of units that consume dock resources, e.g. move a store when at max storage
- Zoom to mouse
- Smoother zooming with scroll wheel (Two scroll ticks zoom the same amount, whether or not second one comes too quickly)
- Pause on load.
- You can't select invisible people (ie offsite people next to the busstop, also elevator usage.)
- Removed IT Services 2-hour daily break. This is the only job that does that. They eat lunch all the same, anyway.
- Renamed Office Supply "Store" to "Service" BECAUSE THAT'S WHAT IT ACTUALLY IS
- Fixed bug where you could move in tenants over parking spot limit when paused


# To develop code (after BepInEx installed): #
1. Point to Steam folder (for Project Highrise installation path)
  - Edit SteamDir.user file. 
  - This file should not be commited back into the repo.
  - git update-index --skip-worktree SteamDir.user
2. Publicize Assembly-CSharp.dll so we can access the goddamn methods, okay?
  - Use Assembly Publicizer: https://github.com/CabbageCrow/AssemblyPublicizer
  - Turn: Project Highrise/Game_Data/Managed/Assembly-CSharp.dll
  - Into: TDEnhancementPack/Libs/publicized_assemblies/Assembly-CSharp_publicized.dll
3. Project Debug build puts dll into BepInEx/plugins folder. 
4. 'Start' launches Project Highrise

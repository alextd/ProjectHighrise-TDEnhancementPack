# ProjectHighrise-BetterPlacement
A mod for Project Highrise that makes placing rooms easier - Also various other fixes I discovered were needed.


## To install: ##
1. Use BepInEx (https://github.com/BepInEx/BepInEx). Todo: explain BepInEx. TL;DR Drop BepInEx into Project Highrise intallation.
2. Put BetterPlacement.dll in BepInEx/plugins
3. Verify that running the game creates BepInEx/LogOutput.log which contains a line "[Message:Better Placement] Wake up!"

## Features: ##
- Better Placement:
  - So you don't have to click literally hundreds of times in precise locations.
  - In short: Use shift-click to copy that click-action where available. Shift-click floors to fill floors with rooms. Shift-click tenants to move in all possible tenants.

- Save/Load actions for people:
  - Yes the game literally did not even try to save that info. It's not a bug, it simply wasn't a feature to save current action and script, and relied on them to re-start what they were doing - and even that didn't make any attempt to save progress, let alone the scripts that simply won't work when restarted, let alone the scripts that were programatically added and won't be restarted to begin with.
  - e.g. Hotel guests who haven't checked in would turn around leave, workers building things would stop and go back to the office, before going back to work, which had progress wiped
  - Save/Load people's current animation (wasn't so big a problem but now that their action is saved, their animation wasn't getting reset on load as they don't start new actions).

- Little things:
  - You can't select invisible people (ie offsite people next to the busstop, also elevator usage.)
  - Dialogs for move-in selection are bigger
    - and scroll faster.
    - More windows todo later
    - I assume you have a 1080p monitor


### To develop code (after BepInEx installed): ###
1. Point to Steam folder (for Project Highrise installation path)
  - Edit SteamDir.user file. 
  - This file should not be commited back into the repo.
  - git update-index --skip-worktree SteamDir.user
2. Publicize Assembly-CSharp.dll so we can access the goddamn methods, okay?
  - Use Assembly Publicizer: https://github.com/CabbageCrow/AssemblyPublicizer
  - Turn: Project Highrise/Game_Data/Managed/Assembly-CSharp.dll
  - Into: BetterPlacement/Libs/publicized_assemblies/Assembly-CSharp_publicized.dll
3. Project 'build' puts dll into BepInEx/plugins folder. 
4. 'Start' launches Project Highrise

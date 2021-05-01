# ProjectHighrise-BetterPlacement
A mod for Project Highrise that makes placing rooms easier, so you don't have to click literally hundreds of times in precise locations.


To install:
Use BepInEx, put BetterPlacement.dll in plugins. Todo: explain BepInEx. TL;DR Drop BepInEx into Project Highrise intallation.


To develop code (after BepInEx installed):
- Point to Steam folder (for Project Highrise installation path)
-- Edit SteamDir.user file. 
-- This file should not be commited back into the repo.
-- git update-index --skip-worktree SteamDir.user
- Publicize Assembly-CSharp.dll so we can access the goddamn methods, okay?
-- Use Assembly Publicizer: https://github.com/CabbageCrow/AssemblyPublicizer
-- Turn: Project Highrise/Game_Data/Managed/Assembly-CSharp.dll
-- Into: BetterPlacement/Libs/publicized_assemblies/Assembly-CSharp_publicized.dll
- Project builds dll into BepInEx/plugins folder. 
- 'Start' launches Project Highrise
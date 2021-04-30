# ProjectHighrise-BetterPlacement
A mod for Project Highrise that makes placing rooms easier, so you don't have to click literally hundreds of times in precise locations


To install:
Use BepInEx, put BetterPlacement.dll in plugins. Todo: explain BepInEx


To develop code:
- Point to Project Highrise intallation
-- Visual Studio, Open project properties
-- Reference Paths
-- Add path to Project Highrise\Game_Data\Managed\
- Publicize Assembly-CSharp.dll so we can access the goddamn methods, okay?
-- Use Assembly Publicizer: https://github.com/CabbageCrow/AssemblyPublicizer
-- Turn: Project Highrise/Game_Data/Managed/Assembly-CSharp.dll
-- Into: BetterPlacement/Libs/publicized_assemblies/Assembly-CSharp_publicized.dll
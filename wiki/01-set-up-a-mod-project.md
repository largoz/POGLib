# 01 Set up a mod project

A MelonLoader mod is a .NET 6 class library. You build it against the game's assemblies, which MelonLoader
generates, and against `POGLib.dll`.

## What you need

1. The **.NET SDK** 6 or newer.
2. The game with **MelonLoader 0.7** and **POGLib** installed through a mod manager
   ([r2modman](https://thunderstore.io/package/ebkr/r2modman/) or Gale): make a profile, install POGLib (it brings
   MelonLoader with it), start the game once. The first start takes a few minutes: MelonLoader generates the game's
   assemblies into the profile's `MelonLoader/Il2CppAssemblies` folder.
3. The profile folder. r2modman: Settings, "Browse profile folder". On Windows it is
   `%APPDATA%\r2modmanPlus-local\PitOfGoblin\profiles\<profile>`; Gale on Linux:
   `~/.local/share/com.kesomannen.gale/pit-of-goblin/profiles/<profile>`.

In that folder: `MelonLoader/net6` (MelonLoader itself), `MelonLoader/Il2CppAssemblies` (the game), and
`Mods/Largo-POGLib/POGLib.dll`.

## The project file

`BerryMod.csproj`; set `GameDir` to your profile folder:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net6.0</TargetFramework>
    <ImplicitUsings>disable</ImplicitUsings>
    <Nullable>disable</Nullable>
    <AssemblyName>BerryMod</AssemblyName>
    <GameDir>C:\Users\you\AppData\Roaming\r2modmanPlus-local\PitOfGoblin\profiles\Default</GameDir>
    <MelonDir>$(GameDir)\MelonLoader\net6</MelonDir>
    <InteropDir>$(GameDir)\MelonLoader\Il2CppAssemblies</InteropDir>
  </PropertyGroup>
  <ItemGroup>
    <Reference Include="POGLib"><HintPath>$(GameDir)\Mods\Largo-POGLib\POGLib.dll</HintPath><Private>false</Private></Reference>
    <Reference Include="MelonLoader"><HintPath>$(MelonDir)\MelonLoader.dll</HintPath><Private>false</Private></Reference>
    <Reference Include="Il2CppInterop.Runtime"><HintPath>$(MelonDir)\Il2CppInterop.Runtime.dll</HintPath><Private>false</Private></Reference>
    <Reference Include="Il2Cppmscorlib"><HintPath>$(InteropDir)\Il2Cppmscorlib.dll</HintPath><Private>false</Private></Reference>
    <Reference Include="UnityEngine.CoreModule"><HintPath>$(InteropDir)\UnityEngine.CoreModule.dll</HintPath><Private>false</Private></Reference>
    <Reference Include="Assembly-CSharp"><HintPath>$(InteropDir)\Assembly-CSharp.dll</HintPath><Private>false</Private></Reference>
  </ItemGroup>
</Project>
```

`Private=false` keeps those DLLs out of your build: the game and the mod manager already have them. A shop slot
needs no reference to POGShops, only its file ([20 Shops: your first slot](20-shops-your-first-slot.md)).

## Build and try

```
dotnet build -c Release
```

Copy `bin/Release/net6.0/BerryMod.dll` into the profile's `Mods` folder and start the game from the mod manager.
Your mod's lines and POGLib's appear in `MelonLoader/Latest.log`.

Next: [02 Your first item](02-your-first-item.md). A complete working mod to copy from: `samples/ShopDemo` in the
[repository](https://github.com/largoz/POGLib/tree/main/samples/ShopDemo).

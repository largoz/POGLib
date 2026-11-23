#!/usr/bin/env python3
"""wikicheck.py <wiki dir> <POGLib.dll> <game profile dir>

Compiles every ```csharp block of every wiki page against the given POGLib.dll, so a page never shows code that
does not build. A block with a class in it is compiled as it is; any other block is taken as members of a
ModItemDefinition initializer (`Visual = ...,`) and wrapped in one. Exit code = number of blocks that fail."""
import os, re, subprocess, sys, tempfile, pathlib

wiki, dll, profile = sys.argv[1], os.path.abspath(sys.argv[2]), os.path.abspath(sys.argv[3])
work = pathlib.Path(tempfile.mkdtemp(prefix="wikicheck-"))
WRAP = """using System.IO;
using POGLib.Items;
using UnityEngine;
namespace WikiCheck {{ static class Block{n} {{ static readonly string folder = ""; static void Use() {{
    var d = new ModItemDefinition
    {{
{body}
    }};
}} }} }}
"""
blocks = []
for page in sorted(pathlib.Path(wiki).glob("*.md")):
    for i, m in enumerate(re.finditer(r"```csharp\n(.*?)```", page.read_text(encoding="utf-8"), re.S)):
        code = m.group(1)
        n = len(blocks)
        src = code if re.search(r"\bclass\s", code) else WRAP.format(n=n, body=code)
        (work / f"Block{n}.cs").write_text(src, encoding="utf-8")
        blocks.append((page.name, i + 1, f"Block{n}.cs"))
proj = f"""<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup><TargetFramework>net6.0</TargetFramework><ImplicitUsings>disable</ImplicitUsings><Nullable>disable</Nullable>
    <OutputType>Library</OutputType><MelonDir>{profile}/MelonLoader/net6</MelonDir><InteropDir>{profile}/MelonLoader/Il2CppAssemblies</InteropDir></PropertyGroup>
  <ItemGroup>
    <Reference Include="POGLib"><HintPath>{dll}</HintPath><Private>false</Private></Reference>
    <Reference Include="MelonLoader"><HintPath>$(MelonDir)/MelonLoader.dll</HintPath><Private>false</Private></Reference>
    <Reference Include="Il2CppInterop.Runtime"><HintPath>$(MelonDir)/Il2CppInterop.Runtime.dll</HintPath><Private>false</Private></Reference>
    <Reference Include="Il2Cppmscorlib"><HintPath>$(InteropDir)/Il2Cppmscorlib.dll</HintPath><Private>false</Private></Reference>
    <Reference Include="UnityEngine.CoreModule"><HintPath>$(InteropDir)/UnityEngine.CoreModule.dll</HintPath><Private>false</Private></Reference>
    <Reference Include="Assembly-CSharp"><HintPath>$(InteropDir)/Assembly-CSharp.dll</HintPath><Private>false</Private></Reference>
  </ItemGroup>
</Project>"""
(work / "WikiCheck.csproj").write_text(proj, encoding="utf-8")
dotnet = os.path.expanduser("~/.dotnet/dotnet")
r = subprocess.run([dotnet, "build", str(work / "WikiCheck.csproj"), "-c", "Release", "-nologo", "-v", "q"],
                   capture_output=True, text=True, env={**os.environ, "DOTNET_ROOT": os.path.expanduser("~/.dotnet")})
failed = set()
for line in (r.stdout + r.stderr).splitlines():
    m = re.search(r"(Block\d+\.cs)\(\d+,\d+\): error (CS\d+: [^\[]*)", line)
    if m:
        f = m.group(1); page, i, _ = next(b for b in blocks if b[2] == f)
        if f not in failed: print(f"FAIL {page} block {i}: {m.group(2).strip()}")
        failed.add(f)
for page, i, f in blocks:
    if f not in failed: print(f"ok   {page} block {i}")
if r.returncode != 0 and not failed: print(r.stdout[-2000:]); sys.exit(1)
print(f"{len(blocks) - len(failed)} of {len(blocks)} block(s) build")
sys.exit(len(failed))

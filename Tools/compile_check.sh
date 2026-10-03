#!/usr/bin/env bash
# Kompilier-Check gegen die Unity-6.3-Referenz-DLLs (ohne Unity-Editor).
#   Tools/compile_check.sh <name> [Spieldateien...]
# Kompiliert Core + Menu/Options + die angegebenen Dateien aus Assets/Glamour/Scripts/Games.
# Fuer Spielklassen, die nicht angegeben sind, werden Platzhalter erzeugt. Ohne Dateiliste: alle Spiele.
set -e
NAME=${1:-all}; shift || true
ROOT=$(cd "$(dirname "$0")/.." && pwd)
U=${UNITY_DATA:-/tmp/claude-0/-home-user-glamour-games-windows/55bc5c9f-9405-5732-a176-e00fe5df9991/scratchpad/unity/Editor/Data}
DIR=$HOME/cc_$NAME; mkdir -p "$DIR"
declare -A CLS=( [Memory.cs]=MemoryGame [TicTacToe.cs]=TicTacToe [ConnectFour.cs]=ConnectFour [Battleship.cs]=Battleship [Snake.cs]=SnakeGame [Kniffel.cs]=Kniffel [Nim.cs]=Nim [Slot.cs]=SlotGame [Blackjack.cs]=Blackjack [Poker.cs]=Poker )
FILES=("$@")
if [ ${#FILES[@]} -eq 0 ]; then FILES=(Memory.cs TicTacToe.cs ConnectFour.cs Battleship.cs Snake.cs Kniffel.cs Nim.cs Slot.cs Blackjack.cs Poker.cs); fi
{
  echo "namespace GlamourGames {"
  for f in "${!CLS[@]}"; do
    skip=0; for g in "${FILES[@]}"; do [ "$g" = "$f" ] && skip=1; done
    [ $skip = 0 ] && echo " public class ${CLS[$f]} : Scene { public override void Draw(Canvas2D c){} }"
  done
  echo "}"
} > "$DIR/stubs.cs"
INC=""
for g in "${FILES[@]}"; do INC="$INC<Compile Include=\"$ROOT/Assets/Glamour/Scripts/Games/$g\" />"; done
cat > "$DIR/cc.csproj" <<XML
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup><TargetFramework>netstandard2.1</TargetFramework><LangVersion>9.0</LangVersion><Nullable>disable</Nullable><EnableDefaultCompileItems>false</EnableDefaultCompileItems><NoWarn>CS0414;CS0649;CS0169;CS0162;CS0219;CS0168</NoWarn></PropertyGroup>
  <ItemGroup><Reference Include="$U/Managed/UnityEngine/UnityEngine.*Module.dll" /></ItemGroup>
  <ItemGroup>
    <Compile Include="stubs.cs" />
    <Compile Include="$ROOT/Assets/Glamour/Scripts/Core/**/*.cs" />
    <Compile Include="$ROOT/Assets/Glamour/Scripts/Games/Menu.cs" />
    <Compile Include="$ROOT/Assets/Glamour/Scripts/Games/Options.cs" />
    $INC
  </ItemGroup>
</Project>
XML
cd "$DIR" && ~/.dotnet/dotnet build -nologo -v q 2>&1 | grep -E "error|warn CS|Build succeeded" | sed "s|$ROOT/||" | sort -u

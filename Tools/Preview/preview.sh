#!/usr/bin/env bash
# Rendert eine Szene ohne Unity als PNG.  Beispiele:
#   Tools/Preview/preview.sh --scene=menu --t=2 --out=/tmp/menu.png
#   Tools/Preview/preview.sh --scene=1 --t=3 --script="c,700,620@1" --out=/tmp/ttt.png
set -e
HERE=$(cd "$(dirname "$0")" && pwd); ROOT=$(cd "$HERE/../.." && pwd)
CACHE=${TMPDIR:-/tmp}/glamour_preview_cache; mkdir -p "$CACHE"
if [ ! -f "$CACHE/font.raw" ] || [ "$ROOT/Assets/Resources/Glamour/font_atlas.png.bytes" -nt "$CACHE/font.raw" ] || [ "$ROOT/Assets/Resources/Glamour/img_atlas.png.bytes" -nt "$CACHE/img.raw" ]; then
  python3 - "$ROOT" "$CACHE" <<'PY'
import sys, numpy as np
from PIL import Image
root, cache = sys.argv[1], sys.argv[2]
np.asarray(Image.open(root+'/Assets/Resources/Glamour/font_atlas.png.bytes').convert('L')).tofile(cache+'/font.raw')
np.asarray(Image.open(root+'/Assets/Resources/Glamour/img_atlas.png.bytes').convert('RGBA')).tofile(cache+'/img.raw')
PY
fi
B=$HOME/.glamour_preview_build_${PREVIEW_ONLY:-all}; B=${B%.cs}; mkdir -p "$B"
declare -A CLS=( [Memory.cs]=MemoryGame [TicTacToe.cs]=TicTacToe [ConnectFour.cs]=ConnectFour [Battleship.cs]=Battleship [Snake.cs]=SnakeGame [Kniffel.cs]=Kniffel [Nim.cs]=Nim [Slot.cs]=SlotGame [Blackjack.cs]=Blackjack [Poker.cs]=Poker )
INC=""; { echo "namespace GlamourGames {"
for f in "${!CLS[@]}"; do
  if [ -f "$ROOT/Assets/Glamour/Scripts/Games/$f" ] && [ -z "${PREVIEW_STUB:-}" -o "${PREVIEW_ONLY:-$f}" = "$f" ]; then INC="$INC<Compile Include=\"$ROOT/Assets/Glamour/Scripts/Games/$f\" />"
  else echo " public class ${CLS[$f]} : Scene { public override void Draw(Canvas2D c){ Gfx.Text(c, \"(noch nicht portiert)\", 800, 450, 40, Col.White); } }"; fi
done; echo "}"; } > "$B/stubs.cs"
cat > "$B/Preview.csproj" <<XML
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net8.0</TargetFramework><LangVersion>9.0</LangVersion><Nullable>disable</Nullable><EnableDefaultCompileItems>false</EnableDefaultCompileItems><NoWarn>CS0414;CS0649;CS0169;CS0162;CS0219;CS0168;CS8632</NoWarn><InvariantGlobalization>true</InvariantGlobalization></PropertyGroup>
  <ItemGroup>
    <Compile Include="$HERE/FakeUnity.cs" /><Compile Include="$HERE/Preview.cs" /><Compile Include="stubs.cs" />
    <Compile Include="$ROOT/Assets/Glamour/Scripts/Core/**/*.cs" Exclude="$ROOT/Assets/Glamour/Scripts/Core/App.cs;$ROOT/Assets/Glamour/Scripts/Core/Sfx.cs" />
    <Compile Include="$ROOT/Assets/Glamour/Scripts/Games/Menu.cs" /><Compile Include="$ROOT/Assets/Glamour/Scripts/Games/Options.cs" />
    $INC
  </ItemGroup>
</Project>
XML
cd "$B" && ~/.dotnet/dotnet build -nologo -v q -c Release -o out 2>&1 | grep -E " error |rror\(s\)" | sed "s|$ROOT/||" | sort -u | head -30
cd "$OLDPWD"; GLAMOUR_ROOT="$ROOT" TMPDIR=${TMPDIR:-/tmp} ~/.dotnet/dotnet "$B/out/Preview.dll" "$@"

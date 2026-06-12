#!/usr/bin/env bash
# WinOverlay macOS 로컬 빌드 (본인용 — 서명/공증 없음).
#
# 전제: macOS + .NET 8 SDK (https://dotnet.microsoft.com/download/dotnet/8.0)
# 사용: ./tools/build-macos.sh            # Apple Silicon (기본)
#       ./tools/build-macos.sh osx-x64    # Intel Mac
#
# 산출물: publish-macos/WinOverlay  (실행 파일, 더블클릭 또는 ./WinOverlay)

set -euo pipefail

RID="${1:-osx-arm64}"
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
PROJ="$ROOT/src/OverlayApp.Avalonia/OverlayApp.Avalonia.csproj"
OUT="$ROOT/publish-macos"

echo "==== WinOverlay macOS build ($RID) ===="

rm -rf "$OUT"

dotnet publish "$PROJ" \
    -c Release \
    -f net8.0 \
    -r "$RID" \
    --self-contained true \
    -p:PublishSingleFile=true \
    -p:DebugType=embedded \
    -o "$OUT"

chmod +x "$OUT/WinOverlay"

echo ""
echo "==== 완료 ===="
echo "실행: $OUT/WinOverlay"
echo ""
echo "참고:"
echo " - 본인 머신에서 직접 빌드한 바이너리라 Gatekeeper 공증이 필요 없습니다."
echo " - 전역 단축키는 macOS 미구현 — 트레이 메뉴로 토글하세요."
echo " - CPU/GPU 온도 지표는 macOS 미지원입니다(메모리는 표시)."

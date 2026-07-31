#!/usr/bin/env bash

set -euo pipefail

cd "$(dirname "$0")"

rebuild_sheet() {
  local expected_sha="$1"
  local output="$2"
  shift 2

  local temporary
  temporary="$(mktemp "${output%.png}.XXXXXX.png")"

  # ImageMagick 7.1.2 writes the correct unlabeled montage but returns 1 when
  # its empty default label cannot resolve a font. Accept that warning only
  # when the newly rendered file matches the reviewed output byte-for-byte.
  if ! magick montage "$@" "$temporary"; then
    local actual_sha
    actual_sha="$(shasum -a 256 "$temporary" | awk '{print $1}')"
    if [[ "$actual_sha" != "$expected_sha" ]]; then
      rm -f "$temporary"
      return 1
    fi
  fi

  mv "$temporary" "$output"
}

# Array order is the row-major cell order in each unlabeled review sheet.
buildings=(
  Buildings/Hisar_Complete.png
  Buildings/Hisar_Destroyed.png
  Buildings/House_Complete.png
  Buildings/House_Foundation.png
  Buildings/House_HalfBuilt.png
  Buildings/House_Rubble.png
  Buildings/Storehouse_Complete.png
  Buildings/Storehouse_Foundation.png
  Buildings/Storehouse_HalfBuilt.png
  Buildings/Storehouse_Rubble.png
  Buildings/Watchtower_Complete.png
  Buildings/Watchtower_Foundation.png
  Buildings/Watchtower_HalfBuilt.png
  Buildings/Watchtower_Rubble.png
)

resources=(
  Resources/Cache_Empty.png
  Resources/Cache_Full.png
  Resources/Cache_Low.png
  Resources/Supply_Sacks.png
  Resources/Timber_Bundle.png
  Resources/Tool_Bundle.png
  Resources/Trade_Chest.png
)

environment=(
  Environment/Broken_Caravan_Cart.png
  Environment/Caravan_Debris.png
  Environment/Dry_Grass.png
  Environment/Dry_Scrub.png
  Environment/Fallen_Masonry_Piles.png
  Environment/Ground_Road_Materials.png
  Environment/Highland_Pines.png
  Environment/Large_Rock_Clusters.png
  Environment/Roadside_Markers.png
  Environment/Ruined_Wall_Corner_End.png
  Environment/Ruined_Wall_Straight.png
  Environment/Small_Dark_Rocks.png
)

rebuild_sheet a36ac5fe5b369fc781e1a75affe87b3c5289e94e168e5bcb0e5e167a17d6e79d \
  Buildings_ContactSheet.png "${buildings[@]}" -thumbnail 400x300 -tile 4x4 \
  -geometry 400x300+8+8 -background '#c9bba9'
rebuild_sheet 6c44a354f68e497d14f2fecaabb3aeedd81d28bed6b11e0347959f4a1f7e1050 \
  Resources_ContactSheet.png "${resources[@]}" -thumbnail 400x300 -tile 4x2 \
  -geometry 400x300+8+8 -background '#c9bba9'
rebuild_sheet 873ab343325d385b507bc57ce24f0b9f720b014a6d731ea42b7fd503e6837812 \
  Environment_ContactSheet.png "${environment[@]}" -thumbnail 400x300 -tile 4x3 \
  -geometry 400x300+8+8 -background '#c9bba9'

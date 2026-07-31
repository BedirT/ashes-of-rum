#!/usr/bin/env bash

set -euo pipefail

cd "$(dirname "$0")"

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

magick montage "${buildings[@]}" -thumbnail 400x300 -tile 4x4 \
  -geometry 400x300+8+8 -background '#c9bba9' Buildings_ContactSheet.png
magick montage "${resources[@]}" -thumbnail 400x300 -tile 4x2 \
  -geometry 400x300+8+8 -background '#c9bba9' Resources_ContactSheet.png
magick montage "${environment[@]}" -thumbnail 400x300 -tile 4x3 \
  -geometry 400x300+8+8 -background '#c9bba9' Environment_ContactSheet.png

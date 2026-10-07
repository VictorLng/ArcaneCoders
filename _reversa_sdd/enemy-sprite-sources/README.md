# Enemy sprite sources

Nine transparent PNG source sheets generated from the visual language of `Assets/Resources/Characters/Mage/mage-directions.png`.

Every sheet is laid out horizontally in this order: front, back, left, right. The files are kept at their original generation size to preserve pixel detail before game-specific cropping and resizing.

The project configuration currently permits writes only to `Assets/ArcaneCode/**/*.cs`; therefore these source images are intentionally not placed in `Assets/Resources` yet. To integrate them, permit `Assets/Resources/Characters/Enemies/**` in `.reversa/reversa-config.json`, then copy, crop, and resize them to the game's desired 4 x 128 x 160 sprite-sheet format.

Included enemies: skeleton, bat, goblin, rat, mini mage, spider, black knight, flaming headless knight, and basilisk frog.

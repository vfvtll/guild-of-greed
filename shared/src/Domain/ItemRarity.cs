namespace GuildOfGreed.Shared.Domain;

// Уровень редкости предмета. Влияет на:
//   - цвет рамки в инвентаре (см. UIStyle.RarityColor)
//   - цвет лога при выпадении
//   - количество аффиксов на предмете (Common = 0П+1С, Legendary = 3П+3С — см.
//     AffixGenerator / .claude_design_items.md и Affix tables в Инк. B)
//   - шанс выпадения по уровню грейда (см. ItemGrade.AllowedRarities)
//
// ВАЖНО: значения int сохраняются в JSON-сейвах и передаются по wire (через
// поля Rarity в ArmorData/WeaponData). Новые значения только в конец, старые
// не переупорядочивать — иначе старые сейвы будут разэнумерированы.
public enum ItemRarity
{
	Common    = 0,
	Uncommon  = 1,
	Rare      = 2,
	Heroic    = 3,   // Между Rare и Epic. И6.2.
	Epic      = 4,
	Legendary = 5,   // Высшая. И6.2.
}

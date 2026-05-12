namespace GuildOfGreed.Shared.Domain;

// Уникальные пассивные эффекты оружия. Каркас — реальная игровая логика
// ещё не подключена. Согласовано 2026-05-12:
//   - knife              — повышенный crit (chance/damage).
//   - sword_1h, sword_2h — шанс наложить bleeding effect при ударе.
//   - staff              — без уник.эффекта (магия покрыта статами).
//   - в будущем          — элементальный урон/резисты (light/dark/poison)
//                          через отдельные AffixStatKind или собственный enum.
//
// Сейчас этот enum заполняется в WeaponData.Passives при определении оружия
// в ItemsDB, но никакой engine-логики (бой, описания) к нему не подключено.
// Это сделано отдельным инкрементом, когда понадобятся реальные эффекты.
//
// ID сохраняются в JSON, поэтому новые значения добавлять ТОЛЬКО в конец
// (см. CODING_STANDARDS §11 для аналогичной защиты в enum'ах).
public enum WeaponPassive
{
	None         = 0,
	CritBonus    = 1,   // +шанс/+урон крита. Используется ножами.
	BleedOnHit   = 2,   // Шанс bleeding-эффекта при физ.ударе. Мечи.
	// TODO: ManaLeechOnHit, ElementalLight, ElementalDark, ElementalPoison, ...
}

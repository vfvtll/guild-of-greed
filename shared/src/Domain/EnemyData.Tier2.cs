namespace GuildOfGreed.Shared.Domain;

// Враги двух длинных подземелий, доступных с 5 уровня персонажа.
// Это всё ещё первый-второй грейд-полоса (E → ранний D), поэтому лор сугубо
// мирской: лесные звери, разбойники, каторжники. Никаких личей/призраков/
// командиров — это позже (см. project_world_tone_grades в памяти).
//
//   локация 3 — "Звериная балка": звери глухой чащи. Длинная, но без магии
//     высоких порядков; шаман шайки и ведунья — единственный кастер. Босс —
//     просто бурый медведь, сильный, но абсолютно мирской.
//   локация 4 — "Разбойничья застава": бандиты, каторжники, наёмники. Самое
//     длинное подземелье; босс — главарь шайки, бывший наёмник, ничего более.
//
// Калибровка по HP/урону — против перса 5–7 ур.: рядовой враг 90-160 HP,
// средний 180-220 HP, толстяк до 250 HP. Удары лежат в 22-50. Босс — 380-560 HP
// и поднимает удар до ~50, чтобы перс с MagDef=0 / 18 PhysDef к концу забега
// действительно мог не дотянуть.
public partial class EnemyData
{
	// =========================================================================
	// Локация 3 — "Звериная балка" (8 рядов карты).
	// Зверьё чащи + одна лесная ведунья. Из мобов магией бьёт только ведунья;
	// остальные — чистая физика. Звери крепче волков из первой локации,
	// но всё ещё просто звери.
	// =========================================================================

	// Бурый волк — крупнее обычного волка, но это всё ещё волк.
	public static EnemyData CreateBrownWolf()
	{
		var e = new EnemyData
		{
			EnemyName = "Бурый волк",
			MaxHp = 120, CurrentHp = 120,
			PhysDef = 3, MagicDef = 0,
			MoneyMin = 2, MoneyMax = 6,
		};
		e.Intents.Add(new Intent { Type = "attack", Amount = 24, Name = "Укус",            WeaponType = null });
		e.Intents.Add(new Intent { Type = "attack", Amount = 34, Name = "Рывок-укус",      WeaponType = null });
		e.Intents.Add(new Intent { Type = "block",  Amount = 10, Name = "Скалит зубы" });
		e.LootTable.Add(new LootEntry { ItemId = "potion_hp_small",  Chance = 0.40f });
		e.LootTable.Add(new LootEntry { ItemId = "potion_hp_medium", Chance = 0.10f });
		return e;
	}

	// Дикий вепрь — толстая туша, бьёт клыками и тараном.
	public static EnemyData CreateWildBoar()
	{
		var e = new EnemyData
		{
			EnemyName = "Дикий вепрь",
			MaxHp = 180, CurrentHp = 180,
			PhysDef = 5, MagicDef = 0,
			MoneyMin = 3, MoneyMax = 7,
		};
		e.Intents.Add(new Intent { Type = "attack", Amount = 28, Name = "Удар клыками",   WeaponType = null });
		e.Intents.Add(new Intent { Type = "attack", Amount = 40, Name = "Лобовой таран",  WeaponType = null });
		e.Intents.Add(new Intent { Type = "block",  Amount = 8,  Name = "Опускает голову" });
		e.LootTable.Add(new LootEntry { ItemId = "potion_hp_small",  Chance = 0.50f });
		e.LootTable.Add(new LootEntry { ItemId = "potion_hp_medium", Chance = 0.18f });
		return e;
	}

	// Росомаха — быстрая, бешеная, рвёт пакостно. Низкие HP, высокий выпад.
	public static EnemyData CreateWolverine()
	{
		var e = new EnemyData
		{
			EnemyName = "Лохматая росомаха",
			MaxHp = 90, CurrentHp = 90,
			PhysDef = 2, MagicDef = 0,
			MoneyMin = 2, MoneyMax = 5,
		};
		e.Intents.Add(new Intent { Type = "attack", Amount = 26, Name = "Когтистый рывок",  WeaponType = null });
		e.Intents.Add(new Intent { Type = "attack", Amount = 36, Name = "Разрывающий укус", WeaponType = null });
		e.Intents.Add(new Intent { Type = "block",  Amount = 6,  Name = "Жмётся к земле" });
		e.LootTable.Add(new LootEntry { ItemId = "potion_hp_small", Chance = 0.40f });
		e.LootTable.Add(new LootEntry { ItemId = "potion_mp_small", Chance = 0.20f });
		return e;
	}

	// Лесная ведунья — единственный кастер пула. Не "тёмная неофит", а просто
	// деревенская колдунья, живущая в чаще: травы, наговоры, испуг скота.
	// На воина с MagDef=0 удар всё равно проходит в полную силу.
	public static EnemyData CreateForestWitch()
	{
		var e = new EnemyData
		{
			EnemyName = "Лесная ведунья",
			MaxHp = 90, CurrentHp = 90,
			PhysDef = 1, MagicDef = 3,
			MoneyMin = 4, MoneyMax = 9,
		};
		e.Intents.Add(new Intent { Type = "attack_magic", Amount = 28, Name = "Наговор",         WeaponType = null });
		e.Intents.Add(new Intent { Type = "attack_magic", Amount = 38, Name = "Травяной ожог",   WeaponType = null });
		e.Intents.Add(new Intent { Type = "block",        Amount = 10, Name = "Бормочет оберег" });
		e.LootTable.Add(new LootEntry { ItemId = "potion_mp_medium", Chance = 0.35f });
		e.LootTable.Add(new LootEntry { ItemId = "potion_hp_small",  Chance = 0.30f });
		e.LootTable.Add(new LootEntry { ItemId = "ring_focus_low",   Chance = 0.15f, Affixed = true });
		e.LootTable.Add(new LootEntry { ItemId = "potion_focus",     Chance = 0.08f });
		return e;
	}

	// Беглый каторжник — мирской человек с силой взрослого мужика. Не громила,
	// но рукам своим хозяин. Тематически тут он одиночка, забрёл в балку.
	public static EnemyData CreateRunawayConvict()
	{
		var e = new EnemyData
		{
			EnemyName = "Беглый каторжник",
			MaxHp = 140, CurrentHp = 140,
			PhysDef = 2, MagicDef = 0,
			MoneyMin = 4, MoneyMax = 10,
		};
		e.Intents.Add(new Intent { Type = "attack", Amount = 30, Name = "Удар обухом",      WeaponType = null });
		e.Intents.Add(new Intent { Type = "attack", Amount = 40, Name = "Рваная атака",     WeaponType = null });
		e.Intents.Add(new Intent { Type = "block",  Amount = 12, Name = "Прикрывается курткой" });
		e.LootTable.Add(new LootEntry { ItemId = "potion_hp_small",  Chance = 0.45f });
		e.LootTable.Add(new LootEntry { ItemId = "potion_hp_medium", Chance = 0.12f });
		e.LootTable.Add(new LootEntry { ItemId = "dagger_low",       Chance = 0.15f, Affixed = true });
		return e;
	}

	// Босс балки — бурый медведь. Сильнейший лесной зверь этого края, но
	// всё ещё именно медведь: не "магический страж", не "владыка чащи".
	// 380 HP против ~55 dpt = ~7 ходов; удары до 50 phys и блок 20. Магии нет —
	// игрок с PhysDef 18 половину поглощает, но всё равно теряет 25-32 за хит.
	public static EnemyData CreateBrownBear()
	{
		var e = new EnemyData
		{
			EnemyName = "Бурый медведь",
			MaxHp = 380, CurrentHp = 380,
			PhysDef = 7, MagicDef = 0,
			HpRegen = 3,
			MoneyMin = 20, MoneyMax = 45,
		};
		e.Intents.Add(new Intent { Type = "attack", Amount = 38, Name = "Удар лапой",    WeaponType = null });
		e.Intents.Add(new Intent { Type = "attack", Amount = 50, Name = "Сшибающий удар",WeaponType = null });
		e.Intents.Add(new Intent { Type = "attack", Amount = 30, Name = "Укус",          WeaponType = null });
		e.Intents.Add(new Intent { Type = "block",  Amount = 20, Name = "Встаёт на дыбы" });
		e.LootTable.Add(new LootEntry { ItemId = "amulet_might_low", Chance = 1.00f, Affixed = true });
		e.LootTable.Add(new LootEntry { ItemId = "ring_power_low",   Chance = 1.00f, Affixed = true });
		e.LootTable.Add(new LootEntry { ItemId = "potion_full",      Chance = 1.00f });
		e.LootTable.Add(new LootEntry { ItemId = "potion_hp_medium", Chance = 1.00f });
		e.LootTable.Add(new LootEntry { ItemId = "potion_strength",  Chance = 0.60f });
		return e;
	}

	// =========================================================================
	// Локация 4 — "Разбойничья застава" (10 рядов карты, самое длинное).
	// Бандиты, каторжники, наёмники — всё мирское. Один кастер — знахарь шайки.
	// Босс — главарь, бывший наёмник; никаких командиров и капитанов.
	// =========================================================================

	// Разбойник-ветеран — крепкий мужик с мечом, опытнее старшего разбойника
	// с локации 0.
	public static EnemyData CreateBanditVeteran()
	{
		var e = new EnemyData
		{
			EnemyName = "Разбойник-ветеран",
			MaxHp = 150, CurrentHp = 150,
			PhysDef = 4, MagicDef = 1,
			MoneyMin = 4, MoneyMax = 10,
		};
		e.Intents.Add(new Intent { Type = "attack", Amount = 30, Name = "Удар мечом",        WeaponType = "sword_1h" });
		e.Intents.Add(new Intent { Type = "attack", Amount = 40, Name = "Сокрушающий замах", WeaponType = "sword_1h" });
		e.Intents.Add(new Intent { Type = "block",  Amount = 16, Name = "Боевая стойка" });
		e.LootTable.Add(new LootEntry { ItemId = "potion_hp_small",   Chance = 0.55f });
		e.LootTable.Add(new LootEntry { ItemId = "potion_hp_medium",  Chance = 0.15f });
		e.LootTable.Add(new LootEntry { ItemId = "potion_strength",   Chance = 0.10f });
		e.LootTable.Add(new LootEntry { ItemId = "ring_power_low",    Chance = 0.18f, Affixed = true });
		e.LootTable.Add(new LootEntry { ItemId = "shield_physical_low",Chance = 0.10f });
		return e;
	}

	// Наёмник — двуручник, бьёт сильно но размашисто. Простой служивый.
	public static EnemyData CreateMercenary()
	{
		var e = new EnemyData
		{
			EnemyName = "Наёмник",
			MaxHp = 180, CurrentHp = 180,
			PhysDef = 5, MagicDef = 1,
			MoneyMin = 5, MoneyMax = 12,
		};
		e.Intents.Add(new Intent { Type = "attack", Amount = 34, Name = "Маховой удар",  WeaponType = "sword_2h" });
		e.Intents.Add(new Intent { Type = "attack", Amount = 46, Name = "Колющий рывок", WeaponType = "sword_2h" });
		e.Intents.Add(new Intent { Type = "block",  Amount = 14, Name = "Опирается на меч" });
		e.LootTable.Add(new LootEntry { ItemId = "potion_hp_medium", Chance = 0.35f });
		e.LootTable.Add(new LootEntry { ItemId = "potion_hp_small",  Chance = 0.30f });
		e.LootTable.Add(new LootEntry { ItemId = "amulet_might_low", Chance = 0.18f, Affixed = true });
		e.LootTable.Add(new LootEntry { ItemId = "potion_strength",  Chance = 0.10f });
		return e;
	}

	// Закованный каторжник — мужик в цепях, толстый, но прикованный. Никакого
	// "латного стража" — это просто связанный каторжник с тяжёлыми кулаками.
	public static EnemyData CreateChainedConvict()
	{
		var e = new EnemyData
		{
			EnemyName = "Закованный каторжник",
			MaxHp = 240, CurrentHp = 240,
			PhysDef = 9, MagicDef = 1,
			MoneyMin = 5, MoneyMax = 13,
		};
		e.Intents.Add(new Intent { Type = "attack", Amount = 26, Name = "Удар кулаком",  WeaponType = null });
		e.Intents.Add(new Intent { Type = "attack", Amount = 34, Name = "Удар цепью",    WeaponType = null });
		e.Intents.Add(new Intent { Type = "block",  Amount = 22, Name = "Сжался в комок" });
		e.LootTable.Add(new LootEntry { ItemId = "potion_hp_medium",    Chance = 0.40f });
		e.LootTable.Add(new LootEntry { ItemId = "shield_balanced_low", Chance = 0.16f });
		e.LootTable.Add(new LootEntry { ItemId = "shield_physical_low", Chance = 0.15f });
		e.LootTable.Add(new LootEntry { ItemId = "ring_blessed_low",    Chance = 0.10f, Affixed = true });
		return e;
	}

	// Каторжник-берсерк — стеклянная пушка-физика. Не "тёмный", не "проклятый",
	// просто потерявший рассудок мужик.
	public static EnemyData CreateBerserkConvict()
	{
		var e = new EnemyData
		{
			EnemyName = "Каторжник-берсерк",
			MaxHp = 130, CurrentHp = 130,
			PhysDef = 1, MagicDef = 0,
			MoneyMin = 3, MoneyMax = 9,
		};
		e.Intents.Add(new Intent { Type = "attack", Amount = 42, Name = "Безумный замах",  WeaponType = null });
		e.Intents.Add(new Intent { Type = "attack", Amount = 54, Name = "Двойной удар",    WeaponType = null });
		e.Intents.Add(new Intent { Type = "attack", Amount = 36, Name = "Кулак в лицо",    WeaponType = null });
		e.LootTable.Add(new LootEntry { ItemId = "potion_hp_small",  Chance = 0.40f });
		e.LootTable.Add(new LootEntry { ItemId = "potion_strength",  Chance = 0.20f });
		e.LootTable.Add(new LootEntry { ItemId = "ring_power_low",   Chance = 0.15f, Affixed = true });
		return e;
	}

	// Лучник засады — дальний боец шайки, лёгкий и точечный.
	public static EnemyData CreateAmbushArcher()
	{
		var e = new EnemyData
		{
			EnemyName = "Лучник засады",
			MaxHp = 100, CurrentHp = 100,
			PhysDef = 2, MagicDef = 1,
			MoneyMin = 3, MoneyMax = 8,
		};
		e.Intents.Add(new Intent { Type = "attack", Amount = 26, Name = "Выстрел",            WeaponType = null });
		e.Intents.Add(new Intent { Type = "attack", Amount = 36, Name = "Стрела с подкрутом", WeaponType = null });
		e.Intents.Add(new Intent { Type = "block",  Amount = 10, Name = "Уходит за укрытие" });
		e.LootTable.Add(new LootEntry { ItemId = "potion_hp_small", Chance = 0.40f });
		e.LootTable.Add(new LootEntry { ItemId = "potion_mp_small", Chance = 0.25f });
		e.LootTable.Add(new LootEntry { ItemId = "dagger_low",      Chance = 0.15f, Affixed = true });
		return e;
	}

	// Знахарь шайки — деревенский колдун при бандитах. Простой травник-кастер.
	// Никаких "полевых колдунов" / "тёмных жрецов" — это просто знахарь, как
	// у гоблинов есть шаман.
	public static EnemyData CreateGangHealer()
	{
		var e = new EnemyData
		{
			EnemyName = "Знахарь шайки",
			MaxHp = 115, CurrentHp = 115,
			PhysDef = 2, MagicDef = 4,
			MoneyMin = 5, MoneyMax = 12,
		};
		e.Intents.Add(new Intent { Type = "attack_magic", Amount = 32, Name = "Заговор-удар", WeaponType = null });
		e.Intents.Add(new Intent { Type = "attack_magic", Amount = 44, Name = "Кипящая кровь",WeaponType = null });
		e.Intents.Add(new Intent { Type = "block",        Amount = 14, Name = "Бормочет оберег" });
		e.LootTable.Add(new LootEntry { ItemId = "potion_mp_medium",   Chance = 0.40f });
		e.LootTable.Add(new LootEntry { ItemId = "potion_focus",       Chance = 0.18f });
		e.LootTable.Add(new LootEntry { ItemId = "ring_focus_low",     Chance = 0.20f, Affixed = true });
		return e;
	}

	// Босс — главарь шайки. Бывший наёмник, отколовшийся со своими; добротный
	// двуручник, опыт боя, ничего сверхъестественного. Сильнее старшего
	// разбойника с локации 0 в разы (560 HP против 180), но это всё ещё просто
	// мужик с мечом. Никаких командиров и капитанов.
	public static EnemyData CreateGangLeader()
	{
		var e = new EnemyData
		{
			EnemyName = "Главарь шайки",
			MaxHp = 560, CurrentHp = 560,
			PhysDef = 8, MagicDef = 3,
			HpRegen = 2,
			MoneyMin = 40, MoneyMax = 80,
		};
		e.Intents.Add(new Intent { Type = "attack", Amount = 40, Name = "Удар двуручником",  WeaponType = "sword_2h" });
		e.Intents.Add(new Intent { Type = "attack", Amount = 52, Name = "Сокрушающий замах", WeaponType = "sword_2h" });
		e.Intents.Add(new Intent { Type = "attack", Amount = 32, Name = "Колено в живот",    WeaponType = null });
		e.Intents.Add(new Intent { Type = "block",  Amount = 26, Name = "Командует прикрытием" });
		e.LootTable.Add(new LootEntry { ItemId = "amulet_might_low", Chance = 1.00f, Affixed = true });
		e.LootTable.Add(new LootEntry { ItemId = "ring_blessed_low", Chance = 1.00f, Affixed = true });
		e.LootTable.Add(new LootEntry { ItemId = "ring_power_low",   Chance = 0.50f, Affixed = true });
		e.LootTable.Add(new LootEntry { ItemId = "shield_balanced_low", Chance = 0.50f });
		e.LootTable.Add(new LootEntry { ItemId = "potion_full",      Chance = 1.00f });
		e.LootTable.Add(new LootEntry { ItemId = "potion_hp_medium", Chance = 1.00f });
		e.LootTable.Add(new LootEntry { ItemId = "potion_strength",  Chance = 0.75f });
		return e;
	}
}

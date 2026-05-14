namespace GuildOfGreed.Shared.Domain;

// Враги D-грейдовых локаций (уровни 21..40) + C-trial босс. Лор-калибровка:
// игрок по-прежнему рядовой авантюрист, просто закалённый — поэтому пул
// остаётся приземлённым (камнетёсы, бандитские старшины, болотные знахари,
// горные звери, ренегаты-рыцари). Никаких личей/тёмных лордов на D — это
// зарезервировано за C+. См. project_world_tone_grades в памяти.
//
// Калибровка HP/урона (против перса 22-40 ур. с D-low/mid экипировкой):
//   D-low  рядовой:  220-320 HP, удары 38-50
//   D-mid  рядовой:  280-480 HP, удары 50-66
//   D-top  рядовой:  520-720 HP, удары 62-86
//   D-low  босс:     ~1000 HP, удары 55-70
//   D-mid  босс:     ~1400 HP, удары 70-88
//   D-top  босс:     ~2000 HP, удары 80-100
//   C-trial босс:    ~3200 HP, удары 95-130 (вход в C через победу)
public partial class EnemyData
{
	// =========================================================================
	// Локация 5 — "Каменоломня Гримсхольд" (D low). Заброшенный карьер,
	// захваченный отколовшейся бандой и контрабандистами. Уровень 21+.
	// =========================================================================

	public static EnemyData CreateQuarryBrute()
	{
		var e = new EnemyData
		{
			EnemyName = "Каменный громила",
			MaxHp = 320, CurrentHp = 320,
			PhysDef = 9, MagicDef = 1,
			MoneyMin = 8, MoneyMax = 16,
		};
		e.Intents.Add(new Intent { Type = "attack", Amount = 38, Name = "Удар киркой",     WeaponType = null });
		e.Intents.Add(new Intent { Type = "attack", Amount = 50, Name = "Двуручный замах", WeaponType = null });
		e.Intents.Add(new Intent { Type = "block",  Amount = 18, Name = "Опирается на кирку" });
		e.LootTable.Add(new LootEntry { ItemId = "potion_hp_medium",       Chance = 0.55f });
		e.LootTable.Add(new LootEntry { ItemId = "heavy_gloves_d_low",     Chance = 0.10f, Affixed = true });
		e.LootTable.Add(new LootEntry { ItemId = "heavy_boots_d_low",      Chance = 0.10f, Affixed = true });
		e.LootTable.Add(new LootEntry { ItemId = "potion_strength",        Chance = 0.15f });
		return e;
	}

	public static EnemyData CreateBanditElderD()
	{
		var e = new EnemyData
		{
			EnemyName = "Бандит-старшина",
			MaxHp = 280, CurrentHp = 280,
			PhysDef = 7, MagicDef = 2,
			MoneyMin = 10, MoneyMax = 22,
		};
		e.Intents.Add(new Intent { Type = "attack", Amount = 42, Name = "Удар мечом",         WeaponType = "sword_1h" });
		e.Intents.Add(new Intent { Type = "attack", Amount = 50, Name = "Подсечка-укол",      WeaponType = "sword_1h" });
		e.Intents.Add(new Intent { Type = "block",  Amount = 16, Name = "Боевая стойка" });
		e.LootTable.Add(new LootEntry { ItemId = "potion_hp_medium",     Chance = 0.45f });
		e.LootTable.Add(new LootEntry { ItemId = "sword_1h_d_low",       Chance = 0.10f, Affixed = true });
		e.LootTable.Add(new LootEntry { ItemId = "light_helmet_d_low",   Chance = 0.10f, Affixed = true });
		e.LootTable.Add(new LootEntry { ItemId = "ring_blessed_low",     Chance = 0.20f, Affixed = true });
		return e;
	}

	public static EnemyData CreateSwampHerbalist()
	{
		var e = new EnemyData
		{
			EnemyName = "Болотный знахарь",
			MaxHp = 220, CurrentHp = 220,
			PhysDef = 3, MagicDef = 8,
			MoneyMin = 12, MoneyMax = 24,
		};
		e.Intents.Add(new Intent { Type = "attack_magic", Amount = 45, Name = "Ядовитая зола",  WeaponType = null });
		e.Intents.Add(new Intent { Type = "attack_magic", Amount = 55, Name = "Гниль-заговор",  WeaponType = null });
		e.Intents.Add(new Intent { Type = "block",        Amount = 14, Name = "Бормочет оберег" });
		e.LootTable.Add(new LootEntry { ItemId = "potion_mp_medium",     Chance = 0.45f });
		e.LootTable.Add(new LootEntry { ItemId = "robe_helmet_d_low",    Chance = 0.10f, Affixed = true });
		e.LootTable.Add(new LootEntry { ItemId = "robe_gloves_d_low",    Chance = 0.10f, Affixed = true });
		e.LootTable.Add(new LootEntry { ItemId = "potion_focus",         Chance = 0.20f });
		return e;
	}

	public static EnemyData CreateSmuggler()
	{
		var e = new EnemyData
		{
			EnemyName = "Контрабандист",
			MaxHp = 260, CurrentHp = 260,
			PhysDef = 5, MagicDef = 2,
			MoneyMin = 14, MoneyMax = 28,
		};
		e.Intents.Add(new Intent { Type = "attack", Amount = 38, Name = "Удар кинжалом",      WeaponType = "knife" });
		e.Intents.Add(new Intent { Type = "attack", Amount = 48, Name = "Двойной удар",       WeaponType = "knife" });
		e.Intents.Add(new Intent { Type = "block",  Amount = 12, Name = "Уходит в тень" });
		e.LootTable.Add(new LootEntry { ItemId = "potion_hp_small",      Chance = 0.50f });
		e.LootTable.Add(new LootEntry { ItemId = "dagger_d_low",         Chance = 0.12f, Affixed = true });
		e.LootTable.Add(new LootEntry { ItemId = "light_boots_d_low",    Chance = 0.10f, Affixed = true });
		e.LootTable.Add(new LootEntry { ItemId = "amulet_might_low",     Chance = 0.18f, Affixed = true });
		return e;
	}

	public static EnemyData CreateQuarryOverseer()
	{
		var e = new EnemyData
		{
			EnemyName = "Надзиратель каменоломни",
			MaxHp = 1000, CurrentHp = 1000,
			PhysDef = 12, MagicDef = 4,
			HpRegen = 4,
			MoneyMin = 70, MoneyMax = 140,
		};
		e.Intents.Add(new Intent { Type = "attack", Amount = 55, Name = "Удар двуручником",     WeaponType = "sword_2h" });
		e.Intents.Add(new Intent { Type = "attack", Amount = 70, Name = "Сокрушающий замах",    WeaponType = "sword_2h" });
		e.Intents.Add(new Intent { Type = "attack", Amount = 45, Name = "Удар сапогом",         WeaponType = null });
		e.Intents.Add(new Intent { Type = "block",  Amount = 26, Name = "Командует отбой" });
		e.LootTable.Add(new LootEntry { ItemId = "sword_2h_d_low",       Chance = 1.00f, Affixed = true });
		e.LootTable.Add(new LootEntry { ItemId = "heavy_chest_d_low",    Chance = 1.00f, Affixed = true });
		e.LootTable.Add(new LootEntry { ItemId = "ring_power_low",       Chance = 1.00f, Affixed = true });
		e.LootTable.Add(new LootEntry { ItemId = "potion_full",          Chance = 1.00f });
		e.LootTable.Add(new LootEntry { ItemId = "potion_strength",      Chance = 0.80f });
		return e;
	}

	// =========================================================================
	// Локация 6 — "Полузатопленный храм" (D mid). Старый храм в низине,
	// в котором осели фанатики раскольнической секты. Уровень 26+.
	// =========================================================================

	public static EnemyData CreateZealotSpearman()
	{
		var e = new EnemyData
		{
			EnemyName = "Фанатик-копейщик",
			MaxHp = 380, CurrentHp = 380,
			PhysDef = 9, MagicDef = 4,
			MoneyMin = 14, MoneyMax = 28,
		};
		e.Intents.Add(new Intent { Type = "attack", Amount = 50, Name = "Удар копьём",       WeaponType = "sword_2h" });
		e.Intents.Add(new Intent { Type = "attack", Amount = 60, Name = "Колющий выпад",     WeaponType = "sword_2h" });
		e.Intents.Add(new Intent { Type = "block",  Amount = 20, Name = "Прячется за щит" });
		e.LootTable.Add(new LootEntry { ItemId = "potion_hp_medium",     Chance = 0.50f });
		e.LootTable.Add(new LootEntry { ItemId = "heavy_helmet_d_mid",   Chance = 0.10f, Affixed = true });
		e.LootTable.Add(new LootEntry { ItemId = "shield_physical_low",  Chance = 0.18f });
		return e;
	}

	public static EnemyData CreateCultGuard()
	{
		var e = new EnemyData
		{
			EnemyName = "Охранник культа",
			MaxHp = 480, CurrentHp = 480,
			PhysDef = 13, MagicDef = 6,
			HpRegen = 4,
			MoneyMin = 16, MoneyMax = 32,
		};
		e.Intents.Add(new Intent { Type = "attack", Amount = 52, Name = "Удар алебардой",      WeaponType = "sword_2h" });
		e.Intents.Add(new Intent { Type = "attack", Amount = 64, Name = "Двойной взмах",       WeaponType = "sword_2h" });
		e.Intents.Add(new Intent { Type = "block",  Amount = 28, Name = "Стена щитов" });
		e.LootTable.Add(new LootEntry { ItemId = "potion_hp_medium",     Chance = 0.55f });
		e.LootTable.Add(new LootEntry { ItemId = "heavy_chest_d_mid",    Chance = 0.10f, Affixed = true });
		e.LootTable.Add(new LootEntry { ItemId = "heavy_gloves_d_mid",   Chance = 0.10f, Affixed = true });
		e.LootTable.Add(new LootEntry { ItemId = "ring_blessed_low",     Chance = 0.20f, Affixed = true });
		return e;
	}

	public static EnemyData CreateMadPilgrim()
	{
		var e = new EnemyData
		{
			EnemyName = "Безумный паломник",
			MaxHp = 280, CurrentHp = 280,
			PhysDef = 4, MagicDef = 2,
			MoneyMin = 8, MoneyMax = 18,
		};
		e.Intents.Add(new Intent { Type = "attack", Amount = 60, Name = "Безумный удар",        WeaponType = null });
		e.Intents.Add(new Intent { Type = "attack", Amount = 72, Name = "Кулаком в лицо",       WeaponType = null });
		e.Intents.Add(new Intent { Type = "attack", Amount = 50, Name = "Подножка",             WeaponType = null });
		e.LootTable.Add(new LootEntry { ItemId = "potion_hp_small",      Chance = 0.45f });
		e.LootTable.Add(new LootEntry { ItemId = "potion_strength",      Chance = 0.20f });
		return e;
	}

	public static EnemyData CreateSchismHealer()
	{
		var e = new EnemyData
		{
			EnemyName = "Целитель-расколольник",
			MaxHp = 340, CurrentHp = 340,
			PhysDef = 4, MagicDef = 9,
			HpRegen = 6,
			MoneyMin = 20, MoneyMax = 38,
		};
		e.Intents.Add(new Intent { Type = "attack_magic", Amount = 56, Name = "Костёр в груди",   WeaponType = null });
		e.Intents.Add(new Intent { Type = "attack_magic", Amount = 66, Name = "Гнилостный нимб",  WeaponType = null });
		e.Intents.Add(new Intent { Type = "block",        Amount = 22, Name = "Молитва" });
		e.LootTable.Add(new LootEntry { ItemId = "potion_mp_medium",     Chance = 0.50f });
		e.LootTable.Add(new LootEntry { ItemId = "robe_chest_d_mid",     Chance = 0.10f, Affixed = true });
		e.LootTable.Add(new LootEntry { ItemId = "ring_focus_low",       Chance = 0.20f, Affixed = true });
		e.LootTable.Add(new LootEntry { ItemId = "potion_focus",         Chance = 0.18f });
		return e;
	}

	public static EnemyData CreateSchismHierarch()
	{
		var e = new EnemyData
		{
			EnemyName = "Иерарх Раскола",
			MaxHp = 1400, CurrentHp = 1400,
			PhysDef = 12, MagicDef = 10,
			HpRegen = 6,
			MoneyMin = 100, MoneyMax = 200,
		};
		e.Intents.Add(new Intent { Type = "attack_magic", Amount = 70, Name = "Кара отступнику",    WeaponType = null });
		e.Intents.Add(new Intent { Type = "attack_magic", Amount = 88, Name = "Световой клинок",    WeaponType = null });
		e.Intents.Add(new Intent { Type = "attack",       Amount = 56, Name = "Удар посохом",       WeaponType = "staff" });
		e.Intents.Add(new Intent { Type = "block",        Amount = 30, Name = "Защитный круг" });
		e.LootTable.Add(new LootEntry { ItemId = "staff_d_mid",          Chance = 1.00f, Affixed = true });
		e.LootTable.Add(new LootEntry { ItemId = "robe_chest_d_mid",     Chance = 1.00f, Affixed = true });
		e.LootTable.Add(new LootEntry { ItemId = "amulet_arcane_low",    Chance = 1.00f, Affixed = true });
		e.LootTable.Add(new LootEntry { ItemId = "potion_full",          Chance = 1.00f });
		e.LootTable.Add(new LootEntry { ItemId = "potion_focus",         Chance = 0.80f });
		return e;
	}

	// =========================================================================
	// Локация 7 — "Холмы Когтя" (D top). Горный хребет: крупное зверьё,
	// контрабанда, осевшие беглые рыцари. Уровень 32+.
	// =========================================================================

	public static EnemyData CreateMountainGrizzly()
	{
		var e = new EnemyData
		{
			EnemyName = "Горный гризли",
			MaxHp = 720, CurrentHp = 720,
			PhysDef = 10, MagicDef = 0,
			HpRegen = 5,
			MoneyMin = 18, MoneyMax = 36,
		};
		e.Intents.Add(new Intent { Type = "attack", Amount = 62, Name = "Удар лапой",          WeaponType = null });
		e.Intents.Add(new Intent { Type = "attack", Amount = 76, Name = "Сшибающий рывок",     WeaponType = null });
		e.Intents.Add(new Intent { Type = "attack", Amount = 50, Name = "Укус",                WeaponType = null });
		e.Intents.Add(new Intent { Type = "block",  Amount = 26, Name = "Встаёт на дыбы" });
		e.LootTable.Add(new LootEntry { ItemId = "potion_hp_medium",     Chance = 0.60f });
		e.LootTable.Add(new LootEntry { ItemId = "potion_strength",      Chance = 0.25f });
		return e;
	}

	public static EnemyData CreateGyrokhotak()
	{
		var e = new EnemyData
		{
			EnemyName = "Гирокохтак",
			MaxHp = 520, CurrentHp = 520,
			PhysDef = 4, MagicDef = 12,
			MoneyMin = 22, MoneyMax = 44,
		};
		e.Intents.Add(new Intent { Type = "attack_magic", Amount = 68, Name = "Кислотный плевок",  WeaponType = null });
		e.Intents.Add(new Intent { Type = "attack_magic", Amount = 80, Name = "Грозовой разряд",   WeaponType = null });
		e.Intents.Add(new Intent { Type = "block",        Amount = 22, Name = "Скрывается в туман" });
		e.LootTable.Add(new LootEntry { ItemId = "potion_mp_medium",     Chance = 0.55f });
		e.LootTable.Add(new LootEntry { ItemId = "robe_helmet_d_top",    Chance = 0.10f, Affixed = true });
		e.LootTable.Add(new LootEntry { ItemId = "amulet_arcane_low",    Chance = 0.18f, Affixed = true });
		return e;
	}

	public static EnemyData CreateRenegadeKnight()
	{
		var e = new EnemyData
		{
			EnemyName = "Рыцарь-затворник",
			MaxHp = 640, CurrentHp = 640,
			PhysDef = 15, MagicDef = 6,
			HpRegen = 3,
			MoneyMin = 26, MoneyMax = 50,
		};
		e.Intents.Add(new Intent { Type = "attack", Amount = 70, Name = "Удар двуручником",    WeaponType = "sword_2h" });
		e.Intents.Add(new Intent { Type = "attack", Amount = 86, Name = "Колено в землю",      WeaponType = "sword_2h" });
		e.Intents.Add(new Intent { Type = "attack", Amount = 58, Name = "Удар крестовиной",    WeaponType = "sword_1h" });
		e.Intents.Add(new Intent { Type = "block",  Amount = 34, Name = "Опускает забрало" });
		e.LootTable.Add(new LootEntry { ItemId = "sword_2h_d_top",       Chance = 0.10f, Affixed = true });
		e.LootTable.Add(new LootEntry { ItemId = "heavy_helmet_d_top",   Chance = 0.10f, Affixed = true });
		e.LootTable.Add(new LootEntry { ItemId = "heavy_gloves_d_top",   Chance = 0.10f, Affixed = true });
		e.LootTable.Add(new LootEntry { ItemId = "ring_blessed_low",     Chance = 0.25f, Affixed = true });
		return e;
	}

	public static EnemyData CreateAlphaGrizzly()
	{
		var e = new EnemyData
		{
			EnemyName = "Альфа-гризли",
			MaxHp = 2000, CurrentHp = 2000,
			PhysDef = 14, MagicDef = 4,
			HpRegen = 6,
			MoneyMin = 160, MoneyMax = 280,
		};
		e.Intents.Add(new Intent { Type = "attack", Amount = 80,  Name = "Удар лапой",         WeaponType = null });
		e.Intents.Add(new Intent { Type = "attack", Amount = 100, Name = "Двойной размах",     WeaponType = null });
		e.Intents.Add(new Intent { Type = "attack", Amount = 68,  Name = "Сшибающий укус",     WeaponType = null });
		e.Intents.Add(new Intent { Type = "block",  Amount = 38,  Name = "Встаёт на дыбы" });
		e.LootTable.Add(new LootEntry { ItemId = "heavy_chest_d_top",    Chance = 1.00f, Affixed = true });
		e.LootTable.Add(new LootEntry { ItemId = "amulet_might_low",     Chance = 1.00f, Affixed = true });
		e.LootTable.Add(new LootEntry { ItemId = "ring_power_low",       Chance = 1.00f, Affixed = true });
		e.LootTable.Add(new LootEntry { ItemId = "potion_full",          Chance = 1.00f, MinCount = 2, MaxCount = 2 });
		e.LootTable.Add(new LootEntry { ItemId = "potion_strength",      Chance = 1.00f });
		return e;
	}

	// =========================================================================
	// Локация 8 — "Турнирная ставка Каркаса" (C-trial). Очень сложно. Победа
	// над Каркасом автоматически даёт C-грейд (см. Session.HandleBattleAction).
	// Лор: подпольный наёмничий турнир в каменных шарах у границы. Каркас —
	// бывший чемпион полка, после увечья основал собственную арену. Никакой
	// магии: просто мужик, которого не победили за двенадцать лет.
	// =========================================================================

	public static EnemyData CreateArenaVeteran()
	{
		var e = new EnemyData
		{
			EnemyName = "Ветеран арены",
			MaxHp = 1200, CurrentHp = 1200,
			PhysDef = 16, MagicDef = 6,
			HpRegen = 4,
			MoneyMin = 50, MoneyMax = 90,
		};
		e.Intents.Add(new Intent { Type = "attack", Amount = 82,  Name = "Удар двуручником",   WeaponType = "sword_2h" });
		e.Intents.Add(new Intent { Type = "attack", Amount = 100, Name = "Сокрушающий замах",  WeaponType = "sword_2h" });
		e.Intents.Add(new Intent { Type = "attack", Amount = 68,  Name = "Удар щитом",         WeaponType = null });
		e.Intents.Add(new Intent { Type = "block",  Amount = 36,  Name = "Стойка ветерана" });
		e.LootTable.Add(new LootEntry { ItemId = "potion_hp_medium",     Chance = 1.00f });
		e.LootTable.Add(new LootEntry { ItemId = "potion_strength",      Chance = 0.60f });
		e.LootTable.Add(new LootEntry { ItemId = "heavy_chest_d_top",    Chance = 0.20f, Affixed = true });
		return e;
	}

	public static EnemyData CreateArenaChampion()
	{
		var e = new EnemyData
		{
			EnemyName = "Чемпион арены",
			MaxHp = 1600, CurrentHp = 1600,
			PhysDef = 18, MagicDef = 8,
			HpRegen = 5,
			MoneyMin = 80, MoneyMax = 150,
		};
		e.Intents.Add(new Intent { Type = "attack", Amount = 90,  Name = "Маховой удар",       WeaponType = "sword_2h" });
		e.Intents.Add(new Intent { Type = "attack", Amount = 110, Name = "Двойной замах",      WeaponType = "sword_2h" });
		e.Intents.Add(new Intent { Type = "attack", Amount = 72,  Name = "Боковой выпад",      WeaponType = "sword_1h" });
		e.Intents.Add(new Intent { Type = "block",  Amount = 40,  Name = "Опирается на меч" });
		e.LootTable.Add(new LootEntry { ItemId = "potion_full",          Chance = 1.00f });
		e.LootTable.Add(new LootEntry { ItemId = "sword_2h_d_top",       Chance = 0.30f, Affixed = true });
		e.LootTable.Add(new LootEntry { ItemId = "heavy_boots_d_top",    Chance = 0.25f, Affixed = true });
		return e;
	}

	// Финальный босс турнира. Бьёт чудовищно сильно; реген ощутимый. Магии нет.
	// Главный челлендж: продержаться по HP, успеть прокачать урон до того, как
	// он засосёт всю мp/зелья. Идеален для тестирования билда в финале D-полосы.
	public static EnemyData CreateKarkasTheFingerless()
	{
		var e = new EnemyData
		{
			EnemyName = "Каркас Беспалый",
			MaxHp = 3200, CurrentHp = 3200,
			PhysDef = 18, MagicDef = 10,
			HpRegen = 8,
			MoneyMin = 300, MoneyMax = 500,
		};
		e.Intents.Add(new Intent { Type = "attack", Amount = 95,  Name = "Удар двуручником",    WeaponType = "sword_2h" });
		e.Intents.Add(new Intent { Type = "attack", Amount = 115, Name = "Тяжёлый замах",       WeaponType = "sword_2h" });
		e.Intents.Add(new Intent { Type = "attack", Amount = 130, Name = "Сокрушающий удар",    WeaponType = "sword_2h" });
		e.Intents.Add(new Intent { Type = "attack", Amount = 80,  Name = "Кулак в челюсть",     WeaponType = null });
		e.Intents.Add(new Intent { Type = "block",  Amount = 50,  Name = "Командует прикрытием" });
		// Лут — щедрый, оправдывает риск.
		e.LootTable.Add(new LootEntry { ItemId = "sword_2h_d_top",       Chance = 1.00f, Affixed = true });
		e.LootTable.Add(new LootEntry { ItemId = "heavy_chest_d_top",    Chance = 1.00f, Affixed = true });
		e.LootTable.Add(new LootEntry { ItemId = "heavy_helmet_d_top",   Chance = 1.00f, Affixed = true });
		e.LootTable.Add(new LootEntry { ItemId = "amulet_might_low",     Chance = 1.00f, Affixed = true });
		e.LootTable.Add(new LootEntry { ItemId = "ring_power_low",       Chance = 1.00f, Affixed = true });
		e.LootTable.Add(new LootEntry { ItemId = "potion_full",          Chance = 1.00f, MinCount = 3, MaxCount = 3 });
		e.LootTable.Add(new LootEntry { ItemId = "potion_strength",      Chance = 1.00f });
		return e;
	}
}

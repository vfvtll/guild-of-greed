namespace GuildOfGreed.Shared.Domain;

// Враги двух длинных подземелий, доступных с 5 уровня персонажа:
//   локация 3 — "Заброшенные катакомбы" (нежить + культисты, упор на маг.урон,
//     играет на отсутствии MagDef у воинских билдов первой грейд-полосы).
//   локация 4 — "Развалины старого замка" (разбойники-ветераны и наёмники,
//     самое длинное подземелье; тяжёлый физический пресс + 1 кастер).
//
// Лор остаётся в духе E-грейда: ни одного эпического босса. Лич-послушник —
// это просто культист-некромант начального толка, "Капитан гарнизона" —
// бывший наёмник, который никого не спасает. Подземелья длиннее, поэтому
// калибровка целит в то, чтобы средне-снаряжённый перс 5–7 уровня
// проходил их на грани и без запаса зелий до конца не дошёл.
public partial class EnemyData
{
	// =========================================================================
	// Локация 3 — "Заброшенные катакомбы" (8 рядов карты).
	// Подавляющее большинство угроз бьёт магией — у воина с 0 MagDef каждый
	// удар проходит на полную в HP (после блока). Это и есть основной вектор
	// смертности подземелья.
	// =========================================================================

	// Костяной воин — рядовой нежити. Танковатый, с щитом, бьёт ржавым мечом.
	public static EnemyData CreateSkeletonWarrior()
	{
		var e = new EnemyData
		{
			EnemyName = "Костяной воин",
			MaxHp = 130, CurrentHp = 130,
			PhysDef = 4, MagicDef = 1,
			MoneyMin = 3, MoneyMax = 7,
		};
		e.Intents.Add(new Intent { Type = "attack", Amount = 28, Name = "Удар ржавым мечом", WeaponType = "sword_1h" });
		e.Intents.Add(new Intent { Type = "attack", Amount = 36, Name = "Рубящий замах",      WeaponType = "sword_1h" });
		e.Intents.Add(new Intent { Type = "block",  Amount = 14, Name = "Костяная защита" });
		e.LootTable.Add(new LootEntry { ItemId = "potion_hp_small",  Chance = 0.50f });
		e.LootTable.Add(new LootEntry { ItemId = "potion_hp_medium", Chance = 0.15f });
		e.LootTable.Add(new LootEntry { ItemId = "shield_physical_low", Chance = 0.12f });
		e.LootTable.Add(new LootEntry { ItemId = "ring_power_low",   Chance = 0.10f, Affixed = true });
		return e;
	}

	// Костяной лучник — лёгкий бойник, но Aimed Shot бьёт болезненно.
	public static EnemyData CreateSkeletonArcher()
	{
		var e = new EnemyData
		{
			EnemyName = "Костяной лучник",
			MaxHp = 90, CurrentHp = 90,
			PhysDef = 1, MagicDef = 0,
			MoneyMin = 2, MoneyMax = 6,
		};
		e.Intents.Add(new Intent { Type = "attack", Amount = 22, Name = "Выстрел",        WeaponType = null });
		e.Intents.Add(new Intent { Type = "attack", Amount = 32, Name = "Прицельный залп", WeaponType = null });
		e.Intents.Add(new Intent { Type = "block",  Amount = 6,  Name = "Отступает в тень" });
		e.LootTable.Add(new LootEntry { ItemId = "potion_hp_small", Chance = 0.40f });
		e.LootTable.Add(new LootEntry { ItemId = "potion_mp_small", Chance = 0.25f });
		e.LootTable.Add(new LootEntry { ItemId = "dagger_low",      Chance = 0.12f, Affixed = true });
		return e;
	}

	// Тёмный неофит — кастер. Магия здесь — главный killer персонажа.
	public static EnemyData CreateDarkNeophyte()
	{
		var e = new EnemyData
		{
			EnemyName = "Тёмный неофит",
			MaxHp = 85, CurrentHp = 85,
			PhysDef = 1, MagicDef = 3,
			MoneyMin = 4, MoneyMax = 9,
		};
		e.Intents.Add(new Intent { Type = "attack_magic", Amount = 30, Name = "Чёрная искра",  WeaponType = null });
		e.Intents.Add(new Intent { Type = "attack_magic", Amount = 40, Name = "Шёпот могилы",  WeaponType = null });
		e.Intents.Add(new Intent { Type = "block",        Amount = 10, Name = "Тёмный круг" });
		e.LootTable.Add(new LootEntry { ItemId = "potion_mp_medium", Chance = 0.35f });
		e.LootTable.Add(new LootEntry { ItemId = "potion_hp_small",  Chance = 0.30f });
		e.LootTable.Add(new LootEntry { ItemId = "ring_focus_low",   Chance = 0.18f, Affixed = true });
		e.LootTable.Add(new LootEntry { ItemId = "amulet_arcane_low",Chance = 0.10f, Affixed = true });
		e.LootTable.Add(new LootEntry { ItemId = "potion_focus",     Chance = 0.08f });
		return e;
	}

	// Бредущий мертвец — толстый, медленный, регенит. Игнорирует bleed на 4/тик.
	public static EnemyData CreateShamblingDead()
	{
		var e = new EnemyData
		{
			EnemyName = "Бредущий мертвец",
			MaxHp = 210, CurrentHp = 210,
			PhysDef = 6, MagicDef = 0,
			HpRegen = 4,
			MoneyMin = 3, MoneyMax = 8,
		};
		e.Intents.Add(new Intent { Type = "attack", Amount = 24, Name = "Тяжёлый замах",    WeaponType = null });
		e.Intents.Add(new Intent { Type = "attack", Amount = 32, Name = "Рывок мертвяка",   WeaponType = null });
		e.Intents.Add(new Intent { Type = "block",  Amount = 8,  Name = "Сросшаяся плоть" });
		e.LootTable.Add(new LootEntry { ItemId = "potion_hp_small",  Chance = 0.60f });
		e.LootTable.Add(new LootEntry { ItemId = "potion_hp_medium", Chance = 0.25f });
		e.LootTable.Add(new LootEntry { ItemId = "potion_full",      Chance = 0.06f });
		e.LootTable.Add(new LootEntry { ItemId = "ring_power_low",   Chance = 0.10f, Affixed = true });
		return e;
	}

	// Тёмный призрак — стеклянная пушка магии: 1 хит снимает у воина 30+ HP.
	public static EnemyData CreateShade()
	{
		var e = new EnemyData
		{
			EnemyName = "Тёмный призрак",
			MaxHp = 70, CurrentHp = 70,
			PhysDef = 0, MagicDef = 4,
			MoneyMin = 3, MoneyMax = 7,
		};
		e.Intents.Add(new Intent { Type = "attack_magic", Amount = 26, Name = "Касание холода", WeaponType = null });
		e.Intents.Add(new Intent { Type = "attack_magic", Amount = 36, Name = "Воющий вихрь",   WeaponType = null });
		e.Intents.Add(new Intent { Type = "block",        Amount = 12, Name = "Растворяется в тени" });
		e.LootTable.Add(new LootEntry { ItemId = "potion_mp_small",  Chance = 0.45f });
		e.LootTable.Add(new LootEntry { ItemId = "ring_focus_low",   Chance = 0.15f, Affixed = true });
		e.LootTable.Add(new LootEntry { ItemId = "amulet_arcane_low",Chance = 0.07f, Affixed = true });
		return e;
	}

	// Босс катакомб — лич-послушник. Долгий бой; смесь магии и физ.проклятий.
	// 420 HP против ~55 dpt чистого урона воина = ~7-8 ходов на чистой выкладке,
	// при этом сам наносит ~40-55 магии в HP (MagDef=0). За 7 ходов это 280-380
	// HP damage — гарантированный размен ресурсов и зелий.
	public static EnemyData CreateLichAcolyte()
	{
		var e = new EnemyData
		{
			EnemyName = "Лич-послушник",
			MaxHp = 420, CurrentHp = 420,
			PhysDef = 6, MagicDef = 8,
			HpRegen = 3,
			MoneyMin = 25, MoneyMax = 55,
		};
		e.Intents.Add(new Intent { Type = "attack_magic", Amount = 42, Name = "Тёмная вспышка",  WeaponType = null });
		e.Intents.Add(new Intent { Type = "attack_magic", Amount = 54, Name = "Гниль времён",    WeaponType = null });
		e.Intents.Add(new Intent { Type = "attack",       Amount = 30, Name = "Проклятый посох", WeaponType = "staff" });
		e.Intents.Add(new Intent { Type = "block",        Amount = 22, Name = "Барьер праха" });
		e.LootTable.Add(new LootEntry { ItemId = "amulet_arcane_low", Chance = 1.00f, Affixed = true });
		e.LootTable.Add(new LootEntry { ItemId = "ring_focus_low",    Chance = 1.00f, Affixed = true });
		e.LootTable.Add(new LootEntry { ItemId = "ring_blessed_low",  Chance = 0.50f, Affixed = true });
		e.LootTable.Add(new LootEntry { ItemId = "potion_full",       Chance = 1.00f });
		e.LootTable.Add(new LootEntry { ItemId = "potion_mp_medium",  Chance = 1.00f });
		e.LootTable.Add(new LootEntry { ItemId = "potion_focus",      Chance = 0.75f });
		e.LootTable.Add(new LootEntry { ItemId = "potion_strength",   Chance = 0.40f });
		return e;
	}

	// =========================================================================
	// Локация 4 — "Развалины старого замка" (10 рядов карты, самое длинное).
	// Тяжёлый физический пресс. Чтобы пробить 18+ PhysDef игрока стабильно,
	// удары рядового мяса лежат в 28–46 — net 10-28 HP за хит. 1 кастер для
	// чейндж-ап (Полевой колдун).
	// =========================================================================

	// Разбойник-ветеран — почти ровно "Старший разбойник" из старта, только
	// он теперь рядовой враг этого подземелья.
	public static EnemyData CreateBanditVeteran()
	{
		var e = new EnemyData
		{
			EnemyName = "Разбойник-ветеран",
			MaxHp = 150, CurrentHp = 150,
			PhysDef = 4, MagicDef = 1,
			MoneyMin = 4, MoneyMax = 10,
		};
		e.Intents.Add(new Intent { Type = "attack", Amount = 30, Name = "Удар мечом",       WeaponType = "sword_1h" });
		e.Intents.Add(new Intent { Type = "attack", Amount = 40, Name = "Сокрушающий замах",WeaponType = "sword_1h" });
		e.Intents.Add(new Intent { Type = "block",  Amount = 16, Name = "Боевая стойка" });
		e.LootTable.Add(new LootEntry { ItemId = "potion_hp_small",   Chance = 0.55f });
		e.LootTable.Add(new LootEntry { ItemId = "potion_hp_medium",  Chance = 0.15f });
		e.LootTable.Add(new LootEntry { ItemId = "potion_strength",   Chance = 0.10f });
		e.LootTable.Add(new LootEntry { ItemId = "ring_power_low",    Chance = 0.18f, Affixed = true });
		e.LootTable.Add(new LootEntry { ItemId = "shield_physical_low",Chance = 0.10f });
		return e;
	}

	// Наёмник с алебардой — 2H-боец, медленный, бьёт изо всей дури.
	public static EnemyData CreateHalberdMerc()
	{
		var e = new EnemyData
		{
			EnemyName = "Наёмник с алебардой",
			MaxHp = 180, CurrentHp = 180,
			PhysDef = 5, MagicDef = 1,
			MoneyMin = 5, MoneyMax = 12,
		};
		e.Intents.Add(new Intent { Type = "attack", Amount = 34, Name = "Маховой удар",  WeaponType = "sword_2h" });
		e.Intents.Add(new Intent { Type = "attack", Amount = 46, Name = "Колющий рывок", WeaponType = "sword_2h" });
		e.Intents.Add(new Intent { Type = "block",  Amount = 14, Name = "Опирается на древко" });
		e.LootTable.Add(new LootEntry { ItemId = "potion_hp_medium", Chance = 0.35f });
		e.LootTable.Add(new LootEntry { ItemId = "potion_hp_small",  Chance = 0.30f });
		e.LootTable.Add(new LootEntry { ItemId = "amulet_might_low", Chance = 0.18f, Affixed = true });
		e.LootTable.Add(new LootEntry { ItemId = "potion_strength",  Chance = 0.10f });
		return e;
	}

	// Закованный страж — танк подземелья. Толстый, бьёт умеренно, выматывает.
	public static EnemyData CreateChainedGuard()
	{
		var e = new EnemyData
		{
			EnemyName = "Закованный страж",
			MaxHp = 250, CurrentHp = 250,
			PhysDef = 10, MagicDef = 2,
			MoneyMin = 6, MoneyMax = 14,
		};
		e.Intents.Add(new Intent { Type = "attack", Amount = 26, Name = "Удар латной рукой", WeaponType = null });
		e.Intents.Add(new Intent { Type = "attack", Amount = 34, Name = "Шипастый шит-таран",WeaponType = null });
		e.Intents.Add(new Intent { Type = "block",  Amount = 24, Name = "Глухая защита" });
		e.LootTable.Add(new LootEntry { ItemId = "potion_hp_medium",    Chance = 0.40f });
		e.LootTable.Add(new LootEntry { ItemId = "shield_balanced_low", Chance = 0.18f });
		e.LootTable.Add(new LootEntry { ItemId = "shield_physical_low", Chance = 0.15f });
		e.LootTable.Add(new LootEntry { ItemId = "ring_blessed_low",    Chance = 0.10f, Affixed = true });
		return e;
	}

	// Каторжник-берсерк — стеклянная пушка-2: 1 PhysDef, но самый дикий удар
	// по физике в подземелье. Атакует и атакует — почти не блокирует.
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
		e.Intents.Add(new Intent { Type = "attack", Amount = 56, Name = "Двойной удар",    WeaponType = null });
		e.Intents.Add(new Intent { Type = "attack", Amount = 36, Name = "Кулак в лицо",    WeaponType = null });
		e.LootTable.Add(new LootEntry { ItemId = "potion_hp_small",  Chance = 0.40f });
		e.LootTable.Add(new LootEntry { ItemId = "potion_strength",  Chance = 0.20f });
		e.LootTable.Add(new LootEntry { ItemId = "ring_power_low",   Chance = 0.15f, Affixed = true });
		return e;
	}

	// Лучник засады — дальний, лёгкий, точечный.
	public static EnemyData CreateAmbushArcher()
	{
		var e = new EnemyData
		{
			EnemyName = "Лучник засады",
			MaxHp = 100, CurrentHp = 100,
			PhysDef = 2, MagicDef = 1,
			MoneyMin = 3, MoneyMax = 8,
		};
		e.Intents.Add(new Intent { Type = "attack", Amount = 26, Name = "Выстрел",          WeaponType = null });
		e.Intents.Add(new Intent { Type = "attack", Amount = 36, Name = "Стрела с подкрутом",WeaponType = null });
		e.Intents.Add(new Intent { Type = "block",  Amount = 10, Name = "Уходит за укрытие" });
		e.LootTable.Add(new LootEntry { ItemId = "potion_hp_small", Chance = 0.40f });
		e.LootTable.Add(new LootEntry { ItemId = "potion_mp_small", Chance = 0.25f });
		e.LootTable.Add(new LootEntry { ItemId = "dagger_low",      Chance = 0.15f, Affixed = true });
		return e;
	}

	// Полевой колдун — единственный кастер пула. Опасен для физ.билдов
	// с пустой MagDef. На "Грозовой удар" игрок может потерять ~40 HP за тик.
	public static EnemyData CreateFieldWarlock()
	{
		var e = new EnemyData
		{
			EnemyName = "Полевой колдун",
			MaxHp = 115, CurrentHp = 115,
			PhysDef = 2, MagicDef = 4,
			MoneyMin = 5, MoneyMax = 12,
		};
		e.Intents.Add(new Intent { Type = "attack_magic", Amount = 32, Name = "Грозовой удар", WeaponType = null });
		e.Intents.Add(new Intent { Type = "attack_magic", Amount = 44, Name = "Ледяной шип",   WeaponType = null });
		e.Intents.Add(new Intent { Type = "block",        Amount = 14, Name = "Защитный знак" });
		e.LootTable.Add(new LootEntry { ItemId = "potion_mp_medium",   Chance = 0.40f });
		e.LootTable.Add(new LootEntry { ItemId = "potion_focus",       Chance = 0.18f });
		e.LootTable.Add(new LootEntry { ItemId = "ring_focus_low",     Chance = 0.20f, Affixed = true });
		e.LootTable.Add(new LootEntry { ItemId = "amulet_arcane_low",  Chance = 0.10f, Affixed = true });
		return e;
	}

	// Босс замка — Капитан гарнизона. Дольше и больнее всех бывших боссов.
	// 560 HP, бьёт по двум стихиям сразу: ~45 phys (net ~25-28 после 18 PhysDef)
	// плюс магия 34 (net 34, MagDef=0). Размен ходов почти всегда не в пользу
	// игрока — без зелий просто не выжить.
	public static EnemyData CreateGarrisonCaptain()
	{
		var e = new EnemyData
		{
			EnemyName = "Капитан гарнизона",
			MaxHp = 560, CurrentHp = 560,
			PhysDef = 8, MagicDef = 4,
			HpRegen = 2,
			MoneyMin = 40, MoneyMax = 80,
		};
		e.Intents.Add(new Intent { Type = "attack",       Amount = 38, Name = "Удар клевца",      WeaponType = "sword_1h" });
		e.Intents.Add(new Intent { Type = "attack",       Amount = 50, Name = "Двойной выпад",    WeaponType = "sword_1h" });
		e.Intents.Add(new Intent { Type = "attack_magic", Amount = 34, Name = "Печать командира", WeaponType = null });
		e.Intents.Add(new Intent { Type = "block",        Amount = 26, Name = "Командирский щит" });
		e.LootTable.Add(new LootEntry { ItemId = "amulet_might_low",  Chance = 1.00f, Affixed = true });
		e.LootTable.Add(new LootEntry { ItemId = "ring_blessed_low",  Chance = 1.00f, Affixed = true });
		e.LootTable.Add(new LootEntry { ItemId = "ring_power_low",    Chance = 0.50f, Affixed = true });
		e.LootTable.Add(new LootEntry { ItemId = "shield_balanced_low",Chance = 0.50f });
		e.LootTable.Add(new LootEntry { ItemId = "potion_full",       Chance = 1.00f });
		e.LootTable.Add(new LootEntry { ItemId = "potion_hp_medium",  Chance = 1.00f });
		e.LootTable.Add(new LootEntry { ItemId = "potion_strength",   Chance = 0.75f });
		e.LootTable.Add(new LootEntry { ItemId = "potion_focus",      Chance = 0.50f });
		return e;
	}
}

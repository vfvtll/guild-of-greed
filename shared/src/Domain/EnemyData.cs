using System.Collections.Generic;

namespace GuildOfGreed.Shared.Domain;

// Враги работают по системе Intent: показывают своё намерение до своего хода.
// Не используют карты — только заранее заданный список действий.
//
// LootTable: при смерти Combat ролит каждую запись по её Chance и кладёт
// успешные дропы в инвентарь игрока (см. Combat.Cards.DropLoot).
public partial class EnemyData
{
	public string EnemyName = "Гоблин";
	public int MaxHp = 60;
	public int CurrentHp = 60;
	public int PhysDef = 0;
	public int MagicDef = 0;
	// Регенерация в начале хода врага. Сначала тратится на снижение
	// BleedStack, остаток (если есть) восстанавливает HP. Default=0 для
	// большинства врагов — bleed их растирает в порошок без сопротивления.
	public int HpRegen = 0;
	public int CurrentBlock = 0;
	public List<StatusEffect> Effects = new();
	public List<Intent> Intents = new();
	public Intent NextIntent;
	public List<LootEntry> LootTable = new();
	// Стак кровотечения (И6.2-E). Каждый физ.удар оружием с
	// WeaponPassive.BleedOnHit добавляет magnitude% от нанесённого по HP
	// урона. В конце хода врага стак вычитает HpRegen и наносит остаток
	// как урон по HP (игнорируя PhysDef/Block). Стак НЕ сбрасывается —
	// продолжает копиться между ходами.
	public int BleedStack = 0;

	// Деньги — минимум/максимум медяков, выпадающих при смерти. CombatEngine.DropLoot
	// катает Rng.Range(Min, Max+1) и прибавляет к Inventory.Money. Default 1..3 для
	// рядовых врагов. Для тутора-болванчика выставлен 0/0 (см. CreateTrainingDummy).
	public int MoneyMin = 1;
	public int MoneyMax = 3;

	// Дропать ли крафтовые ресурсы (см. ResourcesDB + CombatEngine.DropLoot).
	// По умолчанию все враги дропают; туторный болванчик/мобы без лута могут
	// явно отключить. Базовый уровень крафт-системы (см. .claude_design_crafting.md):
	// независимо от грейда мобы кидают случайные E/D ресурсы.
	public bool DropsResources = true;

	// Спавн encounter'а для узла на карте. Детерминированно от seed — клиент
	// и сервер получают идентичный набор врагов. Seed — это battleSeed, который
	// сам выводится из (runSeed, nodeId) на сервере, см. Session.DeriveBattleSeed.
	//
	// Лор-настройка: игрок — обычный авантюрист, а не избранный. Подавляющее
	// большинство встреч — дикие звери и гоблины-разбойники. Боссы локаций
	// первой грейд-полосы — старший разбойник / альфа-волк / гоблин-вожак,
	// без эпического пафоса.
	public static List<EnemyData> SpawnFor(int locationIndex, MapNodeType nodeType, int seed = 0)
	{
		var list = new List<EnemyData>();
		if (nodeType == MapNodeType.Tutorial)
		{
			list.Add(CreateTrainingDummy());
			return list;
		}
		var rng = new RandomSource(seed);
		if (nodeType == MapNodeType.Boss)
		{
			list.Add(CreateBossFor(locationIndex));
			return list;
		}
		// Обычный бой — берём 1–3 случайных из пула локации.
		var pool = PoolFor(locationIndex);
		int count = rng.Range(1, 4);  // 1..3
		// Локация "лес" — стайные мобы, перевес в сторону мелких; даём шанс
		// добавить +1 врага сверху (тоже из пула).
		if (locationIndex == 1 && rng.Chance(0.5f)) count++;
		// Катакомбы и руины (длинные подземелья от 5 ур.) — иногда смешанные пачки
		// из 3–4 врагов: это там, где у игрока проседает MagDef/HP, давление пакета
		// заметно сильнее одиночки. ~33% шанс +1.
		if ((locationIndex == 3 || locationIndex == 4) && rng.Chance(0.33f)) count++;
		count = System.Math.Min(count, 4);
		for (int i = 0; i < count; i++)
			list.Add(pool[rng.Next(pool.Count)]());
		return list;
	}

	// Пул фабрик по локации. Каждая фабрика возвращает нового врага.
	// Использование делегатов (без аргументов) позволяет SpawnFor рандомно
	// семплировать без аллокации каждой возможной комбинации.
	private static List<System.Func<EnemyData>> PoolFor(int locationIndex)
	{
		switch (locationIndex)
		{
			case 0: // "Подземелье" — гоблины-разбойники, разведчики, эльпи.
				return new() { CreateGoblinRogue, CreateGoblinScout, CreateElpy };
			case 1: // "Тёмный лес" — звери: волки, зайцы, эльпи.
				return new() { CreateWildHare, CreateWolf, CreateElpy, CreateForestGoblin };
			case 2: // "Логово гоблинов" — гоблины и шаман.
				return new() { CreateGoblinRogue, CreateGoblinScout, CreateGoblinShaman };
			case 3: // "Звериная балка" — звери чащи + одна ведунья + забредший каторжник (lvl≥5).
				return new() { CreateBrownWolf, CreateWildBoar, CreateWolverine, CreateForestWitch, CreateRunawayConvict };
			case 4: // "Разбойничья застава" — бандиты, каторжники, наёмники, знахарь (lvl≥5, самое длинное).
				return new() { CreateBanditVeteran, CreateMercenary, CreateChainedConvict, CreateBerserkConvict, CreateAmbushArcher, CreateGangHealer };
			default:
				return new() { CreateGoblinRogue };
		}
	}

	// Босс локации — конкретная фабрика. Все скромные — это всё ещё первый грейд.
	private static EnemyData CreateBossFor(int locationIndex) => locationIndex switch
	{
		0 => CreateBanditElder(),    // Подземелье — старший разбойник
		1 => CreateAlphaWolf(),      // Лес — альфа стаи
		2 => CreateGoblinChief(),    // Логово — вождь гоблинов
		3 => CreateBrownBear(),      // Звериная балка — бурый медведь
		4 => CreateGangLeader(),     // Разбойничья застава — главарь шайки
		_ => CreateBanditElder(),
	};

	// Стартовый бой нового персонажа: волк-подранок на опушке леса.
	// HP/PhysDef рассчитаны так, чтобы голый персонаж (без оружия, Str~40) убил
	// его за 2-3 хода картами "strike" из стандартной WarriorDeck.
	// Lut гарантированный — это единственный способ выдать игроку стартовое
	// оружие и броню (EnsureDefaults для нового персонажа их не выдаёт).
	public static EnemyData CreateTrainingDummy()
	{
		var e = new EnemyData
		{
			EnemyName = "Тощий волк",
			MaxHp = 28,
			CurrentHp = 28,
			PhysDef = 0,
			MagicDef = 0,
			// Тутор-волк не дропает деньги — стартового лута и так гора.
			MoneyMin = 0,
			MoneyMax = 0,
			DropsResources = false,   // тутор не сорит крафт-сырьём.
		};
		e.Intents.Add(new Intent { Type = "attack", Amount = 4, Name = "Укус" });
		e.Intents.Add(new Intent { Type = "attack", Amount = 6, Name = "Бросок" });
		e.Intents.Add(new Intent { Type = "block",  Amount = 3, Name = "Скалит зубы" });

		// Гарантированный стартовый набор: меч + полный комплект кожанки + амулет + пара зелий.
		// Все Chance=1.0 — рандом не влияет, поэтому клиент и сервер получат
		// одинаковый лут даже не разворачивая RNG (он всё равно крутится в Rng.Chance).
		e.LootTable.Add(new LootEntry { ItemId = "sword_1h_low",           Chance = 1.0f });
		e.LootTable.Add(new LootEntry { ItemId = "light_chest_strength_low", Chance = 1.0f });
		e.LootTable.Add(new LootEntry { ItemId = "light_helmet_low",       Chance = 1.0f });
		e.LootTable.Add(new LootEntry { ItemId = "light_gloves_low",       Chance = 1.0f });
		e.LootTable.Add(new LootEntry { ItemId = "light_boots_low",        Chance = 1.0f });
		e.LootTable.Add(new LootEntry { ItemId = "amulet_might_low",       Chance = 1.0f });
		e.LootTable.Add(new LootEntry { ItemId = "potion_hp_small",        Chance = 1.0f, MinCount = 2, MaxCount = 2 });
		e.LootTable.Add(new LootEntry { ItemId = "potion_mp_small",        Chance = 1.0f });
		return e;
	}

	// ========= Мелочь (E-grade Low, 1-2 хода на голом персе) =========

	// Заяц — самый слабый враг. Прыжки, обычно бьёт мало. Хороший XP для новичка.
	public static EnemyData CreateWildHare()
	{
		var e = new EnemyData
		{
			EnemyName = "Заяц-русак",
			MaxHp = 18, CurrentHp = 18,
			PhysDef = 0, MagicDef = 0,
		};
		e.Intents.Add(new Intent { Type = "attack", Amount = 3, Name = "Прыжок", WeaponType = null });
		e.Intents.Add(new Intent { Type = "attack", Amount = 5, Name = "Двойной прыжок", WeaponType = null });
		e.Intents.Add(new Intent { Type = "block",  Amount = 2, Name = "Прижался" });
		e.LootTable.Add(new LootEntry { ItemId = "potion_hp_small", Chance = 0.10f });
		return e;
	}

	// Эльпи — мелкий лесной фей. Колется магически (бывает противно для воина без MagDef).
	public static EnemyData CreateElpy()
	{
		var e = new EnemyData
		{
			EnemyName = "Эльпи",
			MaxHp = 32, CurrentHp = 32,
			PhysDef = 0, MagicDef = 1,
		};
		e.Intents.Add(new Intent { Type = "attack",       Amount = 5, Name = "Тычок усиком", WeaponType = null });
		e.Intents.Add(new Intent { Type = "attack_magic", Amount = 7, Name = "Искра пыльцы", WeaponType = null });
		e.Intents.Add(new Intent { Type = "block",        Amount = 3, Name = "Зашуршал листьями" });
		e.LootTable.Add(new LootEntry { ItemId = "potion_mp_small", Chance = 0.35f });
		e.LootTable.Add(new LootEntry { ItemId = "potion_hp_small", Chance = 0.20f });
		return e;
	}

	// Лесной гоблин — base для боёв "стайкой" в лесу. (Оставил старое имя
	// для backward compat внутри файла.)
	public static EnemyData CreateForestGoblin()
	{
		var e = new EnemyData
		{
			EnemyName = "Гоблин-дикарь",
			MaxHp = 40, CurrentHp = 40,
			PhysDef = 1, MagicDef = 0,
		};
		e.Intents.Add(new Intent { Type = "attack", Amount = 6, Name = "Удар когтями", WeaponType = null });
		e.Intents.Add(new Intent { Type = "attack", Amount = 9, Name = "Засечка ножом", WeaponType = "knife" });
		e.Intents.Add(new Intent { Type = "block",  Amount = 4, Name = "Уклонение" });
		e.LootTable.Add(new LootEntry { ItemId = "potion_hp_small", Chance = 0.30f });
		e.LootTable.Add(new LootEntry { ItemId = "potion_mp_small", Chance = 0.15f });
		return e;
	}

	// ========= Средние (E-grade Low/Mid боевые) =========

	// Гоблин-разбойник: классический "с ножиком". Заменяет старого CreateGoblin.
	public static EnemyData CreateGoblinRogue()
	{
		var e = new EnemyData
		{
			EnemyName = "Гоблин-разбойник",
			MaxHp = 100, CurrentHp = 100,
			PhysDef = 2, MagicDef = 0,
		};
		e.Intents.Add(new Intent { Type = "attack", Amount = 12, Name = "Удар кинжалом", WeaponType = "knife" });
		e.Intents.Add(new Intent { Type = "attack", Amount = 17, Name = "Сильный замах", WeaponType = "sword_1h" });
		e.Intents.Add(new Intent { Type = "block",  Amount = 7,  Name = "Уклонение" });
		e.LootTable.Add(new LootEntry { ItemId = "potion_hp_small",   Chance = 0.60f });
		e.LootTable.Add(new LootEntry { ItemId = "potion_mp_small",   Chance = 0.30f });
		e.LootTable.Add(new LootEntry { ItemId = "potion_hp_medium",  Chance = 0.10f });
		e.LootTable.Add(new LootEntry { ItemId = "ring_power_low",    Chance = 0.15f, Affixed = true });
		e.LootTable.Add(new LootEntry { ItemId = "ring_focus_low",    Chance = 0.10f, Affixed = true });
		e.LootTable.Add(new LootEntry { ItemId = "amulet_might_low",  Chance = 0.08f, Affixed = true });
		e.LootTable.Add(new LootEntry { ItemId = "potion_strength",   Chance = 0.05f });
		e.LootTable.Add(new LootEntry { ItemId = "dagger_low",            Chance = 0.20f, Affixed = true });
		e.LootTable.Add(new LootEntry { ItemId = "shield_physical_low",   Chance = 0.10f });
		e.LootTable.Add(new LootEntry { ItemId = "shield_magic_low",      Chance = 0.07f });
		e.LootTable.Add(new LootEntry { ItemId = "shield_balanced_low",   Chance = 0.07f });
		return e;
	}

	// Гоблин-разведчик: быстрее, но слабее. Без оружия — голые руки.
	public static EnemyData CreateGoblinScout()
	{
		var e = new EnemyData
		{
			EnemyName = "Гоблин-разведчик",
			MaxHp = 70, CurrentHp = 70,
			PhysDef = 1, MagicDef = 0,
		};
		e.Intents.Add(new Intent { Type = "attack", Amount = 8,  Name = "Резкий выпад", WeaponType = "knife" });
		e.Intents.Add(new Intent { Type = "attack", Amount = 10, Name = "Двойной выпад", WeaponType = "knife" });
		e.Intents.Add(new Intent { Type = "block",  Amount = 5,  Name = "Отступление" });
		e.LootTable.Add(new LootEntry { ItemId = "potion_hp_small", Chance = 0.40f });
		e.LootTable.Add(new LootEntry { ItemId = "dagger_low",      Chance = 0.15f, Affixed = true });
		return e;
	}

	// Волк: атаки покусыванием. Толще зайца, без магии.
	public static EnemyData CreateWolf()
	{
		var e = new EnemyData
		{
			EnemyName = "Волк",
			MaxHp = 55, CurrentHp = 55,
			PhysDef = 1, MagicDef = 0,
		};
		e.Intents.Add(new Intent { Type = "attack", Amount = 7,  Name = "Укус", WeaponType = null });
		e.Intents.Add(new Intent { Type = "attack", Amount = 10, Name = "Прыжок-укус", WeaponType = null });
		e.Intents.Add(new Intent { Type = "block",  Amount = 5,  Name = "Прижал уши" });
		e.LootTable.Add(new LootEntry { ItemId = "potion_hp_small", Chance = 0.20f });
		return e;
	}

	// Гоблин-шаман: магические атаки. Мало HP но больный, особенно по тому
	// у кого слабый MagDef.
	public static EnemyData CreateGoblinShaman()
	{
		var e = new EnemyData
		{
			EnemyName = "Гоблин-шаман",
			MaxHp = 85, CurrentHp = 85,
			PhysDef = 1, MagicDef = 3,
		};
		e.Intents.Add(new Intent { Type = "attack_magic", Amount = 10, Name = "Болотная искра", WeaponType = null });
		e.Intents.Add(new Intent { Type = "attack_magic", Amount = 14, Name = "Пляска духов", WeaponType = null });
		e.Intents.Add(new Intent { Type = "block",        Amount = 8,  Name = "Защитный круг" });
		e.LootTable.Add(new LootEntry { ItemId = "potion_mp_medium", Chance = 0.30f });
		e.LootTable.Add(new LootEntry { ItemId = "ring_focus_low",   Chance = 0.15f, Affixed = true });
		e.LootTable.Add(new LootEntry { ItemId = "amulet_arcane_low", Chance = 0.08f, Affixed = true });
		return e;
	}

	// ========= Боссы локаций (всё ещё E-grade, без пафоса) =========

	// Locality 0 boss: старший разбойник.
	public static EnemyData CreateBanditElder()
	{
		var e = new EnemyData
		{
			EnemyName = "Старший разбойник",
			MaxHp = 180, CurrentHp = 180,
			PhysDef = 3, MagicDef = 1,
		};
		e.Intents.Add(new Intent { Type = "attack", Amount = 16, Name = "Удар мечом", WeaponType = "sword_1h" });
		e.Intents.Add(new Intent { Type = "attack", Amount = 22, Name = "Сокрушающий замах", WeaponType = "sword_1h" });
		e.Intents.Add(new Intent { Type = "block",  Amount = 12, Name = "Боевая стойка" });
		e.LootTable.Add(new LootEntry { ItemId = "ring_blessed_low",  Chance = 1.00f, Affixed = true });
		e.LootTable.Add(new LootEntry { ItemId = "potion_full",       Chance = 0.50f });
		e.LootTable.Add(new LootEntry { ItemId = "potion_hp_medium",  Chance = 1.00f });
		e.LootTable.Add(new LootEntry { ItemId = "potion_strength",   Chance = 0.50f });
		return e;
	}

	// Locality 1 boss: альфа стаи.
	public static EnemyData CreateAlphaWolf()
	{
		var e = new EnemyData
		{
			EnemyName = "Альфа-волк",
			MaxHp = 200, CurrentHp = 200,
			PhysDef = 3, MagicDef = 0,
		};
		e.Intents.Add(new Intent { Type = "attack", Amount = 18, Name = "Свирепый укус", WeaponType = null });
		e.Intents.Add(new Intent { Type = "attack", Amount = 26, Name = "Прыжок на загривок", WeaponType = null });
		e.Intents.Add(new Intent { Type = "block",  Amount = 14, Name = "Скалит клыки" });
		e.LootTable.Add(new LootEntry { ItemId = "amulet_might_low", Chance = 1.00f, Affixed = true });
		e.LootTable.Add(new LootEntry { ItemId = "potion_full",      Chance = 0.40f });
		e.LootTable.Add(new LootEntry { ItemId = "potion_hp_medium", Chance = 1.00f });
		e.LootTable.Add(new LootEntry { ItemId = "potion_strength",  Chance = 0.40f });
		return e;
	}

	// Locality 2 boss: вождь гоблинов.
	public static EnemyData CreateGoblinChief()
	{
		var e = new EnemyData
		{
			EnemyName = "Вождь гоблинов",
			MaxHp = 240, CurrentHp = 240,
			PhysDef = 4, MagicDef = 2,
		};
		e.Intents.Add(new Intent { Type = "attack",       Amount = 20, Name = "Тяжёлый замах",     WeaponType = "sword_2h" });
		e.Intents.Add(new Intent { Type = "attack_magic", Amount = 18, Name = "Зов тотема",        WeaponType = null });
		e.Intents.Add(new Intent { Type = "block",        Amount = 15, Name = "Ритуальный плащ" });
		e.LootTable.Add(new LootEntry { ItemId = "amulet_arcane_low", Chance = 1.00f, Affixed = true });
		e.LootTable.Add(new LootEntry { ItemId = "ring_blessed_low",  Chance = 0.50f, Affixed = true });
		e.LootTable.Add(new LootEntry { ItemId = "potion_full",       Chance = 0.80f });
		e.LootTable.Add(new LootEntry { ItemId = "potion_mp_medium",  Chance = 1.00f });
		e.LootTable.Add(new LootEntry { ItemId = "potion_focus",      Chance = 0.50f });
		return e;
	}

	// Backward-compat алиасы (на случай старых сейвов / тестов).
	[System.Obsolete("Use CreateGoblinRogue / CreateBanditElder / CreateAlphaWolf / CreateGoblinChief")]
	public static EnemyData CreateGoblin() => CreateGoblinRogue();
	[System.Obsolete("Use CreateBossFor(locationIndex) via SpawnFor")]
	public static EnemyData CreateBoss() => CreateBanditElder();

	public void RollIntent()
		=> NextIntent = Rng.Pick(Intents);

	// Детерминированная версия для CSP-боя: использует переданный RandomSource
	// вместо глобального Rng, чтобы клиент и сервер получили один Intent.
	public void RollIntent(RandomSource rng)
		=> NextIntent = rng != null ? rng.Pick(Intents) : Rng.Pick(Intents);

	public void AddEffect(string id, string type, float amount, int duration)
	{
		var existing = Effects.Find(e => e.Id == id);
		if (existing != null)
		{
			existing.Amount = amount;
			existing.Remaining = duration;
			return;
		}
		Effects.Add(new StatusEffect { Id = id, Type = type, Amount = amount, Remaining = duration });
	}

	public void TickEffects() => Effects.RemoveAll(e => --e.Remaining <= 0);

	public float GetEffectAmount(string type)
	{
		float total = 0f;
		foreach (var e in Effects)
			if (e.Type == type) total += e.Amount;
		return total;
	}

	public string DescribeIntent() => DescribeIntent(null);

	// Описание намерения с учётом блока/защиты игрока. Для интент-урона
	// показываем итоговое значение (с учётом текущего CurrentBlock + PhysDef/MagDef),
	// а в скобках — сырой удар, чтобы было видно "потенциал". Совпадают —
	// показываем одно число (без шума).
	public string DescribeIntent(CharacterData player)
	{
		if (NextIntent == null) return "...";
		var i = NextIntent;
		switch (i.Type)
		{
			case "attack":
			case "attack_magic":
			{
				if (player == null) return $"{i.Name} — урон {i.Amount}";
				bool isPhys = i.Type != "attack_magic";
				int raw = i.Amount;
				int afterBlock = System.Math.Max(0, raw - player.CurrentBlock);
				int def = isPhys ? player.PhysDef() : player.MagDef();
				int actual = System.Math.Max(0, afterBlock - def);
				return actual == raw
					? $"{i.Name} — урон {raw}"
					: $"{i.Name} — урон {actual} (из {raw})";
			}
			case "block":
				return $"{i.Name} — блок {i.Amount}";
			default:
				return i.Name;
		}
	}
}

// Запись в таблице лута: с указанной вероятностью при смерти врага падает
// MinCount..MaxCount единиц предмета itemId.
//
// Affixed (И6.2): если true и предмет — оружие/броня, при дропе проходит
// через ItemGenerator.RollArmor/RollWeapon — катается случайная rarity по
// весам RollRarity(grade) и аффиксы из AffixesDB по бюджету AffixBudget.
// Зелья и базы без аффиксов игнорируют этот флаг (стакаемые предметы
// держат только string Id в инвентаре, instance-данные негде хранить).
//
// CombatEngine.DropLoot реализует ролл (см. Combat.Cards.cs).
public class LootEntry
{
	public string ItemId;
	public float Chance = 1.0f;
	public int MinCount = 1;
	public int MaxCount = 1;
	public bool Affixed = false;
}

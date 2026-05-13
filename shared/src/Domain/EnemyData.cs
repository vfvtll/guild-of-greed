using System.Collections.Generic;

namespace GuildOfGreed.Shared.Domain;

// Враги работают по системе Intent: показывают своё намерение до своего хода.
// Не используют карты — только заранее заданный список действий.
//
// LootTable: при смерти Combat ролит каждую запись по её Chance и кладёт
// успешные дропы в инвентарь игрока (см. Combat.Cards.DropLoot).
public class EnemyData
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

	// Спавн encounter'а для узла на карте. Используется и клиентом
	// (GameData.SpawnForCurrentNode → display), и сервером (BattleSession
	// → authoritative). Чтобы анти-чит работал, обе стороны должны
	// вычислять одно и то же.
	public static List<EnemyData> SpawnFor(int locationIndex, MapNodeType nodeType)
	{
		var list = new List<EnemyData>();
		if (nodeType == MapNodeType.Tutorial)
		{
			list.Add(CreateTrainingDummy());
			return list;
		}
		if (nodeType == MapNodeType.Boss)
		{
			list.Add(CreateBoss());
			return list;
		}
		switch (locationIndex)
		{
			case 0: list.Add(CreateGoblin()); break;
			case 1: list.Add(CreateForestGoblin()); list.Add(CreateForestGoblin()); break;
			case 2: list.Add(CreateGoblin()); break;
			default: list.Add(CreateGoblin()); break;
		}
		return list;
	}

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

	// Слабый гоблин для леса — 5 шт. атакуют разом, поэтому каждый сильно слабее.
	public static EnemyData CreateForestGoblin()
	{
		var e = new EnemyData
		{
			EnemyName = "Гоблин",
			MaxHp = 40,
			CurrentHp = 40,
			PhysDef = 1,
			MagicDef = 0,
		};
		e.Intents.Add(new Intent { Type = "attack", Amount = 6,  Name = "Удар когтями" });
		e.Intents.Add(new Intent { Type = "attack", Amount = 9,  Name = "Засечка" });
		e.Intents.Add(new Intent { Type = "block",  Amount = 4,  Name = "Уклонение" });

		// Лесной гоблин дропает мало — игрок не должен через лес ломиться за лутом.
		e.LootTable.Add(new LootEntry { ItemId = "potion_hp_small", Chance = 0.30f });
		e.LootTable.Add(new LootEntry { ItemId = "potion_mp_small", Chance = 0.15f });
		return e;
	}

	// Goblin под игрока со статами 35..45 и ХП ~120.
	public static EnemyData CreateGoblin()
	{
		var e = new EnemyData
		{
			EnemyName = "Гоблин-разбойник",
			MaxHp = 100,
			CurrentHp = 100,
			PhysDef = 2,
			MagicDef = 0,
		};
		e.Intents.Add(new Intent { Type = "attack", Amount = 12, Name = "Удар кинжалом" });
		e.Intents.Add(new Intent { Type = "attack", Amount = 17, Name = "Сильный замах" });
		e.Intents.Add(new Intent { Type = "block",  Amount = 7,  Name = "Уклонение" });

		// Стандартный лут — гарантированно зелье, шанс на бижутерию с аффиксами.
		// Affixed=true на украшениях даёт рандомный rarity (по весам Grade=E:
		// 80% Common, 20% Uncommon) и аффиксы по бюджету (Common=1суф,
		// Uncommon=1преф+1суф).
		e.LootTable.Add(new LootEntry { ItemId = "potion_hp_small",   Chance = 0.60f });
		e.LootTable.Add(new LootEntry { ItemId = "potion_mp_small",   Chance = 0.30f });
		e.LootTable.Add(new LootEntry { ItemId = "potion_hp_medium",  Chance = 0.10f });
		e.LootTable.Add(new LootEntry { ItemId = "ring_power_low",    Chance = 0.15f, Affixed = true });
		e.LootTable.Add(new LootEntry { ItemId = "ring_focus_low",    Chance = 0.10f, Affixed = true });
		e.LootTable.Add(new LootEntry { ItemId = "amulet_might_low",  Chance = 0.08f, Affixed = true });
		e.LootTable.Add(new LootEntry { ItemId = "potion_strength",   Chance = 0.05f });
		// И6.4: dual-wield и щиты. Демо-частые шансы чтобы быстро увидеть.
		e.LootTable.Add(new LootEntry { ItemId = "dagger_low",            Chance = 0.20f, Affixed = true });
		e.LootTable.Add(new LootEntry { ItemId = "shield_physical_low",   Chance = 0.10f });
		e.LootTable.Add(new LootEntry { ItemId = "shield_magic_low",      Chance = 0.07f });
		e.LootTable.Add(new LootEntry { ItemId = "shield_balanced_low",   Chance = 0.07f });
		return e;
	}

	// Босс: 320 ХП, толстая защита, мощные удары. Реальный челлендж.
	public static EnemyData CreateBoss()
	{
		var e = new EnemyData
		{
			EnemyName = "Тёмный рыцарь",
			MaxHp = 320,
			CurrentHp = 320,
			PhysDef = 5,
			MagicDef = 2,
		};
		e.Intents.Add(new Intent { Type = "attack", Amount = 22, Name = "Удар алебардой" });
		e.Intents.Add(new Intent { Type = "attack", Amount = 30, Name = "Сокрушающий удар" });
		e.Intents.Add(new Intent { Type = "block",  Amount = 18, Name = "Кровавая стойка" });

		// Босс — щедрый: оба украшения с аффиксами (Rarity катается по grade=E,
		// но это редкие базы — обычно выпадает Uncommon с 1+1 аффиксом).
		e.LootTable.Add(new LootEntry { ItemId = "ring_blessed_low",  Chance = 1.00f, Affixed = true });
		e.LootTable.Add(new LootEntry { ItemId = "amulet_arcane_low", Chance = 0.40f, Affixed = true });
		e.LootTable.Add(new LootEntry { ItemId = "potion_full",       Chance = 0.50f });
		e.LootTable.Add(new LootEntry { ItemId = "potion_hp_medium",  Chance = 1.00f });
		e.LootTable.Add(new LootEntry { ItemId = "potion_mp_medium",  Chance = 1.00f });
		e.LootTable.Add(new LootEntry { ItemId = "potion_strength",   Chance = 0.50f });
		e.LootTable.Add(new LootEntry { ItemId = "potion_focus",      Chance = 0.50f });
		return e;
	}

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

	public string DescribeIntent()
	{
		if (NextIntent == null) return "...";
		return NextIntent.Type switch
		{
			"attack" => $"{NextIntent.Name} — урон {NextIntent.Amount}",
			"block"  => $"{NextIntent.Name} — блок {NextIntent.Amount}",
			_        => NextIntent.Name,
		};
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

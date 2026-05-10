using System.Collections.Generic;

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
	public int CurrentBlock = 0;
	public List<StatusEffect> Effects = new();
	public List<Intent> Intents = new();
	public Intent NextIntent;
	public List<LootEntry> LootTable = new();

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

		// Стандартный лут — гарантированно зелье, шанс на бижутерию.
		e.LootTable.Add(new LootEntry { ItemId = "potion_hp_small",   Chance = 0.60f });
		e.LootTable.Add(new LootEntry { ItemId = "potion_mp_small",   Chance = 0.30f });
		e.LootTable.Add(new LootEntry { ItemId = "potion_hp_medium",  Chance = 0.10f });
		e.LootTable.Add(new LootEntry { ItemId = "ring_power_low",    Chance = 0.05f });
		e.LootTable.Add(new LootEntry { ItemId = "ring_focus_low",    Chance = 0.05f });
		e.LootTable.Add(new LootEntry { ItemId = "amulet_might_low",  Chance = 0.03f });
		e.LootTable.Add(new LootEntry { ItemId = "potion_strength",   Chance = 0.05f });
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

		// Босс — щедрый: гарантированно редкое кольцо, шанс на эпик.
		e.LootTable.Add(new LootEntry { ItemId = "ring_blessed_low",  Chance = 1.00f });
		e.LootTable.Add(new LootEntry { ItemId = "amulet_arcane_low", Chance = 0.40f });
		e.LootTable.Add(new LootEntry { ItemId = "potion_full",       Chance = 0.50f });
		e.LootTable.Add(new LootEntry { ItemId = "potion_hp_medium",  Chance = 1.00f });
		e.LootTable.Add(new LootEntry { ItemId = "potion_mp_medium",  Chance = 1.00f });
		e.LootTable.Add(new LootEntry { ItemId = "potion_strength",   Chance = 0.50f });
		e.LootTable.Add(new LootEntry { ItemId = "potion_focus",      Chance = 0.50f });
		return e;
	}

	public void RollIntent()
		=> NextIntent = Rng.Pick(Intents);

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
public class LootEntry
{
	public string ItemId;
	public float Chance = 1.0f;
	public int MinCount = 1;
	public int MaxCount = 1;
}

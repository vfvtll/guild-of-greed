using System.Collections.Generic;
using Godot;

// Враги работают по системе Intent: показывают своё намерение до своего хода.
// Не используют карты — только заранее заданный список действий.
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
		return e;
	}

	// Goblin под игрока со статами 35..45 и ХП ~120.
	// Урон 12/17 — 7-10 ходов до смерти игрока.
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
		return e;
	}

	public void RollIntent()
		=> NextIntent = Intents[(int)(GD.Randi() % (uint)Intents.Count)];

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

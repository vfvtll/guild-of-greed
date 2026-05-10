using System.Collections.Generic;
using Godot;

// Персонаж: статы Lineage 2 стиля.
//   STR — физ. атака
//   INT — маг. атака
//   CON — ХП
//   WIT — реген МП
//   MEN — макс. МП
public class CharacterData
{
	public string CharacterName = "Авантюрист";
	public int Level = 1;
	public string Grade = "E";
	public int Exp = 0;

	// Статы выкатываются один раз при создании персонажа: 35..45.
	public int Str;
	public int Int;
	public int Con;
	public int Wit;
	public int Men;

	public WeaponData Weapon;
	public ArmorData Armor;

	public int CurrentHp;
	public int CurrentMp;
	public int CurrentBlock;
	public List<StatusEffect> Effects = new();

	public CharacterData()
	{
		RerollStats();
	}

	public void RerollStats()
	{
		Str = RollStat();
		Int = RollStat();
		Con = RollStat();
		Wit = RollStat();
		Men = RollStat();
	}

	private static int RollStat() => (int)(GD.Randi() % 11) + 35;  // 35..45 включительно

	// === Производные параметры ===
	// Формулы подобраны под статы 35..45.
	public int MaxHp()
	{
		int b = 40 + Con * 2;          // CON 40 → 120 ХП
		if (Armor != null) b += Armor.HpBonus;
		return b;
	}

	public int MaxMp()
	{
		int b = 30 + Men;               // MEN 40 → 70 МП (без брони). С робой +30 = 100.
		if (Armor != null) b += Armor.MpMaxBonus;
		return b;
	}

	public int MpRegen()
	{
		int b = Wit / 3;                // WIT 40 → 13 МП/ход (без брони). Тугая экономика.
		if (Armor != null) b += Armor.MpRegenBonus;
		return b;
	}

	public int HandSize()
	{
		int b = 5;
		if (Weapon != null) b += Weapon.ExtraDraw;
		if (Armor != null) b += Armor.ExtraDrawBonus;
		return b;
	}

	public float PhysMult()      => Weapon?.PhysMult ?? 1.0f;
	public float MagicMult()     => Weapon?.MagicMult ?? 1.0f;
	public int   WeaponPhysAtk() => Weapon?.PhysAtk ?? 0;
	public int   WeaponMagAtk()  => Weapon?.MagicAtk ?? 0;
	public int   PhysAtkBonus()  => Armor?.PhysAtkBonus ?? 0;
	public int   MagicAtkBonus() => Armor?.MagicAtkBonus ?? 0;
	public int   MagicAtkPct()   => Armor?.MagicAtkPct ?? 0;
	public int   PhysDef()       => Armor?.PhysDef ?? 0;

	public void ResetForCombat()
	{
		CurrentHp = MaxHp();
		CurrentMp = MaxMp();
		CurrentBlock = 0;
		Effects.Clear();
	}

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
}

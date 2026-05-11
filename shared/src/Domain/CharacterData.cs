using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace GuildOfGreed.Shared.Domain;

// Персонаж: статы Lineage 2 стиля.
//   STR — физ. атака
//   INT — маг. атака
//   CON — ХП
//   WIT — реген МП
//   MEN — макс. МП
//   DEX — блок, частота крита, множитель крита
//
// Сохраняется в JSON: имя, уровни, статы, ID-ы надетых предметов, инвентарь.
// Не сохраняется ([JsonIgnore]): прямые ссылки на WeaponData/ArmorData
// (резолвятся при загрузке через ItemsDB), боевое состояние (HP, MP, эффекты).
public class CharacterData
{
	public string CharacterName = "Авантюрист";
	public int Level = 1;
	public string Grade = "E";
	public int Exp = 0;

	// Статы (35..45 + распределённые игроком 10 очков).
	public int Str;
	public int Int;
	public int Con;
	public int Wit;
	public int Men;
	public int Dex;

	// === Экипировка (ID-ы; реальные объекты резолвятся через ResolveEquipment) ===
	public string EquippedWeaponId = "";
	public string EquippedChestId  = "";
	public string EquippedHelmetId = "";
	public string EquippedGlovesId = "";
	public string EquippedBootsId  = "";
	public string EquippedAmuletId = "";
	public string EquippedRing1Id  = "";
	public string EquippedRing2Id  = "";

	// === Инвентарь (с лимитом по слотам) ===
	public Inventory Inventory = new();

	// === Прямые ссылки (рантайм, после ResolveEquipment) ===
	[JsonIgnore] public WeaponData Weapon;
	[JsonIgnore] public ArmorData Chest;
	[JsonIgnore] public ArmorData Helmet;
	[JsonIgnore] public ArmorData Gloves;
	[JsonIgnore] public ArmorData Boots;
	[JsonIgnore] public ArmorData Amulet;
	[JsonIgnore] public ArmorData Ring1;
	[JsonIgnore] public ArmorData Ring2;

	// === Боевое состояние (рантайм) ===
	[JsonIgnore] public int CurrentHp;
	[JsonIgnore] public int CurrentMp;
	[JsonIgnore] public int CurrentBlock;
	[JsonIgnore] public List<StatusEffect> Effects = new();
	[JsonIgnore] public int AttacksSinceLastCrit;

	public CharacterData() { }

	public static CharacterData CreateRandom()
	{
		var c = new CharacterData();
		c.RerollStats();
		return c;
	}

	public void RerollStats()
	{
		Str = RollStat();
		Int = RollStat();
		Con = RollStat();
		Wit = RollStat();
		Men = RollStat();
		Dex = RollStat();
	}

	private static int RollStat() => Rng.Range(35, 46);

	// === Производные параметры (с учётом всех 4 слотов брони) ===
	public int MaxHp()    => 40 + Con * 2 + SumArmor(a => a.HpBonus);
	public int MaxMp()    => 30 + Men + SumArmor(a => a.MpMaxBonus);
	public int MpRegen()  => Wit / 3 + SumArmor(a => a.MpRegenBonus);
	public int HandSize() => 5 + (Weapon?.ExtraDraw ?? 0) + SumArmor(a => a.ExtraDrawBonus);

	public float PhysMult()      => Weapon?.PhysMult ?? 1.0f;
	public float MagicMult()     => Weapon?.MagicMult ?? 1.0f;
	public int   WeaponPhysAtk() => Weapon?.PhysAtk ?? 0;
	public int   WeaponMagAtk()  => Weapon?.MagicAtk ?? 0;
	public int   PhysAtkBonus()  => SumArmor(a => a.PhysAtkBonus);
	public int   MagicAtkBonus() => SumArmor(a => a.MagicAtkBonus);
	public int   MagicAtkPct()   => SumArmor(a => a.MagicAtkPct);
	public int   PhysDef()       => SumArmor(a => a.PhysDef);

	// Итерация по всем надетым кускам брони + бижутерии (без null'ов).
	public IEnumerable<ArmorData> AllArmor()
	{
		if (Chest  != null) yield return Chest;
		if (Helmet != null) yield return Helmet;
		if (Gloves != null) yield return Gloves;
		if (Boots  != null) yield return Boots;
		if (Amulet != null) yield return Amulet;
		if (Ring1  != null) yield return Ring1;
		if (Ring2  != null) yield return Ring2;
	}

	private int SumArmor(Func<ArmorData, int> selector)
	{
		int sum = 0;
		foreach (var a in AllArmor()) sum += selector(a);
		return sum;
	}

	public ArmorData GetArmorSlot(ArmorSlot slot) => slot switch
	{
		ArmorSlot.Chest  => Chest,
		ArmorSlot.Helmet => Helmet,
		ArmorSlot.Gloves => Gloves,
		ArmorSlot.Boots  => Boots,
		ArmorSlot.Amulet => Amulet,
		ArmorSlot.Ring1  => Ring1,
		ArmorSlot.Ring2  => Ring2,
		_                => null,
	};

	public void SetArmorSlot(ArmorSlot slot, ArmorData data)
	{
		switch (slot)
		{
			case ArmorSlot.Chest:  Chest  = data; break;
			case ArmorSlot.Helmet: Helmet = data; break;
			case ArmorSlot.Gloves: Gloves = data; break;
			case ArmorSlot.Boots:  Boots  = data; break;
			case ArmorSlot.Amulet: Amulet = data; break;
			case ArmorSlot.Ring1:  Ring1  = data; break;
			case ArmorSlot.Ring2:  Ring2  = data; break;
		}
	}

	// === Крит (детерминированный счётчик ударов) ===
	public int EffectiveCritEveryN()
	{
		if (Weapon == null) return int.MaxValue;
		return Math.Max(2, Weapon.CritEveryNAttacks - Dex / 10);
	}

	public float CritMultiplier() => 1.5f + Dex / 100f;

	public bool TryConsumeCrit()
	{
		if (Weapon == null) return false;
		int effective = EffectiveCritEveryN();
		AttacksSinceLastCrit++;
		if (AttacksSinceLastCrit >= effective)
		{
			AttacksSinceLastCrit = 0;
			return true;
		}
		return false;
	}

	// === Боевые методы ===
	public void ResetForCombat()
	{
		CurrentHp = MaxHp();
		CurrentMp = MaxMp();
		CurrentBlock = 0;
		Effects.Clear();
		AttacksSinceLastCrit = 0;
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

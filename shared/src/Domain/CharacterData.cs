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
// Сохраняется в JSON: имя, уровни, статы, экипировка (Weapon/Chest/etc.
// инстансы с аффиксами), инвентарь, текущие HP/MP.
// Не сохраняется ([JsonIgnore]): per-battle поля (CurrentBlock, Effects,
// AttacksSinceLastCrit) — резолвятся в начале каждого боя.
public class CharacterData
{
	// Стабильный идентификатор для серверной БД и для выбора слота.
	// Заполняется при создании; в старых клиентских сейвах будет Guid.Empty
	// и должен быть установлен при первом резолве (см. SaveGame.Migrate).
	public Guid Id = Guid.NewGuid();

	public string CharacterName = "Авантюрист";
	public int Level = 1;
	public string Grade = "E";
	public int Exp = 0;

	// true = персонаж только что создан и ещё не прошёл стартовый бой/обучение.
	// Default = false, чтобы старые сейвы (до этого поля) не получили повторный
	// тутор. Серверный HandleCreateCharacter явно выставляет true для новых.
	// Сбрасывается в false после победы в Tutorial-узле + UpdateCharacter.
	public bool IsNewCharacter = false;

	// Статы (35..45 + распределённые игроком 10 очков).
	public int Str;
	public int Int;
	public int Con;
	public int Wit;
	public int Men;
	public int Dex;

	// === Экипировка ===
	// Хранится как полноценные instance-объекты (с аффиксами), а не как
	// строковые Id. До И6.2 здесь были EquippedXxxId + резолв через
	// CharacterEquipmentResolver — это теряло аффиксы при equip/unequip.
	// Теперь сериализуется напрямую в JSON: вместе с предметом сохраняются
	// и его роллнутые Affixes / Rarity.
	public WeaponData Weapon;
	// Off-hand слот (И6.4): второе одноручное оружие (dual-wield) ИЛИ щит,
	// но не оба сразу. Только один из Offhand/Shield не-null в каждый момент.
	// Если Weapon.IsTwoHanded — оба off-hand'а должны быть null.
	public WeaponData Offhand;
	public ShieldData Shield;
	public ArmorData Chest;
	public ArmorData Helmet;
	public ArmorData Gloves;
	public ArmorData Boots;
	public ArmorData Amulet;
	public ArmorData Ring1;
	public ArmorData Ring2;

	// === Инвентарь (с лимитом по слотам) ===
	public Inventory Inventory = new();

	// === Городской стэш ===
	// Хранится в JSON. Старые сейвы без поля → new Stash() default
	// (System.Text.Json не затирает поле, если ключа в JSON нет).
	// Перенос items из Inventory.Stash и обратно — в GameData / клиентский UI;
	// сервер просто персистит обновлённый character_json при следующем бое.
	public Stash Stash = new();

	// === Боевое состояние ===
	// CurrentHp/CurrentMp — PERSIST между боями: расход здоровья и маны в одном
	// бою влияет на следующий. Восстановление только при StartRun (новый забег)
	// и при смерти в подземелье (server делает full reset перед UpdateCharacter).
	public int CurrentHp;
	public int CurrentMp;
	// Перечисленные ниже — чисто per-battle, не сохраняются.
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

	// === Производные параметры (с учётом всех 4 слотов брони + аффиксов) ===
	//
	// Аффиксы (И6.2): префиксы суммируются как плоские бонусы в `flat`,
	// суффиксы как процентные — применяются ОДНИМ множителем в конце формулы.
	// Формула: result = (base + flat) * (1 + suffixPct / 100).
	// Round-half-to-even, но Min=0 чтобы не уйти в отрицательное.
	public int MaxHp()    => ApplyAffix(40 + Con * 2 + SumArmor(a => a.HpBonus), AffixStatKind.Hp);
	public int MaxMp()    => ApplyAffix(30 + Men + SumArmor(a => a.MpMaxBonus), AffixStatKind.Mp);
	public int MpRegen()  => ApplyAffix(Wit / 3 + SumArmor(a => a.MpRegenBonus), AffixStatKind.MpRegen);
	// HpRegen (И6.2) — новый ресурс. База 0 от персонажа, копится через аффиксы
	// и (в будущем) через сеты. Тикает в начале хода игрока в CombatEngine.
	public int HpRegen()  => ApplyAffix(0, AffixStatKind.HpRegen);
	// HandSize: 5 базово + ExtraDraw оружия + бонусы брони + аффиксы/сеты.
	// Штрафы за off-hand: dual-wield −2, щит −1 (И6.4).
	public int HandSize()
	{
		int n = 5 + (Weapon?.ExtraDraw ?? 0) + SumArmor(a => a.ExtraDrawBonus);
		if (Offhand != null) n -= 2;
		else if (Shield != null) n -= 1;
		return System.Math.Max(1, n);
	}

	public float PhysMult()      => Weapon?.PhysMult ?? 1.0f;
	public float MagicMult()     => Weapon?.MagicMult ?? 1.0f;
	// Статы суммируются с off-hand оружием (И6.4 — dual-wield). Шит не даёт
	// атаки; его Phys/MagDef учитываются в PhysDef()/MagDef() ниже.
	public int   WeaponPhysAtk() => (Weapon?.PhysAtk ?? 0) + (Offhand?.PhysAtk ?? 0);
	public int   WeaponMagAtk()  => (Weapon?.MagicAtk ?? 0) + (Offhand?.MagicAtk ?? 0);
	public int   PhysAtkBonus()  => SumArmor(a => a.PhysAtkBonus) + AffixFlat(AffixStatKind.PhysAtk) + SetFlat(AffixStatKind.PhysAtk);
	public int   MagicAtkBonus() => SumArmor(a => a.MagicAtkBonus) + AffixFlat(AffixStatKind.MagAtk) + SetFlat(AffixStatKind.MagAtk);
	public int   MagicAtkPct()   => SumArmor(a => a.MagicAtkPct) + AffixPct(AffixStatKind.MagAtk) + SetPct(AffixStatKind.MagAtk);
	public int   PhysAtkPct()    => AffixPct(AffixStatKind.PhysAtk) + SetPct(AffixStatKind.PhysAtk);
	public int   PhysDef()       => ApplyAffix(SumArmor(a => a.PhysDef) + (Shield?.PhysDef ?? 0), AffixStatKind.PhysDef);
	public int   MagDef()        => ApplyAffix(Shield?.MagDef ?? 0, AffixStatKind.MagDef);

	// Сумма плоских префиксов выбранного типа со всех надетых предметов
	// (оружие + броня + бижутерия). Использует и Weapon, и AllArmor.
	public int AffixFlat(AffixStatKind kind)
	{
		int sum = 0;
		if (Weapon != null)
			foreach (var aff in Weapon.Affixes)
				if (aff.Slot == AffixSlot.Prefix && aff.Kind == kind) sum += aff.Magnitude;
		foreach (var armor in AllArmor())
			foreach (var aff in armor.Affixes)
				if (aff.Slot == AffixSlot.Prefix && aff.Kind == kind) sum += aff.Magnitude;
		return sum;
	}

	// Сумма процентных суффиксов выбранного типа. Возвращает целое в процентах:
	// 15 значит +15%. Применяется через ApplyAffix или вручную в Compute*.
	public int AffixPct(AffixStatKind kind)
	{
		int sum = 0;
		if (Weapon != null)
			foreach (var aff in Weapon.Affixes)
				if (aff.Slot == AffixSlot.Suffix && aff.Kind == kind) sum += aff.Magnitude;
		foreach (var armor in AllArmor())
			foreach (var aff in armor.Affixes)
				if (aff.Slot == AffixSlot.Suffix && aff.Kind == kind) sum += aff.Magnitude;
		return sum;
	}

	// (base + flat) * (1 + pct/100), округлено вниз с минимумом 0.
	// flat = аффиксы + сеты; pct = аффиксы + сеты.
	private int ApplyAffix(int baseValue, AffixStatKind kind)
	{
		int flat = AffixFlat(kind) + SetFlat(kind);
		int pct  = AffixPct(kind)  + SetPct(kind);
		double v = (baseValue + flat) * (1.0 + pct / 100.0);
		if (v < 0) v = 0;
		return (int)System.Math.Round(v);
	}

	// === Сеты (И6.2-D) =====================================================
	// Активные сеты на персонаже = setId → число надетых частей этого сета.
	// Подсчёт идёт только по AllArmor — оружие в сеты пока не входит.
	public Dictionary<string, int> ActiveSets()
	{
		var counts = new Dictionary<string, int>();
		foreach (var armor in AllArmor())
		{
			if (string.IsNullOrEmpty(armor.SetId)) continue;
			counts.TryGetValue(armor.SetId, out int n);
			counts[armor.SetId] = n + 1;
		}
		return counts;
	}

	public int SetFlat(AffixStatKind kind)
	{
		int sum = 0;
		foreach (var kv in ActiveSets())
		{
			var set = SetsDB.Get(kv.Key);
			if (set == null) continue;
			foreach (var b in SetsDB.ActiveBonusesFor(set, kv.Value))
				if (!b.IsPercent && b.Kind == kind) sum += b.Magnitude;
		}
		return sum;
	}

	public int SetPct(AffixStatKind kind)
	{
		int sum = 0;
		foreach (var kv in ActiveSets())
		{
			var set = SetsDB.Get(kv.Key);
			if (set == null) continue;
			foreach (var b in SetsDB.ActiveBonusesFor(set, kv.Value))
				if (b.IsPercent && b.Kind == kind) sum += b.Magnitude;
		}
		return sum;
	}

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
		int baseCrit = Weapon.CritEveryNAttacks;
		// Dual-wield (И6.4): среднее cooldown'а двух одноручных. Уникальный
		// пассив второго оружия НЕ работает, но статы (CritEveryNAttacks) —
		// усредняются с основным.
		if (Offhand != null)
			baseCrit = (Weapon.CritEveryNAttacks + Offhand.CritEveryNAttacks) / 2;
		return Math.Max(2, baseCrit - Dex / 10);
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

	// Полный restore. Вызывается при StartRun (новый забег) и сервером при
	// смерти игрока в подземелье (чтобы после relog можно было играть дальше).
	public void ResetForCombat()
	{
		CurrentHp = MaxHp();
		CurrentMp = MaxMp();
		CurrentBlock = 0;
		Effects.Clear();
		AttacksSinceLastCrit = 0;
	}

	// Подготовка перед новым боем В ТЕКУЩЕМ забеге: HP/MP переносятся как есть,
	// блок и эффекты обнуляются (они per-battle), счётчик крита тоже.
	public void PrepareForBattle()
	{
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

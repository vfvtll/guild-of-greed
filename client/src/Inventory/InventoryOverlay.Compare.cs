using System.Collections.Generic;
using GuildOfGreed.Shared.Domain;
using GuildOfGreed.Shared.Data;

// InventoryOverlay — описание предметов и сравнение надетого vs выбранного.
//
// Сравнение учитывает: базовые статы предмета + накатанные аффиксы (flat и pct
// раздельно). Это критично — без учёта аффиксов "Меч +5 ФизАтк" и "Меч +0 ФизАтк"
// выглядели бы одинаково, и игрок принимал бы неверные решения.
public partial class InventoryOverlay
{
	// Тултип для предмета в инвентаре. Включает название, краткие статы
	// и diff против того, что сейчас надето в этом слоте.
	private static string BuildItemTooltip(InventoryStack stack, string name, string detail, CharacterData ch)
	{
		var sb = new System.Text.StringBuilder();
		sb.Append(name);
		sb.Append('\n');
		sb.Append(detail);

		if (ch == null) return sb.ToString();

		// Если в стаке instance — сравниваем именно его, а не базу из ItemsDB
		// (у instance аффиксы важны для сравнения).
		ArmorData armor = stack.ArmorInstance ?? ItemsDB.GetArmor(stack.ItemId);
		if (armor != null && stack.WeaponInstance == null)
		{
			AppendSetInfo(sb, armor, ch);
			var current = ch.GetArmorSlot(armor.Slot);
			sb.Append("\n\nСравнение со слотом:\n");
			sb.Append(CompareArmor(armor, current));
			return sb.ToString();
		}
		WeaponData weapon = stack.WeaponInstance ?? ItemsDB.GetWeapon(stack.ItemId);
		if (weapon != null)
		{
			sb.Append("\n\nСравнение с оружием:\n");
			sb.Append(CompareWeapon(weapon, ch.Weapon));
			return sb.ToString();
		}
		// Зелья — без сравнения.
		return sb.ToString();
	}

	// Эффективные характеристики предмета: словари flat[Kind] и pct[Kind].
	// Собираются из базовых полей предмета + всех аффиксов экземпляра.
	private struct ItemTotals
	{
		public Dictionary<AffixStatKind, int> Flat;
		public Dictionary<AffixStatKind, int> Pct;
		public int ExtraDraw;
		public int CritEveryN;       // 0 если неприменимо (броня).

		public static ItemTotals Empty() => new()
		{
			Flat = new Dictionary<AffixStatKind, int>(),
			Pct = new Dictionary<AffixStatKind, int>(),
		};
	}

	private static void Add(Dictionary<AffixStatKind, int> d, AffixStatKind k, int v)
	{
		if (v == 0) return;
		d.TryGetValue(k, out int prev);
		d[k] = prev + v;
	}

	private static void AddAffixes(ItemTotals t, List<AppliedAffix> affixes)
	{
		if (affixes == null) return;
		foreach (var a in affixes)
		{
			var dict = a.IsPercent ? t.Pct : t.Flat;
			Add(dict, a.Kind, a.Magnitude);
		}
	}

	private static ItemTotals TotalsFor(ArmorData a)
	{
		var t = ItemTotals.Empty();
		if (a == null) return t;
		Add(t.Flat, AffixStatKind.PhysDef, a.PhysDef);
		Add(t.Flat, AffixStatKind.PhysAtk, a.PhysAtkBonus);
		Add(t.Flat, AffixStatKind.MagAtk,  a.MagicAtkBonus);
		Add(t.Pct,  AffixStatKind.MagAtk,  a.MagicAtkPct);
		Add(t.Flat, AffixStatKind.Hp,      a.HpBonus);
		Add(t.Flat, AffixStatKind.Mp,      a.MpMaxBonus);
		Add(t.Flat, AffixStatKind.MpRegen, a.MpRegenBonus);
		t.ExtraDraw = a.ExtraDrawBonus;
		AddAffixes(t, a.Affixes);
		return t;
	}

	private static ItemTotals TotalsFor(WeaponData w)
	{
		var t = ItemTotals.Empty();
		if (w == null) return t;
		Add(t.Flat, AffixStatKind.PhysAtk, w.PhysAtk);
		Add(t.Flat, AffixStatKind.MagAtk,  w.MagicAtk);
		t.ExtraDraw = w.ExtraDraw;
		t.CritEveryN = w.CritEveryNAttacks;
		AddAffixes(t, w.Affixes);
		return t;
	}

	private static string CompareArmor(ArmorData @new, ArmorData old)
	{
		if (old == null) return "  🆕 Слот пуст — будет надето сразу";
		if (old.Id == @new.Id && SameAffixes(@new.Affixes, old.Affixes))
			return "  = Этот же предмет";

		var n = TotalsFor(@new);
		var o = TotalsFor(old);
		var deltas = new List<string>();
		AppendKindDeltas(deltas, n, o);
		AddDelta(deltas, "Карты", n.ExtraDraw - o.ExtraDraw);
		return deltas.Count == 0 ? "  Без изменений" : string.Join("\n", deltas);
	}

	private static string CompareWeapon(WeaponData @new, WeaponData old)
	{
		if (old == null) return "  🆕 Слот пуст — будет надето сразу";
		if (old.Id == @new.Id && SameAffixes(@new.Affixes, old.Affixes))
			return "  = То же оружие";

		var n = TotalsFor(@new);
		var o = TotalsFor(old);
		var deltas = new List<string>();
		AppendKindDeltas(deltas, n, o);
		AddDelta(deltas,      "Карты",    n.ExtraDraw - o.ExtraDraw);

		// Крит: меньше CritEveryN = лучше → инвертируем знак, чтобы "+ это улучшение".
		int dCrit = o.CritEveryN - n.CritEveryN;
		if (dCrit != 0)
		{
			string sign = dCrit > 0 ? "+" : "";
			deltas.Add($"  {sign}{dCrit} к скорости крита (− кулдаун)");
		}

		// Пассивы оружия — текстовое сравнение списков. Сравнить magnitude
		// разных kind'ов осмысленно нельзя, поэтому при любом расхождении
		// выводим обе строки.
		string newPassives = DescribePassives(@new.Passives);
		string oldPassives = DescribePassives(old.Passives);
		if (newPassives != oldPassives)
		{
			deltas.Add($"  🌟 Пассив: {newPassives}");
			deltas.Add($"     было: {oldPassives}");
		}

		return deltas.Count == 0 ? "  Без изменений" : string.Join("\n", deltas);
	}

	private static string DescribePassives(List<WeaponPassive> passives)
	{
		if (passives == null || passives.Count == 0) return "—";
		var parts = new List<string>(passives.Count);
		foreach (var p in passives)
		{
			if (string.IsNullOrEmpty(p?.Kind)) continue;
			parts.Add(p.Magnitude2 != 0
				? $"{p.Kind} ({p.Magnitude}/{p.Magnitude2})"
				: $"{p.Kind} ({p.Magnitude})");
		}
		// Отсортируем чтобы порядок в Passives-листе не менял diff-результат.
		parts.Sort(System.StringComparer.Ordinal);
		return parts.Count == 0 ? "—" : string.Join(", ", parts);
	}

	// Сравнение двух наборов аффиксов как multiset: сортируем по (Slot, Kind, Mag)
	// и сравниваем поэлементно. Используется чтобы отличать «тот же base id, но
	// разные роллы» от «полностью идентичный экземпляр».
	private static bool SameAffixes(List<AppliedAffix> a, List<AppliedAffix> b)
	{
		int ac = a?.Count ?? 0, bc = b?.Count ?? 0;
		if (ac != bc) return false;
		if (ac == 0) return true;
		var aSorted = new List<AppliedAffix>(a);
		var bSorted = new List<AppliedAffix>(b);
		aSorted.Sort(CompareAffix);
		bSorted.Sort(CompareAffix);
		for (int i = 0; i < ac; i++)
		{
			if (aSorted[i].Slot != bSorted[i].Slot) return false;
			if (aSorted[i].Kind != bSorted[i].Kind) return false;
			if (aSorted[i].Magnitude != bSorted[i].Magnitude) return false;
		}
		return true;
	}

	private static int CompareAffix(AppliedAffix x, AppliedAffix y)
	{
		int c = ((int)x.Slot).CompareTo((int)y.Slot);
		if (c != 0) return c;
		c = ((int)x.Kind).CompareTo((int)y.Kind);
		if (c != 0) return c;
		return x.Magnitude.CompareTo(y.Magnitude);
	}

	// Перебирает все Kind встречающиеся хотя бы у одного из двух предметов и
	// добавляет дельту flat / pct. Это покрывает кейс «у нового аффикс HP +12,
	// у старого ничего» — игрок увидит «+12 ХП». StatName тянем из AffixesDB —
	// единый источник русских названий.
	private static void AppendKindDeltas(List<string> deltas, ItemTotals n, ItemTotals o)
	{
		var kinds = new HashSet<AffixStatKind>();
		foreach (var k in n.Flat.Keys) kinds.Add(k);
		foreach (var k in o.Flat.Keys) kinds.Add(k);
		foreach (var k in n.Pct.Keys)  kinds.Add(k);
		foreach (var k in o.Pct.Keys)  kinds.Add(k);

		// Стабильный порядок вывода — по индексу enum, не по hash.
		var ordered = new List<AffixStatKind>(kinds);
		ordered.Sort((a, b) => ((int)a).CompareTo((int)b));

		foreach (var k in ordered)
		{
			n.Flat.TryGetValue(k, out int nf);
			o.Flat.TryGetValue(k, out int of);
			n.Pct.TryGetValue(k, out int np);
			o.Pct.TryGetValue(k, out int op);
			AddDelta(deltas, AffixesDB.StatName(k), nf - of);
			AddDelta(deltas, "% " + AffixesDB.StatName(k), np - op);
		}
	}

	private static void AddDelta(List<string> list, string label, int diff)
	{
		if (diff == 0) return;
		string sign = diff > 0 ? "+" : "";
		list.Add($"  {sign}{diff} {label}");
	}

	// =====================================================================
	// Описание предмета (имя/детали/иконка) для UI
	// =====================================================================
	private static (string name, string detail, string icon) DescribeItem(string id)
	{
		var w = ItemsDB.GetWeapon(id);
		if (w != null) return (w.Name, ItemsDB.DescribeWeaponMultiline(w), "⚔");
		var a = ItemsDB.GetArmor(id);
		if (a != null) return (a.Name, ItemsDB.DescribeArmorMultiline(a), SlotIcon(a.Slot));

		var p = PotionsDB.Get(id);
		if (p != null) return (p.Name, p.Description, p.Icon);

		return (id, "—", "?");
	}

	// Редкость для подкраски рамки в инвентаре.
	private static ItemRarity GetItemRarity(string id)
	{
		var w = ItemsDB.GetWeapon(id);
		if (w != null) return w.Rarity;
		var a = ItemsDB.GetArmor(id);
		if (a != null) return a.Rarity;
		var p = PotionsDB.Get(id);
		if (p != null) return p.Rarity;
		return ItemRarity.Common;
	}

	private static string SlotIcon(ArmorSlot s) => s switch
	{
		ArmorSlot.Chest  => "👕",
		ArmorSlot.Helmet => "⛑",
		ArmorSlot.Gloves => "🧤",
		ArmorSlot.Boots  => "👢",
		ArmorSlot.Amulet => "📿",
		ArmorSlot.Ring1  => "💍",
		ArmorSlot.Ring2  => "💍",
		_                => "🛡",
	};

	// Если у предмета есть SetId — добавить в тултип имя сета и текущий
	// прогресс (сколько частей уже надето из общего).
	private static void AppendSetInfo(System.Text.StringBuilder sb, ArmorData armor, CharacterData ch)
	{
		if (armor == null || string.IsNullOrEmpty(armor.SetId)) return;
		var set = SetsDB.Get(armor.SetId);
		if (set == null) return;
		int total = set.PartIds.Count;
		int current = 0;
		if (ch != null)
		{
			ch.ActiveSets().TryGetValue(set.Id, out current);
		}
		sb.Append($"\n\n🔗 Сет: {set.Name} ({current}/{total})");
		foreach (var b in set.Bonuses)
		{
			string sign = b.IsPercent ? "%" : "";
			string state = current >= b.RequiredParts ? "✓" : "·";
			sb.Append($"\n  {state} ({b.RequiredParts}) +{b.Magnitude}{sign} {AffixesDB.StatName(b.Kind)}");
		}
	}
}

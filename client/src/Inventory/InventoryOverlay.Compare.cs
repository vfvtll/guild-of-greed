using System.Collections.Generic;
using GuildOfGreed.Shared.Domain;
using GuildOfGreed.Shared.Data;

// InventoryOverlay — описание предметов и сравнение надетого vs выбранного.
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

	private static string CompareArmor(ArmorData @new, ArmorData old)
	{
		if (old == null) return "  🆕 Слот пуст — будет надето сразу";
		if (old.Id == @new.Id) return "  = Этот же предмет";
		var deltas = new List<string>();
		AddDelta(deltas, "ФизЗащ",   @new.PhysDef        - old.PhysDef);
		AddDelta(deltas, "ХП",       @new.HpBonus        - old.HpBonus);
		AddDelta(deltas, "МакМП",    @new.MpMaxBonus     - old.MpMaxBonus);
		AddDelta(deltas, "РегМП",    @new.MpRegenBonus   - old.MpRegenBonus);
		AddDelta(deltas, "ФизАтк",   @new.PhysAtkBonus   - old.PhysAtkBonus);
		AddDelta(deltas, "МагАтк",   @new.MagicAtkBonus  - old.MagicAtkBonus);
		AddDelta(deltas, "%МагАтк",  @new.MagicAtkPct    - old.MagicAtkPct);
		AddDelta(deltas, "Карты",    @new.ExtraDrawBonus - old.ExtraDrawBonus);
		return deltas.Count == 0 ? "  Без изменений" : string.Join("\n", deltas);
	}

	private static string CompareWeapon(WeaponData @new, WeaponData old)
	{
		if (old == null) return "  🆕 Слот пуст — будет надето сразу";
		if (old.Id == @new.Id) return "  = То же оружие";
		var deltas = new List<string>();
		AddDelta(deltas,      "ФизАтк",   @new.PhysAtk    - old.PhysAtk);
		AddDelta(deltas,      "МагАтк",   @new.MagicAtk   - old.MagicAtk);
		AddDeltaFloat(deltas, "ФизМульт", @new.PhysMult   - old.PhysMult);
		AddDeltaFloat(deltas, "МагМульт", @new.MagicMult  - old.MagicMult);
		AddDelta(deltas,      "Карты",    @new.ExtraDraw  - old.ExtraDraw);
		// Меньше кулдаун = лучше → инвертируем знак, чтобы "+ это улучшение".
		int dCrit = old.CritEveryNAttacks - @new.CritEveryNAttacks;
		if (dCrit != 0)
		{
			string sign = dCrit > 0 ? "+" : "";
			deltas.Add($"  {sign}{dCrit} к скорости крита (− кулдаун)");
		}
		return deltas.Count == 0 ? "  Без изменений" : string.Join("\n", deltas);
	}

	private static void AddDelta(List<string> list, string label, int diff)
	{
		if (diff == 0) return;
		string sign = diff > 0 ? "+" : "";
		list.Add($"  {sign}{diff} {label}");
	}

	private static void AddDeltaFloat(List<string> list, string label, float diff)
	{
		if (System.Math.Abs(diff) < 0.01f) return;
		string sign = diff > 0 ? "+" : "";
		list.Add($"  {sign}{diff:F1} {label}");
	}

	// =====================================================================
	// Описание предмета (имя/детали/иконка) для UI
	// =====================================================================
	private static (string name, string detail, string icon) DescribeItem(string id)
	{
		var w = ItemsDB.GetWeapon(id);
		if (w != null)
		{
			var detail = $"Физ ×{w.PhysMult:F1}, Маг ×{w.MagicMult:F1}";
			if (w.PhysAtk > 0) detail += $", +{w.PhysAtk} ФизАтк";
			if (w.MagicAtk > 0) detail += $", +{w.MagicAtk} МагАтк";
			if (w.ExtraDraw > 0) detail += $", +{w.ExtraDraw} карта";
			detail += $", крит каждые {w.CritEveryNAttacks}";
			return (w.Name, detail, "⚔");
		}
		var a = ItemsDB.GetArmor(id);
		if (a != null) return (a.Name, ItemsDB.DescribeArmor(a), SlotIcon(a.Slot));

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

using System.Collections.Generic;
using GuildOfGreed.Shared.Domain;

namespace GuildOfGreed.Shared.Data;

// Городская лавка: статичный ассортимент на продажу + единая логика цен
// для покупки/продажи. Все цены — в медяках (см. Currency).
//
// MVP: магазин торгует базовыми зельями. Шоп-генерация оружия/брони с роллом
// аффиксов — следующий инкремент. Пока в Stock только потёки.
//
// Покупка: BuyPrice(id) — фиксированная цена базы.
// Продажа: SellPriceForStack(stack) — учитывает редкость instance-предметов;
//          возвращает 40% от полной "базовой" цены того же тира.
public static class ShopDB
{
	// Множитель выкупа: лавка покупает у игрока за 40% полной цены.
	public const float SellRatio = 0.4f;

	// Стартовый ассортимент — разделён по категориям для вкладок в лавке.
	// Запас бесконечный: ShopOverlay списком просто отображает базы.
	public static readonly List<string> PotionsStock = new()
	{
		"potion_hp_small",
		"potion_mp_small",
		"potion_hp_medium",
		"potion_mp_medium",
		"potion_strength",
		"potion_focus",
		"potion_full",
	};

	// Базовое оружие E-grade Low — всегда в продаже, по одному инстансу за клик.
	// Куплено = клон базы без аффиксов (см. GameData.BuyOne).
	public static readonly List<string> WeaponsStock = new()
	{
		"dagger_low",
		"sword_1h_low",
		"sword_2h_low",
		"staff_low",
	};

	[System.Obsolete("Use PotionsStock / WeaponsStock")]
	public static readonly List<string> Stock = PotionsStock;

	// === Базовые таблицы цен ===========================================

	// Зелья: фиксированные цены, чтобы экономика читалась с одного взгляда.
	private static readonly Dictionary<string, long> PotionPrices = new()
	{
		["potion_hp_small"]  = 15,
		["potion_mp_small"]  = 15,
		["potion_hp_medium"] = 60,
		["potion_mp_medium"] = 60,
		["potion_strength"]  = 80,
		["potion_focus"]     = 80,
		["potion_full"]      = 250,
	};

	// Оружие E-grade Low: явные цены чтобы экономика начала читаться при
	// первом тесте. Без аффиксов; дороже зелий, но достижимо за пару забегов.
	private static readonly Dictionary<string, long> WeaponPrices = new()
	{
		["dagger_low"]    = 40,
		["sword_1h_low"]  = 50,
		["sword_2h_low"]  = 80,
		["staff_low"]     = 90,
	};

	// Оружие/броня/щит: цена по редкости (instance — катался ItemGenerator).
	// Числа подобраны так, чтобы Legendary стоила < 1 золота (потолок раннего game).
	private static long PriceByRarity(ItemRarity r) => r switch
	{
		ItemRarity.Common    => 30,
		ItemRarity.Uncommon  => 90,
		ItemRarity.Rare      => 300,
		ItemRarity.Heroic    => 900,
		ItemRarity.Epic      => 2500,
		ItemRarity.Legendary => 8000,
		_                    => 30,
	};

	// Полная цена instance-предмета с учётом редкости И грейда/тира.
	// Без grade/tier-множителя Legendary E-low и Legendary S-top стоили одинаково,
	// что плющит экономику топ-контента. Используем TierProgression.Mult — ту же
	// кривую, по которой растут статы предмета: цена движется в ногу с силой.
	private static long PriceForItem(ItemRarity rarity, string grade, string tier)
	{
		float m = TierProgression.Mult(grade ?? "E", tier ?? "low");
		return (long)System.Math.Round(PriceByRarity(rarity) * m);
	}

	// === Покупка ========================================================

	// Цена покупки по baseId. null для предметов которые лавка не продаёт.
	public static long? BuyPrice(string itemId)
	{
		if (PotionPrices.TryGetValue(itemId, out var p)) return p;
		if (WeaponPrices.TryGetValue(itemId, out var w)) return w;
		return null;
	}

	// === Продажа ========================================================

	// Цена продажи стака из инвентаря. Учитывает:
	//   - стакаемые baseId — по PotionPrices (если есть) × SellRatio × count;
	//   - instance Weapon/Armor/Shield — по rarity × SellRatio;
	//   - неизвестные base-id — 1м (чтобы лавка никогда не давала 0).
	public static long SellPriceForStack(InventoryStack stack)
	{
		if (stack == null) return 0;

		if (stack.WeaponInstance != null)
		{
			var w = stack.WeaponInstance;
			return Floor(PriceForItem(w.Rarity, w.Grade, w.Tier) * SellRatio);
		}
		if (stack.ArmorInstance != null)
		{
			var a = stack.ArmorInstance;
			return Floor(PriceForItem(a.Rarity, a.Grade, a.Tier) * SellRatio);
		}
		if (stack.ShieldInstance != null)
		{
			var sh = stack.ShieldInstance;
			return Floor(PriceForItem(sh.Rarity, sh.Grade, sh.Tier) * SellRatio);
		}

		// Стакаемое: считаем за весь стак.
		long unit;
		if (PotionPrices.TryGetValue(stack.ItemId, out var p)) unit = p;
		else
		{
			// База без аффиксов (например, "sword_1h_low" в сейв-блобе) — оцениваем
			// по rarity самой базы из ItemsDB.
			var w = ItemsDB.GetWeapon(stack.ItemId);
			if (w != null) unit = PriceForItem(w.Rarity, w.Grade, w.Tier);
			else
			{
				var a = ItemsDB.GetArmor(stack.ItemId);
				if (a != null) unit = PriceForItem(a.Rarity, a.Grade, a.Tier);
				else
				{
					var s = ShieldsDB.Get(stack.ItemId);
					unit = s != null ? PriceForItem(s.Rarity, s.Grade, s.Tier) : 1;
				}
			}
		}
		return System.Math.Max(1, Floor(unit * SellRatio)) * stack.Count;
	}

	private static long Floor(float v) => (long)System.Math.Floor(v);
}

using System;
using GuildOfGreed.Shared.Data;
using GuildOfGreed.Shared.Domain;

namespace GuildOfGreed.Shared.Commands;

// Pure-функции мутаций персонажа: купить/продать/надеть/снять/положить в стэш/
// распылить/улучшить и т.п. Эти же методы вызываются и клиентом (для prediction'а
// в будущем), и сервером (как источник правды). Никаких побочных эффектов кроме
// мутации переданного CharacterData; никакой сети, никаких глобалов.
//
// Анти-чит: всё что меняет инвентарь/деньги/экипировку игрока ОБЯЗАНО проходить
// через эти команды на сервере. Сервер не доверяет полному JSON CharacterData
// от клиента — клиент шлёт только параметры команды, сервер сам пересчитывает.
//
// Контракт ошибок: коды стабильны для UI-перевода (см. CharacterCommandError).
public static partial class CharacterCommands
{
	// Слоты экипировки для UnequipSlot. Расширенный набор по сравнению с
	// ArmorSlot — включает Weapon/Offhand/Shield (которые не броня).
	public enum EquipSlotKind
	{
		Weapon  = 0,
		Offhand = 1,
		Shield  = 2,
		Chest   = 3,
		Helmet  = 4,
		Gloves  = 5,
		Boots   = 6,
		Amulet  = 7,
		Ring1   = 8,
		Ring2   = 9,
	}

	public readonly struct Result
	{
		public readonly bool Ok;
		public readonly string Error;     // см. CharacterCommandError
		public readonly long Value;       // dismantle yield / sold price; 0 если неприменимо

		private Result(bool ok, string err, long v) { Ok = ok; Error = err; Value = v; }
		public static Result Success(long value = 0) => new(true, null, value);
		public static Result Fail(string err) => new(false, err, 0);
	}

	// ===================================================================
	// Buy / Sell (лавка)
	// ===================================================================

	public static Result BuyItem(CharacterData ch, string itemId)
	{
		if (ch == null) return Result.Fail(CharacterCommandError.NoCharacter);
		var price = ShopDB.BuyPrice(itemId);
		if (price == null) return Result.Fail(CharacterCommandError.NotForSale);
		if (ch.Inventory.Money < price.Value) return Result.Fail(CharacterCommandError.NoMoney);

		// Оружие/броня/щит — кладём как instance (clone базы) чтобы потом можно
		// было надеть и навесить аффиксы. Зелья — стак по itemId.
		var weaponBase = ItemsDB.GetWeapon(itemId);
		if (weaponBase != null)
		{
			if (ch.Inventory.IsFull) return Result.Fail(CharacterCommandError.NoSpace);
			ch.Inventory.TryAddInstance(weaponBase.Clone());
			ch.Inventory.Money -= price.Value;
			return Result.Success();
		}
		var armorBase = ItemsDB.GetArmor(itemId);
		if (armorBase != null)
		{
			if (ch.Inventory.IsFull) return Result.Fail(CharacterCommandError.NoSpace);
			ch.Inventory.TryAddInstance(armorBase.Clone());
			ch.Inventory.Money -= price.Value;
			return Result.Success();
		}
		var shieldBase = ShieldsDB.Get(itemId);
		if (shieldBase != null)
		{
			if (ch.Inventory.IsFull) return Result.Fail(CharacterCommandError.NoSpace);
			ch.Inventory.TryAddInstance(shieldBase.Clone());
			ch.Inventory.Money -= price.Value;
			return Result.Success();
		}

		int maxStack = PotionsDB.Get(itemId) != null ? 9 : 1;
		long moneyBefore = ch.Inventory.Money;
		if (!ch.Inventory.TryAdd(itemId, 1, maxStack))
			return Result.Fail(CharacterCommandError.NoSpace);
		ch.Inventory.Money = moneyBefore - price.Value;
		return Result.Success();
	}

	public static Result SellSlot(CharacterData ch, int slotIndex)
	{
		if (ch == null) return Result.Fail(CharacterCommandError.NoCharacter);
		if (slotIndex < 0 || slotIndex >= ch.Inventory.Slots.Count)
			return Result.Fail(CharacterCommandError.BadSlot);
		var stack = ch.Inventory.Slots[slotIndex];
		long price = ShopDB.SellPriceForStack(stack);
		if (price <= 0) return Result.Fail(CharacterCommandError.NotSellable);
		ch.Inventory.RemoveAt(slotIndex);
		ch.Inventory.Money += price;
		return Result.Success(price);
	}

	// ===================================================================
	// Equip / Unequip
	// ===================================================================

	public static Result EquipFromInventory(CharacterData ch, int slotIndex)
	{
		if (ch == null) return Result.Fail(CharacterCommandError.NoCharacter);
		var slots = ch.Inventory.Slots;
		if (slotIndex < 0 || slotIndex >= slots.Count)
			return Result.Fail(CharacterCommandError.BadSlot);
		var st = slots[slotIndex];

		// Резолвим объект из слота: instance ИЛИ клон базы для baseId-предметов.
		WeaponData asWeapon = st.WeaponInstance ?? ItemsDB.GetWeapon(st.ItemId)?.Clone();
		ArmorData  asArmor  = st.ArmorInstance  ?? ItemsDB.GetArmor(st.ItemId)?.Clone();
		ShieldData asShield = st.ShieldInstance ?? ShieldsDB.Get(st.ItemId)?.Clone();
		if (asWeapon == null && asArmor == null && asShield == null)
			return Result.Fail(CharacterCommandError.NotEquippable);

		if (asWeapon != null) return EquipWeaponInternal(ch, asWeapon, slotIndex);
		if (asShield != null) return EquipShieldInternal(ch, asShield, slotIndex);

		// Броня (включая бижутерию). Для Ring1: если первое кольцо уже занято,
		// а второе пусто — надеваем во второе.
		ArmorSlot target = asArmor.Slot;
		if (target == ArmorSlot.Ring1 && ch.Ring1 != null && ch.Ring2 == null)
			target = ArmorSlot.Ring2;

		TakeOneFromSlot(ch, slotIndex);
		var old = ch.GetArmorSlot(target);
		if (old != null) ch.Inventory.TryAddInstance(old);
		ch.SetArmorSlot(target, asArmor);
		return Result.Success();
	}

	private static Result EquipWeaponInternal(CharacterData ch, WeaponData w, int slotIndex)
	{
		TakeOneFromSlot(ch, slotIndex);

		if (w.IsTwoHanded)
		{
			if (ch.Weapon != null)  ch.Inventory.TryAddInstance(ch.Weapon);
			if (ch.Offhand != null) ch.Inventory.TryAddInstance(ch.Offhand);
			if (ch.Shield != null)  ch.Inventory.TryAddInstance(ch.Shield);
			ch.Weapon = w;
			ch.Offhand = null;
			ch.Shield = null;
			return Result.Success();
		}

		bool mainSlotIs2H = ch.Weapon != null && ch.Weapon.IsTwoHanded;
		if (ch.Weapon == null || mainSlotIs2H)
		{
			if (ch.Weapon != null) ch.Inventory.TryAddInstance(ch.Weapon);
			ch.Weapon = w;
			return Result.Success();
		}

		if (ch.Offhand == null && ch.Shield == null)
		{
			ch.Offhand = w;
			return Result.Success();
		}

		ch.Inventory.TryAddInstance(ch.Weapon);
		ch.Weapon = w;
		return Result.Success();
	}

	private static Result EquipShieldInternal(CharacterData ch, ShieldData s, int slotIndex)
	{
		TakeOneFromSlot(ch, slotIndex);

		if (ch.Weapon != null && ch.Weapon.IsTwoHanded)
		{
			ch.Inventory.TryAddInstance(ch.Weapon);
			ch.Weapon = null;
		}
		if (ch.Offhand != null)
		{
			ch.Inventory.TryAddInstance(ch.Offhand);
			ch.Offhand = null;
		}
		if (ch.Shield != null)
			ch.Inventory.TryAddInstance(ch.Shield);
		ch.Shield = s;
		return Result.Success();
	}

	public static Result UnequipSlot(CharacterData ch, EquipSlotKind slot)
	{
		if (ch == null) return Result.Fail(CharacterCommandError.NoCharacter);
		if (ch.Inventory.IsFull) return Result.Fail(CharacterCommandError.NoSpace);

		switch (slot)
		{
			case EquipSlotKind.Weapon:
				if (ch.Weapon == null) return Result.Fail(CharacterCommandError.SlotEmpty);
				ch.Inventory.TryAddInstance(ch.Weapon);
				ch.Weapon = null;
				return Result.Success();
			case EquipSlotKind.Offhand:
				if (ch.Offhand == null) return Result.Fail(CharacterCommandError.SlotEmpty);
				ch.Inventory.TryAddInstance(ch.Offhand);
				ch.Offhand = null;
				return Result.Success();
			case EquipSlotKind.Shield:
				if (ch.Shield == null) return Result.Fail(CharacterCommandError.SlotEmpty);
				ch.Inventory.TryAddInstance(ch.Shield);
				ch.Shield = null;
				return Result.Success();
			default:
				var armorSlot = ToArmorSlot(slot);
				if (armorSlot == null) return Result.Fail(CharacterCommandError.BadSlot);
				var item = ch.GetArmorSlot(armorSlot.Value);
				if (item == null) return Result.Fail(CharacterCommandError.SlotEmpty);
				ch.Inventory.TryAddInstance(item);
				ch.SetArmorSlot(armorSlot.Value, null);
				return Result.Success();
		}
	}

	private static ArmorSlot? ToArmorSlot(EquipSlotKind slot) => slot switch
	{
		EquipSlotKind.Chest  => ArmorSlot.Chest,
		EquipSlotKind.Helmet => ArmorSlot.Helmet,
		EquipSlotKind.Gloves => ArmorSlot.Gloves,
		EquipSlotKind.Boots  => ArmorSlot.Boots,
		EquipSlotKind.Amulet => ArmorSlot.Amulet,
		EquipSlotKind.Ring1  => ArmorSlot.Ring1,
		EquipSlotKind.Ring2  => ArmorSlot.Ring2,
		_ => null,
	};

	// ===================================================================
	// Use potion (вне боя)
	// ===================================================================

	public static Result UsePotion(CharacterData ch, string itemId)
	{
		if (ch == null) return Result.Fail(CharacterCommandError.NoCharacter);
		var potion = PotionsDB.Get(itemId);
		if (potion == null) return Result.Fail(CharacterCommandError.NoPotion);
		if (!ch.Inventory.Has(itemId)) return Result.Fail(CharacterCommandError.NoPotion);

		ch.Inventory.Remove(itemId, 1);
		if (potion.HpRestore > 0)
			ch.CurrentHp = Math.Min(ch.MaxHp(), ch.CurrentHp + potion.HpRestore);
		if (potion.MpRestore > 0)
			ch.CurrentMp = Math.Min(ch.MaxMp(), ch.CurrentMp + potion.MpRestore);
		// Buff-эффекты per-battle и не персистятся — в town бессмысленно их
		// навешивать. Сбросятся в PrepareForBattle при следующем бое.
		return Result.Success();
	}

	// ===================================================================
	// Stash deposit / withdraw
	// ===================================================================

	public static Result DepositToStash(CharacterData ch, int invSlotIndex)
	{
		if (ch == null) return Result.Fail(CharacterCommandError.NoCharacter);
		var slots = ch.Inventory.Slots;
		if (invSlotIndex < 0 || invSlotIndex >= slots.Count)
			return Result.Fail(CharacterCommandError.BadSlot);
		if (ch.Stash.IsFull) return Result.Fail(CharacterCommandError.StashFull);
		var stack = slots[invSlotIndex];
		ch.Inventory.RemoveAt(invSlotIndex);
		ch.Stash.TryAddStack(stack);
		return Result.Success();
	}

	public static Result WithdrawFromStash(CharacterData ch, int stashSlotIndex)
	{
		if (ch == null) return Result.Fail(CharacterCommandError.NoCharacter);
		var slots = ch.Stash.Slots;
		if (stashSlotIndex < 0 || stashSlotIndex >= slots.Count)
			return Result.Fail(CharacterCommandError.BadSlot);
		if (ch.Inventory.IsFull) return Result.Fail(CharacterCommandError.NoSpace);
		var stack = slots[stashSlotIndex];
		ch.Stash.RemoveAt(stashSlotIndex);
		ch.Inventory.TryAddStack(stack);
		return Result.Success();
	}

	// ===================================================================
	// Forge
	// ===================================================================

	public static Result ForgeDismantle(CharacterData ch, int slotIndex)
	{
		if (ch == null) return Result.Fail(CharacterCommandError.NoCharacter);
		var slots = ch.Inventory.Slots;
		if (slotIndex < 0 || slotIndex >= slots.Count)
			return Result.Fail(CharacterCommandError.BadSlot);
		var stack = slots[slotIndex];

		string grade, rank;
		ItemRarity rarity;
		if (stack.WeaponInstance != null)
		{ grade = stack.WeaponInstance.Grade; rank = stack.WeaponInstance.Tier; rarity = stack.WeaponInstance.Rarity; }
		else if (stack.ArmorInstance != null)
		{ grade = stack.ArmorInstance.Grade; rank = stack.ArmorInstance.Tier; rarity = stack.ArmorInstance.Rarity; }
		else if (stack.ShieldInstance != null)
		{ grade = stack.ShieldInstance.Grade; rank = stack.ShieldInstance.Tier; rarity = stack.ShieldInstance.Rarity; }
		else
			return Result.Fail(CharacterCommandError.NotForgeable);

		long yield = ForgeDB.DismantleEssence(grade, rank, rarity);
		ch.Inventory.RemoveAt(slotIndex);
		ch.Inventory.Essence += yield;
		return Result.Success(yield);
	}

	public static Result ForgeUpgrade(CharacterData ch, int slotIndex, RandomSource rng)
	{
		if (ch == null) return Result.Fail(CharacterCommandError.NoCharacter);
		var slots = ch.Inventory.Slots;
		if (slotIndex < 0 || slotIndex >= slots.Count)
			return Result.Fail(CharacterCommandError.BadSlot);
		var stack = slots[slotIndex];

		if (stack.WeaponInstance != null)
		{
			var w = stack.WeaponInstance;
			if (!ForgeDB.CanUpgrade(w.Grade, w.Rarity)) return Result.Fail(CharacterCommandError.CantUpgrade);
			long cost = ForgeDB.UpgradeCost(w.Grade, w.Tier, w.Rarity);
			if (ch.Inventory.Essence < cost) return Result.Fail(CharacterCommandError.NoEssence);
			var newRarity = ForgeDB.NextRarity(w.Rarity);
			var fresh = ItemGenerator.RollWeapon(w.Id, rng, newRarity);
			if (fresh == null) return Result.Fail(CharacterCommandError.NotForgeable);
			stack.WeaponInstance = fresh;
			ch.Inventory.Essence -= cost;
			return Result.Success();
		}
		if (stack.ArmorInstance != null)
		{
			var a = stack.ArmorInstance;
			if (!ForgeDB.CanUpgrade(a.Grade, a.Rarity)) return Result.Fail(CharacterCommandError.CantUpgrade);
			long cost = ForgeDB.UpgradeCost(a.Grade, a.Tier, a.Rarity);
			if (ch.Inventory.Essence < cost) return Result.Fail(CharacterCommandError.NoEssence);
			var newRarity = ForgeDB.NextRarity(a.Rarity);
			var fresh = ItemGenerator.RollArmor(a.Id, rng, newRarity);
			if (fresh == null) return Result.Fail(CharacterCommandError.NotForgeable);
			stack.ArmorInstance = fresh;
			ch.Inventory.Essence -= cost;
			return Result.Success();
		}
		if (stack.ShieldInstance != null)
		{
			var s = stack.ShieldInstance;
			if (!ForgeDB.CanUpgrade(s.Grade, s.Rarity)) return Result.Fail(CharacterCommandError.CantUpgrade);
			long cost = ForgeDB.UpgradeCost(s.Grade, s.Tier, s.Rarity);
			if (ch.Inventory.Essence < cost) return Result.Fail(CharacterCommandError.NoEssence);
			s.Rarity = ForgeDB.NextRarity(s.Rarity);
			ch.Inventory.Essence -= cost;
			return Result.Success();
		}
		return Result.Fail(CharacterCommandError.NotForgeable);
	}

	public static Result ForgeReroll(CharacterData ch, int slotIndex, RandomSource rng)
	{
		if (ch == null) return Result.Fail(CharacterCommandError.NoCharacter);
		var slots = ch.Inventory.Slots;
		if (slotIndex < 0 || slotIndex >= slots.Count)
			return Result.Fail(CharacterCommandError.BadSlot);
		var stack = slots[slotIndex];

		if (stack.WeaponInstance != null)
		{
			var w = stack.WeaponInstance;
			long cost = ForgeDB.RerollCost(w.Grade, w.Tier);
			if (ch.Inventory.Essence < cost) return Result.Fail(CharacterCommandError.NoEssence);
			var fresh = ItemGenerator.RollWeapon(w.Id, rng, w.Rarity);
			if (fresh == null) return Result.Fail(CharacterCommandError.NotForgeable);
			stack.WeaponInstance = fresh;
			ch.Inventory.Essence -= cost;
			return Result.Success();
		}
		if (stack.ArmorInstance != null)
		{
			var a = stack.ArmorInstance;
			long cost = ForgeDB.RerollCost(a.Grade, a.Tier);
			if (ch.Inventory.Essence < cost) return Result.Fail(CharacterCommandError.NoEssence);
			var fresh = ItemGenerator.RollArmor(a.Id, rng, a.Rarity);
			if (fresh == null) return Result.Fail(CharacterCommandError.NotForgeable);
			stack.ArmorInstance = fresh;
			ch.Inventory.Essence -= cost;
			return Result.Success();
		}
		return Result.Fail(CharacterCommandError.NotForgeable);
	}

	// Crafting — отдельный файл CharacterCommands.Craft.cs.

	// ===================================================================
	// Stat point
	// ===================================================================

	public static Result SpendStatPoint(CharacterData ch, string stat)
	{
		if (ch == null) return Result.Fail(CharacterCommandError.NoCharacter);
		if (!ch.TrySpendStatPoint(stat))
			return Result.Fail(CharacterCommandError.NoStatPoints);
		return Result.Success();
	}

	// ===================================================================
	// Internal helpers
	// ===================================================================

	// Удалить 1 единицу из слота инвентаря. Для instance — удалить слот
	// целиком; для стака — декремент, и если стак опустел — удалить слот.
	private static void TakeOneFromSlot(CharacterData ch, int slotIndex)
	{
		var slots = ch.Inventory.Slots;
		if (slotIndex < 0 || slotIndex >= slots.Count) return;
		var s = slots[slotIndex];
		if (s.WeaponInstance != null || s.ArmorInstance != null || s.ShieldInstance != null)
		{
			ch.Inventory.RemoveAt(slotIndex);
			return;
		}
		s.Count--;
		if (s.Count <= 0) ch.Inventory.RemoveAt(slotIndex);
	}
}

// Стабильные коды ошибок. Клиент переводит на локализованные сообщения.
public static class CharacterCommandError
{
	public const string NoCharacter     = "no_character";
	public const string NotForSale      = "not_for_sale";
	public const string NoMoney         = "no_money";
	public const string NoSpace         = "no_space";
	public const string BadSlot         = "bad_slot";
	public const string SlotEmpty       = "slot_empty";
	public const string NotEquippable   = "not_equippable";
	public const string NotSellable     = "not_sellable";
	public const string NoPotion        = "no_potion";
	public const string StashFull       = "stash_full";
	public const string NotForgeable    = "not_forgeable";
	public const string CantUpgrade     = "cant_upgrade";
	public const string NoEssence       = "no_essence";
	public const string NoStatPoints    = "no_stat_points";
	public const string LockedInRun     = "locked_in_run";
	public const string LockedInBattle  = "locked_in_battle";
	public const string NoRecipe        = "no_recipe";
	public const string LowSkill        = "low_skill";
	public const string NoResources     = "no_resources";
}

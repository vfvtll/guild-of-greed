using System.Collections.Generic;

namespace GuildOfGreed.Shared.Domain;

// Инвентарь персонажа с лимитом по числу слотов.
//
// Два режима хранения предметов:
//   1. Stackable by ID — зелья и базы без ролла. Слоты: ItemId + Count.
//   2. Instance — аффиксированные оружие/броня. Слот хранит WeaponInstance
//      или ArmorInstance с конкретными rolled-Affixes. Каждый instance —
//      отдельный неcтакаемый слот, Count всегда = 1.
//
// API:
//   inv.TryAdd("potion_hp_small", 2, maxStack: 5)   — добавить со стаком
//   inv.TryAdd("sword_1h_low", 1, maxStack: 1)      — нестакаемая база
//   inv.TryAddInstance(weaponInstance)              — конкретный экземпляр оружия
//   inv.TryAddInstance(armorInstance)               — конкретный экземпляр брони
//   inv.RemoveAt(slotIndex)                         — выкинуть слот целиком
//   inv.Remove("potion_hp_small", 1)                — забрать со стека по Id
//
// Поиск по ItemId для CountOf/Has учитывает только base-Id слоты со
// стакаемым контентом; инстансы (instance-based слоты) считаются отдельно
// через FindInstance/CountOf — instance-предметы НЕ матчат базовый Id.
public class Inventory
{
	public const int Capacity = 20;

	public List<InventoryStack> Slots = new();

	// Валюта одним числом — в медяках. См. Currency для деления на золото/серебро/медь.
	// long потому что у нас 1 золото = 10 000 медяков, а на дольной игре скоро
	// упрёмся в int.MaxValue. Старые сейвы без поля → 0 (JSON default).
	public long Money = 0;

	// Магическая эссенция — внутренняя валюта кузницы. Получается распылом
	// предметов, тратится на улучшение редкости и реролл аффиксов. См. ForgeDB.
	// Старые сейвы → 0 (default).
	public long Essence = 0;

	public bool IsFull => Slots.Count >= Capacity;
	public int FreeSlots => Capacity - Slots.Count;

	// Добавить count единиц стакаемого предмета. Только для baseId-предметов
	// (зелья, базы без ролла). Для аффиксированных — TryAddInstance.
	public bool TryAdd(string itemId, int count, int maxStack)
	{
		if (count <= 0 || string.IsNullOrEmpty(itemId) || maxStack <= 0) return false;

		// Сначала добиваем существующие стаки (если стакаемый).
		if (maxStack > 1)
		{
			foreach (var s in Slots)
			{
				if (s.ItemId != itemId) continue;
				if (s.WeaponInstance != null || s.ArmorInstance != null || s.ShieldInstance != null) continue;
				int free = maxStack - s.Count;
				if (free <= 0) continue;
				int put = count < free ? count : free;
				s.Count += put;
				count -= put;
				if (count == 0) return true;
			}
		}

		// Заполняем новые слоты (пока место есть).
		while (count > 0 && Slots.Count < Capacity)
		{
			int put = count < maxStack ? count : maxStack;
			Slots.Add(new InventoryStack { ItemId = itemId, Count = put });
			count -= put;
		}

		return count == 0;
	}

	// Положить конкретный экземпляр оружия (с уже накатанными аффиксами).
	// Возвращает true если был свободный слот.
	public bool TryAddInstance(WeaponData weapon)
	{
		if (weapon == null || IsFull) return false;
		Slots.Add(new InventoryStack
		{
			ItemId = weapon.Id,
			Count = 1,
			WeaponInstance = weapon,
		});
		return true;
	}

	public bool TryAddInstance(ArmorData armor)
	{
		if (armor == null || IsFull) return false;
		Slots.Add(new InventoryStack
		{
			ItemId = armor.Id,
			Count = 1,
			ArmorInstance = armor,
		});
		return true;
	}

	public bool TryAddInstance(ShieldData shield)
	{
		if (shield == null || IsFull) return false;
		Slots.Add(new InventoryStack
		{
			ItemId = shield.Id,
			Count = 1,
			ShieldInstance = shield,
		});
		return true;
	}

	// Положить готовый стак "как есть" — для перемещений из Stash в Inventory:
	// сохраняет ссылку на тот же InventoryStack (важно для instance-предметов).
	public bool TryAddStack(InventoryStack stack)
	{
		if (stack == null || IsFull) return false;
		Slots.Add(stack);
		return true;
	}

	// Удалить count единиц стакаемого предмета по ItemId. Не трогает instance-слоты
	// (потому что они уникальны и Id для них — лишь lookup-метка, не ключ удаления).
	public int Remove(string itemId, int count = 1)
	{
		int removed = 0;
		for (int i = Slots.Count - 1; i >= 0 && count > 0; i--)
		{
			if (Slots[i].ItemId != itemId) continue;
			if (Slots[i].WeaponInstance != null || Slots[i].ArmorInstance != null) continue;
			int take = Slots[i].Count < count ? Slots[i].Count : count;
			Slots[i].Count -= take;
			count -= take;
			removed += take;
			if (Slots[i].Count <= 0) Slots.RemoveAt(i);
		}
		return removed;
	}

	// Удалить слот целиком по индексу. Для instance-слотов (надевание/выкидывание).
	public bool RemoveAt(int slotIndex)
	{
		if (slotIndex < 0 || slotIndex >= Slots.Count) return false;
		Slots.RemoveAt(slotIndex);
		return true;
	}

	// Считает только стакаемые экземпляры (instance-слоты не учитываются — они
	// уникальны и не "складываются" с базой).
	public int CountOf(string itemId)
	{
		int total = 0;
		foreach (var s in Slots)
		{
			if (s.ItemId != itemId) continue;
			if (s.WeaponInstance != null || s.ArmorInstance != null || s.ShieldInstance != null) continue;
			total += s.Count;
		}
		return total;
	}

	public bool Has(string itemId) => CountOf(itemId) > 0;
}

public class InventoryStack
{
	public string ItemId;
	public int Count;
	// Payload для instance-предметов. Не более одного из *Instance != null.
	// При наличии instance Count всегда = 1 (нестакаемо).
	public WeaponData WeaponInstance;     // И6.2
	public ArmorData  ArmorInstance;      // И6.2
	public ShieldData ShieldInstance;     // И6.4
}

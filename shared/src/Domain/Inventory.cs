using System.Collections.Generic;

namespace GuildOfGreed.Shared.Domain;

// Инвентарь персонажа с лимитом по числу слотов.
// Каждый слот хранит ID предмета (оружие/броня/зелье) и количество.
// Стакаемость определяется не Inventory'ем (он в Domain и не должен знать
// про PotionsDB) — caller передаёт maxStack явно. Обёртка в GameData
// (AddItem) сама смотрит тип предмета и подставляет нужный maxStack.
//
// API:
//   inv.TryAdd("potion_hp_small", 2, maxStack: 5)   — добавить со стаком
//   inv.TryAdd("sword_1h_low", 1, maxStack: 1)      — нестакаемый предмет
//   inv.Remove("potion_hp_small", 1)                — забрать
//   inv.CountOf("potion_hp_small")                  — сколько в инвентаре
public class Inventory
{
	public const int Capacity = 20;

	public List<InventoryStack> Slots = new();

	public bool IsFull => Slots.Count >= Capacity;
	public int FreeSlots => Capacity - Slots.Count;

	// Добавить count единиц предмета. Caller указывает maxStack:
	// 1 для оружия/брони, 5 (или больше) для зелий.
	// Возвращает true если ВСЁ количество удалось положить.
	public bool TryAdd(string itemId, int count, int maxStack)
	{
		if (count <= 0 || string.IsNullOrEmpty(itemId) || maxStack <= 0) return false;

		// Сначала добиваем существующие стаки (если стакаемый).
		if (maxStack > 1)
		{
			foreach (var s in Slots)
			{
				if (s.ItemId != itemId) continue;
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

	// Удалить count единиц. Возвращает сколько удалось удалить.
	public int Remove(string itemId, int count = 1)
	{
		int removed = 0;
		for (int i = Slots.Count - 1; i >= 0 && count > 0; i--)
		{
			if (Slots[i].ItemId != itemId) continue;
			int take = Slots[i].Count < count ? Slots[i].Count : count;
			Slots[i].Count -= take;
			count -= take;
			removed += take;
			if (Slots[i].Count <= 0) Slots.RemoveAt(i);
		}
		return removed;
	}

	public int CountOf(string itemId)
	{
		int total = 0;
		foreach (var s in Slots)
			if (s.ItemId == itemId) total += s.Count;
		return total;
	}

	public bool Has(string itemId) => CountOf(itemId) > 0;
}

public class InventoryStack
{
	public string ItemId;
	public int Count;
}

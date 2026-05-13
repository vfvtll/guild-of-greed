using System.Collections.Generic;

namespace GuildOfGreed.Shared.Domain;

// Городское хранилище. По форме почти как Inventory, но:
//   - больше слотов (50 вместо 20);
//   - нет Money — кошель один на персонажа (на стороне Inventory).
//
// Слоты — те же InventoryStack (см. Inventory.cs), чтобы:
//   - JSON-сериализация была единой;
//   - перенос между Inventory и Stash сводился к перемещению ссылки на стак,
//     а не к копированию полей (важно для instance-предметов с аффиксами).
public class Stash
{
	public const int Capacity = 50;

	public List<InventoryStack> Slots = new();

	public bool IsFull => Slots.Count >= Capacity;
	public int FreeSlots => Capacity - Slots.Count;

	// Добавить count стакаемого baseId-предмета. Логика 1-в-1 с Inventory.TryAdd.
	public bool TryAdd(string itemId, int count, int maxStack)
	{
		if (count <= 0 || string.IsNullOrEmpty(itemId) || maxStack <= 0) return false;

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

		while (count > 0 && Slots.Count < Capacity)
		{
			int put = count < maxStack ? count : maxStack;
			Slots.Add(new InventoryStack { ItemId = itemId, Count = put });
			count -= put;
		}
		return count == 0;
	}

	public bool TryAddInstance(WeaponData weapon)
	{
		if (weapon == null || IsFull) return false;
		Slots.Add(new InventoryStack { ItemId = weapon.Id, Count = 1, WeaponInstance = weapon });
		return true;
	}

	public bool TryAddInstance(ArmorData armor)
	{
		if (armor == null || IsFull) return false;
		Slots.Add(new InventoryStack { ItemId = armor.Id, Count = 1, ArmorInstance = armor });
		return true;
	}

	public bool TryAddInstance(ShieldData shield)
	{
		if (shield == null || IsFull) return false;
		Slots.Add(new InventoryStack { ItemId = shield.Id, Count = 1, ShieldInstance = shield });
		return true;
	}

	// Добавить готовый стак "как есть" — для перемещения instance/стека из
	// инвентаря БЕЗ копирования (сохраняет ссылку на тот же InventoryStack).
	public bool TryAddStack(InventoryStack stack)
	{
		if (stack == null || IsFull) return false;
		Slots.Add(stack);
		return true;
	}

	public bool RemoveAt(int slotIndex)
	{
		if (slotIndex < 0 || slotIndex >= Slots.Count) return false;
		Slots.RemoveAt(slotIndex);
		return true;
	}
}

using Godot;
using System;
using System.Collections.Generic;

// Полноэкранный оверлей инвентаря. Открывается из Combat по кнопке "🎒 Инвентарь".
//
// Файлы:
//   InventoryOverlay.cs         — состояние, _Ready, Refresh, обработчики кликов
//   InventoryOverlay.UI.cs      — BuildUI и фабрики карточек
//   InventoryOverlay.Compare.cs — сравнение предметов и описание
//
// Слева — надетые предметы (5 слотов: оружие + 4 брони).
// Справа — содержимое инвентаря (20 ячеек).
// Сверху — сводка эффективных параметров.
//
// Клик по надетому → снять (если в инвентаре есть место).
// Клик по предмету в инвентаре:
//   - оружие/броня → надеть (свап с текущим в этом слоте)
//   - зелье → использовать
//
// Все изменения делаются через GameData (EquipFromInventory / UnequipSlot / UsePotion).
public partial class InventoryOverlay : Control
{
	[Signal]
	public delegate void ClosedEventHandler();

	// В режиме просмотра (во время боя) клики по слотам ничего не делают.
	// Combat выставляет ReadOnly = !_combatOver перед AddChild.
	public bool ReadOnly = false;

	private Label _capacityLabel;
	private Label _statusLabel;
	private Label _readOnlyHint;
	private VBoxContainer _equipmentList;
	private GridContainer _inventoryGrid;
	private Label _summaryAtk, _summaryDef, _summaryCrit;

	private const int GridColumns = 4;

	public override void _Ready()
	{
		SetAnchorsPreset(LayoutPreset.FullRect);
		MouseFilter = MouseFilterEnum.Stop;
		BuildUI();
		Refresh();
	}

	// =====================================================================
	// Refresh
	// =====================================================================

	private void Refresh()
	{
		var ch = GameData.Instance.Character;
		if (ch == null) return;

		_capacityLabel.Text = $"Содержимое ({ch.Inventory.Slots.Count}/{Inventory.Capacity})";
		RefreshSummary(ch);

		ClearChildren(_equipmentList);
		_equipmentList.AddChild(MakeSlotRow("⚔",  "Оружие",   ch.EquippedWeaponId, () => UnequipWeapon()));
		_equipmentList.AddChild(MakeSlotRow("👕", "Грудь",    ch.EquippedChestId,  () => UnequipArmor(ArmorSlot.Chest)));
		_equipmentList.AddChild(MakeSlotRow("⛑",  "Шлем",     ch.EquippedHelmetId, () => UnequipArmor(ArmorSlot.Helmet)));
		_equipmentList.AddChild(MakeSlotRow("🧤", "Перчатки", ch.EquippedGlovesId, () => UnequipArmor(ArmorSlot.Gloves)));
		_equipmentList.AddChild(MakeSlotRow("👢", "Сапоги",   ch.EquippedBootsId,  () => UnequipArmor(ArmorSlot.Boots)));
		_equipmentList.AddChild(MakeSlotRow("📿", "Амулет",   ch.EquippedAmuletId, () => UnequipArmor(ArmorSlot.Amulet)));
		_equipmentList.AddChild(MakeSlotRow("💍", "Кольцо 1", ch.EquippedRing1Id,  () => UnequipArmor(ArmorSlot.Ring1)));
		_equipmentList.AddChild(MakeSlotRow("💍", "Кольцо 2", ch.EquippedRing2Id,  () => UnequipArmor(ArmorSlot.Ring2)));

		ClearChildren(_inventoryGrid);
		int total = Inventory.Capacity;
		var slots = ch.Inventory.Slots;
		for (int i = 0; i < total; i++)
		{
			if (i < slots.Count)
			{
				var st = slots[i];
				_inventoryGrid.AddChild(MakeItemCard(st.ItemId, st.Count, () => UseInventorySlot(st.ItemId)));
			}
			else
			{
				_inventoryGrid.AddChild(MakeEmptyCard());
			}
		}
	}

	private void RefreshSummary(CharacterData ch)
	{
		int physAtk = ch.Str / 3 + ch.WeaponPhysAtk() + ch.PhysAtkBonus();
		int magAtk  = ch.Int / 3 + ch.WeaponMagAtk() + ch.MagicAtkBonus();
		int def = ch.PhysDef();
		int hp = ch.MaxHp();
		int mp = ch.MaxMp();
		int regen = ch.MpRegen();
		_summaryAtk.Text =
			$"⚔ ФизАтк {physAtk} × {ch.PhysMult():F1}    🔮 МагАтк {magAtk} × {ch.MagicMult():F1}";
		_summaryDef.Text =
			$"🛡 Защ {def}    ❤ ХП {hp}    💧 МП {mp} (+{regen}/ход)    ✋ Хэнд {ch.HandSize()}";
		string critText = ch.Weapon != null
			? $"🎯 Крит каждые {ch.EffectiveCritEveryN()} ат. × {ch.CritMultiplier():F2}"
			: "🎯 Без оружия — нет крита";
		_summaryCrit.Text = critText;
	}

	// =====================================================================
	// Действия игрока
	// =====================================================================

	private void UnequipWeapon()
	{
		if (BlockedByReadOnly()) return;
		if (!GameData.Instance.UnequipWeapon())
			SetStatus("Не получилось снять — инвентарь полон.", error: true);
		else SetStatus("Оружие снято.", error: false);
		Refresh();
	}

	private void UnequipArmor(ArmorSlot slot)
	{
		if (BlockedByReadOnly()) return;
		if (!GameData.Instance.UnequipSlot(slot))
			SetStatus("Не получилось снять — инвентарь полон.", error: true);
		else SetStatus("Снято в инвентарь.", error: false);
		Refresh();
	}

	private void UseInventorySlot(string itemId)
	{
		if (BlockedByReadOnly()) return;
		if (PotionsDB.Get(itemId) != null)
		{
			if (GameData.Instance.UsePotion(itemId))
				SetStatus("Зелье выпито.", error: false);
			else SetStatus("Не удалось применить зелье.", error: true);
		}
		else
		{
			if (GameData.Instance.EquipFromInventory(itemId))
				SetStatus("Надето.", error: false);
			else SetStatus("Не удалось надеть.", error: true);
		}
		Refresh();
	}

	// Если открыли инвентарь во время боя — все мутирующие действия запрещены.
	// Зелья пьются через отдельные кнопки в панели игрока.
	private bool BlockedByReadOnly()
	{
		if (!ReadOnly) return false;
		SetStatus("Во время боя нельзя менять экипировку или пить зелья отсюда.", error: true);
		return true;
	}

	private void SetStatus(string msg, bool error)
	{
		_statusLabel.Text = msg;
		_statusLabel.AddThemeColorOverride("font_color",
			error ? UIStyle.DangerRed : UIStyle.HealGreen);
	}

	private static void ClearChildren(Node parent)
	{
		foreach (Node c in parent.GetChildren())
		{
			parent.RemoveChild(c);
			c.QueueFree();
		}
	}
}

using Godot;
using System;
using System.Collections.Generic;
using GuildOfGreed.Shared.Commands;
using GuildOfGreed.Shared.Domain;
using GuildOfGreed.Shared.Data;

// InventoryOverlay — модальный диалог детализации предмета.
//
// Зачем нужен: на мобильных устройствах нет hover'а, и Godot-тултипы там
// не показываются. Раньше одиночный тап по предмету сразу его экипировал —
// игрок не успевал увидеть сравнение со своим текущим снаряжением. Теперь
// тап открывает этот диалог: полное описание, diff против надетого, и
// явные кнопки "Надеть" / "Снять" / "Использовать" / "Отмена".
//
// На десктопе hover-тултипы у карточек инвентаря и слотов экипировки тоже
// продолжают работать (BuildSlotRow / MakeItemCard сами их выставляют) —
// диалог здесь дополняет, а не заменяет их.
public partial class InventoryOverlay
{
	private Control _detailDialog;

	// =========================================================================
	// Базовый показ диалога
	// =========================================================================
	private void ShowItemDetail(string title, string rarityTag, string bodyText,
		ItemRarity rarity, List<(string label, Action action, bool primary)> actions)
	{
		HideItemDetail();

		// Подложка ловит тапы по фону и закрывает диалог.
		var backdrop = new ColorRect { Color = new Color(0, 0, 0, 0.6f) };
		backdrop.MouseFilter = MouseFilterEnum.Stop;
		backdrop.GuiInput += ev =>
		{
			if (ev is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left)
				HideItemDetail();
		};
		AddChild(backdrop);
		UIStyle.FillParent(backdrop);
		_detailDialog = backdrop;

		var center = new CenterContainer();
		UIStyle.FillParent(center);
		backdrop.AddChild(center);

		// 320 — комфортный минимум для портретной ориентации телефона (320..360
		// типовая ширина без margin'ов). Stop-фильтр чтобы тапы по панели
		// не bubbled на backdrop и не закрывали диалог.
		var panel = new PanelContainer();
		panel.AddThemeStyleboxOverride("panel", UIStyle.PanelStyle());
		panel.CustomMinimumSize = new Vector2(320, 0);
		panel.MouseFilter = MouseFilterEnum.Stop;
		center.AddChild(panel);

		var v = new VBoxContainer();
		v.AddThemeConstantOverride("separation", 10);
		panel.AddChild(v);

		var titleL = UIStyle.MakeLabel(title, 18, UIStyle.RarityColor(rarity));
		titleL.HorizontalAlignment = HorizontalAlignment.Center;
		titleL.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		v.AddChild(titleL);

		if (!string.IsNullOrEmpty(rarityTag))
		{
			var rl = UIStyle.MakeLabel(rarityTag, 11, UIStyle.TextDim);
			rl.HorizontalAlignment = HorizontalAlignment.Center;
			v.AddChild(rl);
		}

		var sep = new HSeparator();
		v.AddChild(sep);

		// Тело — скролл, чтобы длинные описания (сет + аффиксы + пассивы +
		// сравнение) не выкидывали диалог за пределы экрана на телефоне.
		var scroll = new ScrollContainer();
		scroll.CustomMinimumSize = new Vector2(0, 220);
		scroll.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;
		scroll.SizeFlagsVertical = SizeFlags.ExpandFill;
		v.AddChild(scroll);

		var bodyL = UIStyle.MakeLabel(bodyText ?? "—", 13, UIStyle.TextPrimary);
		bodyL.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		bodyL.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		scroll.AddChild(bodyL);

		var btnRow = new HBoxContainer();
		btnRow.AddThemeConstantOverride("separation", 8);
		btnRow.Alignment = BoxContainer.AlignmentMode.Center;
		v.AddChild(btnRow);

		foreach (var (label, action, primary) in actions)
		{
			var btn = new Button { Text = label };
			UIStyle.StyleButton(btn, primary: primary);
			// 44pt — стандарт touch target по Apple HIG / Material Design.
			btn.CustomMinimumSize = new Vector2(130, 44);
			btn.Pressed += () =>
			{
				HideItemDetail();
				action?.Invoke();
			};
			btnRow.AddChild(btn);
		}
	}

	private void HideItemDetail()
	{
		if (_detailDialog == null) return;
		RemoveChild(_detailDialog);
		_detailDialog.QueueFree();
		_detailDialog = null;
	}

	// =========================================================================
	// Тап по предмету в инвентаре
	// =========================================================================
	private void OpenInventorySlotDialog(int slotIndex)
	{
		var ch = GameData.Instance.Character;
		if (ch == null) return;
		var slots = ch.Inventory.Slots;
		if (slotIndex < 0 || slotIndex >= slots.Count) return;
		var st = slots[slotIndex];

		string name; string detail; ItemRarity rarity; bool isPotion = false;
		if (st.WeaponInstance != null)
		{
			name = st.WeaponInstance.Name;
			detail = ItemsDB.DescribeWeaponMultiline(st.WeaponInstance);
			rarity = st.WeaponInstance.Rarity;
		}
		else if (st.ArmorInstance != null)
		{
			name = st.ArmorInstance.Name;
			detail = ItemsDB.DescribeArmorMultiline(st.ArmorInstance);
			rarity = st.ArmorInstance.Rarity;
		}
		else if (st.ShieldInstance != null)
		{
			name = st.ShieldInstance.Name;
			detail = DescribeShieldMultiline(st.ShieldInstance);
			rarity = st.ShieldInstance.Rarity;
		}
		else
		{
			var sd = ShieldsDB.Get(st.ItemId);
			if (sd != null) { name = sd.Name; detail = DescribeShieldMultiline(sd); rarity = sd.Rarity; }
			else
			{
				var (nm, dt, _) = DescribeItem(st.ItemId);
				name = nm; detail = dt;
				rarity = GetItemRarity(st.ItemId);
				isPotion = PotionsDB.Get(st.ItemId) != null;
			}
		}

		// Тело = детали + сет-инфо + сравнение (всё что было в hover-tooltip),
		// без ведущей строки с именем — оно уже в шапке диалога.
		string fullTip = BuildItemTooltip(st, name, detail, ch);
		int firstNL = fullTip.IndexOf('\n');
		string body = firstNL >= 0 ? fullTip.Substring(firstNL + 1) : fullTip;

		var actions = new List<(string, Action, bool)>();
		if (isPotion)
		{
			if (ReadOnly)
				body += "\n\n🔒 В бою зелья пьются через панель игрока.";
			else
				actions.Add(("Использовать", () => DoUsePotion(st.ItemId), true));
		}
		else
		{
			if (ReadOnly)
				body += "\n\n🔒 Во время боя экипировку менять нельзя.";
			else if (RunLocked)
				body += "\n\n🔒 В подземелье экипировку менять нельзя — выйдите в город.";
			else
				actions.Add(("Надеть", () => DoEquip(slotIndex), true));
		}
		actions.Add(("Отмена", null, false));

		ShowItemDetail(name, RarityName(rarity), body, rarity, actions);
	}

	// =========================================================================
	// Тап по надетому слоту
	// =========================================================================
	internal void OpenEquipmentSlotDialog(string slotLabel, string itemName, string detail,
		ItemRarity rarity, bool isEmpty, Action unequipAction)
	{
		if (isEmpty)
		{
			ShowItemDetail(slotLabel, "Пусто",
				"Слот свободен. Выберите подходящий предмет из инвентаря.",
				ItemRarity.Common,
				new List<(string, Action, bool)> { ("Понятно", null, false) });
			return;
		}

		var actions = new List<(string, Action, bool)>();
		string body = detail;
		if (ReadOnly)
			body += "\n\n🔒 В бою экипировку менять нельзя.";
		else if (RunLocked)
			body += "\n\n🔒 В подземелье экипировку менять нельзя — выйдите в город.";
		else if (unequipAction != null)
			actions.Add(("Снять", unequipAction, true));
		actions.Add(("Отмена", null, false));

		ShowItemDetail(itemName, RarityName(rarity), body, rarity, actions);
	}

	private static string RarityName(ItemRarity r) => r switch
	{
		ItemRarity.Common    => "Обычное",
		ItemRarity.Uncommon  => "Необычное",
		ItemRarity.Rare      => "Редкое",
		ItemRarity.Heroic    => "Героическое",
		ItemRarity.Epic      => "Эпическое",
		ItemRarity.Legendary => "Легендарное",
		_                    => "",
	};

	// =========================================================================
	// Реальные действия, вызываются из кнопок диалога
	// =========================================================================
	private async void DoEquip(int slotIndex)
	{
		if (BlockedForCharChange()) return;
		var outcome = await GameData.Instance.EquipFromInventoryAsync(slotIndex);
		if (outcome.Ok) SetStatus("Надето.", error: false);
		else SetStatus(TranslateError(outcome.Error, "Не удалось надеть."), error: true);
		Refresh();
	}

	private async void DoUsePotion(string itemId)
	{
		if (BlockedByReadOnly()) return;
		var outcome = await GameData.Instance.UsePotionAsync(itemId);
		if (outcome.Ok) SetStatus("Зелье выпито.", error: false);
		else SetStatus(TranslateError(outcome.Error, "Не удалось применить зелье."), error: true);
		Refresh();
	}
}

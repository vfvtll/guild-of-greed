using Godot;
using System;
using GuildOfGreed.Shared.Domain;
using GuildOfGreed.Shared.Data;

// InventoryOverlay — построение UI и фабрики карточек.
public partial class InventoryOverlay
{
	private void BuildUI()
	{
		// Затемнение позади панели — закрывает клики по бою.
		_dim = new ColorRect { Color = new Color(0, 0, 0, 0.7f) };
		_dim.MouseFilter = MouseFilterEnum.Stop;
		AddChild(_dim);
		UIStyle.FillParent(_dim);

		// Панель на весь экран с отступом. FillParent прибивает offsets к нулю.
		_panel = new PanelContainer();
		_panel.AddThemeStyleboxOverride("panel", UIStyle.PanelStyle());
		AddChild(_panel);
		UIStyle.FillParent(_panel, marginX: 40, marginY: 30);

		var v = new VBoxContainer();
		v.AddThemeConstantOverride("separation", 10);
		_panel.AddChild(v);

		// === Шапка: 🎒 название | кошель | ✕ ===
		// Кошель перенесён сюда из отдельной строки — экономит вертикаль.
		var titleRow = new HBoxContainer();
		titleRow.AddThemeConstantOverride("separation", 12);
		v.AddChild(titleRow);

		var title = UIStyle.MakeLabel("🎒 Инвентарь", 22, UIStyle.GoldBright);
		title.HorizontalAlignment = HorizontalAlignment.Left;
		titleRow.AddChild(title);

		_currencyLabel = new RichTextLabel
		{
			BbcodeEnabled = true,
			FitContent = true,
			ScrollActive = false,
			CustomMinimumSize = new Vector2(0, 24),
		};
		_currencyLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		_currencyLabel.AddThemeFontSizeOverride("normal_font_size", 14);
		_currencyLabel.AddThemeColorOverride("default_color", UIStyle.TextPrimary);
		titleRow.AddChild(_currencyLabel);

		var xBtn = new Button { Text = "✕" };
		UIStyle.StyleButton(xBtn);
		xBtn.CustomMinimumSize = new Vector2(44, 44);
		xBtn.TooltipText = "Закрыть инвентарь";
		xBtn.Pressed += Close;
		titleRow.AddChild(xBtn);

		// Плашка показа ReadOnly — видна только во время боя.
		_readOnlyHint = UIStyle.MakeLabel(
			"🔒 Бой идёт — изменения недоступны (зелья пьются через панель игрока)",
			13, UIStyle.WarnAmber);
		_readOnlyHint.HorizontalAlignment = HorizontalAlignment.Center;
		_readOnlyHint.Visible = ReadOnly;
		v.AddChild(_readOnlyHint);

		var sep = new HSeparator();
		var sepStyle = new StyleBoxFlat
		{
			BgColor = UIStyle.GoldDark,
			ContentMarginTop = 1, ContentMarginBottom = 1,
		};
		sep.AddThemeStyleboxOverride("separator", sepStyle);
		v.AddChild(sep);

		// === Сводка по эффективным параметрам ===
		// Компактная: 3 строки вместо 5. Atk+Def слиты, крит туда же.
		var summary = new PanelContainer();
		summary.AddThemeStyleboxOverride("panel", UIStyle.MiniPanelStyle());
		v.AddChild(summary);

		var summaryV = new VBoxContainer();
		summaryV.AddThemeConstantOverride("separation", 2);
		summary.AddChild(summaryV);
		_summaryLevel = UIStyle.MakeLabel("", 13, UIStyle.GoldBright);
		_summaryAtk   = UIStyle.MakeLabel("", 13, UIStyle.TextPrimary);
		_summaryDef   = UIStyle.MakeLabel("", 13, UIStyle.TextPrimary);
		_summarySets  = UIStyle.MakeLabel("", 12, UIStyle.RarityUncommon);
		_summarySets.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		_summaryAtk.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		_summaryDef.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		summaryV.AddChild(_summaryLevel);
		summaryV.AddChild(_summaryAtk);
		summaryV.AddChild(_summaryDef);
		summaryV.AddChild(_summarySets);

		// === Распределение очков статов ===
		// Сетка 3×2 вместо HBox на 6 кнопок: 6×140 = 840px не помещается
		// на 1280-ширине после margin'ов. 3×2 ≈ 420×каждая по 100px широки.
		_statSpendPanel = new PanelContainer();
		_statSpendPanel.AddThemeStyleboxOverride("panel", UIStyle.MiniPanelStyle());
		v.AddChild(_statSpendPanel);

		var spendV = new VBoxContainer();
		spendV.AddThemeConstantOverride("separation", 6);
		_statSpendPanel.AddChild(spendV);

		_statPointsAvailLabel = UIStyle.MakeLabel("", 14, UIStyle.GoldBright);
		spendV.AddChild(_statPointsAvailLabel);

		var spendGrid = new GridContainer { Columns = 3 };
		spendGrid.AddThemeConstantOverride("h_separation", 6);
		spendGrid.AddThemeConstantOverride("v_separation", 4);
		spendV.AddChild(spendGrid);

		for (int i = 0; i < 6; i++)
		{
			var btn = new Button { Text = "" };
			UIStyle.StyleButton(btn);
			btn.CustomMinimumSize = new Vector2(120, 0);
			btn.SizeFlagsHorizontal = SizeFlags.ExpandFill;
			string capturedStat = StatIds[i];
			btn.TooltipText = StatTooltip(capturedStat);
			btn.Pressed += () => OnSpendStatPressed(capturedStat);
			spendGrid.AddChild(btn);
			_statSpendButtons[i] = btn;
		}

		// Две колонки: надето слева, инвентарь справа. ExpandFill даёт им
		// доступную высоту целиком — оба внутренних скролла растягиваются.
		var columns = new HBoxContainer();
		columns.AddThemeConstantOverride("separation", 20);
		columns.SizeFlagsVertical = SizeFlags.ExpandFill;
		columns.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		v.AddChild(columns);

		// === Левая колонка: надето ===
		var left = new VBoxContainer { CustomMinimumSize = new Vector2(260, 0) };
		left.AddThemeConstantOverride("separation", 6);
		columns.AddChild(left);

		left.AddChild(UIStyle.MakeSectionTitle("Надето"));

		var equipScroll = new ScrollContainer();
		equipScroll.SizeFlagsVertical = SizeFlags.ExpandFill;
		equipScroll.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;
		left.AddChild(equipScroll);

		_equipmentList = new VBoxContainer();
		_equipmentList.AddThemeConstantOverride("separation", 5);
		_equipmentList.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		equipScroll.AddChild(_equipmentList);

		var hint = UIStyle.MakeLabel("Клик — снять в инвентарь", 11, UIStyle.TextDim);
		left.AddChild(hint);

		// === Правая колонка: инвентарь ===
		var right = new VBoxContainer();
		right.AddThemeConstantOverride("separation", 6);
		right.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		right.SizeFlagsVertical   = SizeFlags.ExpandFill;
		columns.AddChild(right);

		_capacityLabel = UIStyle.MakeSectionTitle("Содержимое");
		right.AddChild(_capacityLabel);

		// 40 ячеек × 50px = 400px + отступы — при 8 рядах легко выходит за
		// экран. ScrollContainer держит сетку всегда внутри границ панели.
		var invScroll = new ScrollContainer();
		invScroll.SizeFlagsVertical = SizeFlags.ExpandFill;
		invScroll.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		invScroll.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;
		right.AddChild(invScroll);

		_inventoryGrid = new GridContainer { Columns = GridColumns };
		_inventoryGrid.AddThemeConstantOverride("h_separation", 6);
		_inventoryGrid.AddThemeConstantOverride("v_separation", 6);
		invScroll.AddChild(_inventoryGrid);

		var hint2 = UIStyle.MakeLabel("Клик — надеть / выпить зелье", 11, UIStyle.TextDim);
		right.AddChild(hint2);

		// === Низ: статус + закрыть ===
		_statusLabel = UIStyle.MakeLabel("", 13, UIStyle.WarnAmber);
		_statusLabel.HorizontalAlignment = HorizontalAlignment.Center;
		v.AddChild(_statusLabel);

		var btnRow = new HBoxContainer();
		btnRow.Alignment = BoxContainer.AlignmentMode.Center;
		v.AddChild(btnRow);

		var closeBtn = new Button { Text = "Закрыть" };
		UIStyle.StyleButton(closeBtn, primary: true);
		closeBtn.CustomMinimumSize = new Vector2(180, 40);
		closeBtn.Pressed += Close;
		btnRow.AddChild(closeBtn);
	}

	// Подсказка для каждой кнопки распределения очков.
	private static string StatTooltip(string stat) => stat switch
	{
		"STR" => "Физическая атака",
		"INT" => "Магическая атака",
		"CON" => "Здоровье (ХП)",
		"WIT" => "Регенерация маны",
		"MEN" => "Максимум маны",
		"DEX" => "Блок, шанс и сила крита",
		_     => "",
	};

	// =====================================================================
	// Фабрики карточек слотов
	// =====================================================================

	// Action unequipAction передаётся в диалог; сам клик по строке открывает
	// детализацию через OpenEquipmentSlotDialog (см. .Detail.cs).
	private Control MakeWeaponSlotRow(string icon, string slotName, WeaponData item, Action unequipAction)
	{
		string itemName = item?.Name ?? "—";
		string detail   = item == null ? "(пусто)" : ItemsDB.DescribeWeaponMultiline(item);
		var rarity = item?.Rarity ?? ItemRarity.Common;
		bool isEmpty = item == null;
		Action onClick = () => OpenEquipmentSlotDialog(slotName, itemName, detail, rarity, isEmpty, unequipAction);
		return BuildSlotRow(icon, slotName, itemName, detail, isEmpty, rarity, onClick);
	}

	// Off-hand: одна строка, варьируется по состоянию.
	//   weapon занят 2H → "Двуручное оружие — обе руки"
	//   offhand != null → оружие в левой руке (dual-wield, −2 к HandSize)
	//   shield  != null → щит (−1 к HandSize)
	//   ничего → пустой слот "вторая рука свободна"
	private Control MakeOffhandRow(CharacterData ch)
	{
		if (ch.Weapon != null && ch.Weapon.IsTwoHanded)
		{
			const string slot = "Вторая рука";
			const string detail = "Двуручное оружие занимает обе руки";
			return BuildSlotRow("✊", slot, "—", detail, true, ItemRarity.Common,
				() => OpenEquipmentSlotDialog(slot, "—", detail, ItemRarity.Common, true, null));
		}

		if (ch.Offhand != null)
		{
			const string slot = "Левая рука (dual-wield)";
			string detail = ItemsDB.DescribeWeaponMultiline(ch.Offhand)
				+ "\n\n⚠ Dual-wield: −2 к размеру руки в бою.";
			return BuildSlotRow("⚔", slot, ch.Offhand.Name,
				detail, false, ch.Offhand.Rarity,
				() => OpenEquipmentSlotDialog(slot, ch.Offhand.Name, detail,
					ch.Offhand.Rarity, false, UnequipOffhand));
		}

		if (ch.Shield != null)
		{
			const string slot = "Щит";
			string detail = DescribeShieldMultiline(ch.Shield)
				+ "\n\n⚠ Щит: −1 к размеру руки в бою.";
			return BuildSlotRow("🛡", slot, ch.Shield.Name,
				detail, false, ch.Shield.Rarity,
				() => OpenEquipmentSlotDialog(slot, ch.Shield.Name, detail,
					ch.Shield.Rarity, false, UnequipShield));
		}

		{
			const string slot = "Левая рука";
			const string detail = "Можно вторую 1H-руку (dual-wield: −2 к размеру руки)\n"
				+ "или щит (−1 к размеру руки)";
			return BuildSlotRow("✊", slot, "—", detail, true, ItemRarity.Common,
				() => OpenEquipmentSlotDialog(slot, "—", detail, ItemRarity.Common, true, null));
		}
	}

	private static string DescribeShieldMultiline(ShieldData s)
	{
		if (s == null) return "—";
		var lines = new System.Collections.Generic.List<string>();
		if (s.PhysDef > 0) lines.Add($"{s.PhysDef} ФизЗащ");
		if (s.MagDef > 0)  lines.Add($"{s.MagDef} МагЗащ");
		string effect = s.Type switch
		{
			ShieldType.Magic    => $"+{s.EffectMagnitude}% возврат маг.урона",
			ShieldType.Physical => $"+{s.EffectMagnitude}% MaxHP в блок начала хода",
			ShieldType.Balanced => $"+{s.EffectMagnitude}% counter-buff (1 ход)",
			_                    => "",
		};
		if (!string.IsNullOrEmpty(effect))
		{
			if (lines.Count > 0) lines.Add("");
			lines.Add("Эффект:");
			lines.Add($"  • {effect}");
		}
		return lines.Count == 0 ? "—" : string.Join("\n", lines);
	}

	private Control MakeArmorSlotRow(string icon, string slotName, ArmorData item, Action unequipAction)
	{
		string itemName = item?.Name ?? "—";
		string detail   = item == null ? "(пусто)" : ItemsDB.DescribeArmorMultiline(item);
		var rarity = item?.Rarity ?? ItemRarity.Common;
		bool isEmpty = item == null;
		Action onClick = () => OpenEquipmentSlotDialog(slotName, itemName, detail, rarity, isEmpty, unequipAction);
		return BuildSlotRow(icon, slotName, itemName, detail, isEmpty, rarity, onClick);
	}

	private Control BuildSlotRow(string icon, string slotName, string itemName, string detail,
		bool isEmpty, ItemRarity rarity, Action onClick)
	{
		var panel = new PanelContainer { CustomMinimumSize = new Vector2(260, 44) };
		var hovered = false;
		ApplySlotStyle(panel, hovered, isEmpty);
		panel.MouseFilter = MouseFilterEnum.Stop;
		panel.TooltipText = $"{slotName}: {itemName}\n{detail}";
		panel.MouseEntered += () => { hovered = true; ApplySlotStyle(panel, hovered, isEmpty); };
		panel.MouseExited  += () => { hovered = false; ApplySlotStyle(panel, hovered, isEmpty); };
		panel.GuiInput += ev =>
		{
			if (ev is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left)
				onClick();
		};

		var h = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
		h.AddThemeConstantOverride("separation", 8);
		panel.AddChild(h);

		var iconL = UIStyle.MakeLabel(icon, 22, UIStyle.GoldBright);
		iconL.CustomMinimumSize = new Vector2(36, 0);
		iconL.HorizontalAlignment = HorizontalAlignment.Center;
		h.AddChild(iconL);

		var col = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
		col.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		h.AddChild(col);

		var slotL = UIStyle.MakeLabel(slotName, 10, UIStyle.TextDim);
		col.AddChild(slotL);

		var nameColor = isEmpty ? UIStyle.TextDim : UIStyle.RarityColor(rarity);
		var nameL = UIStyle.MakeLabel(itemName, 12, nameColor);
		nameL.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		col.AddChild(nameL);

		return panel;
	}

	// Размеры карточек. Меньше = больше слотов помещается без скролла.
	private const int ItemCardWidth  = 170;
	private const int ItemCardHeight = 50;

	private Control MakeItemCard(InventoryStack stack, Action onClick)
	{
		// Если в стаке instance — берём имя/детали с него, чтобы аффиксы попали
		// в карточку. Иначе fallback на DescribeItem(baseId).
		string name; string detail; string icon; ItemRarity rarity;
		if (stack.WeaponInstance != null)
		{
			name = stack.WeaponInstance.Name;
			detail = ItemsDB.DescribeWeaponMultiline(stack.WeaponInstance);
			icon = "⚔";
			rarity = stack.WeaponInstance.Rarity;
		}
		else if (stack.ArmorInstance != null)
		{
			name = stack.ArmorInstance.Name;
			detail = ItemsDB.DescribeArmorMultiline(stack.ArmorInstance);
			icon = SlotIcon(stack.ArmorInstance.Slot);
			rarity = stack.ArmorInstance.Rarity;
		}
		else if (stack.ShieldInstance != null)
		{
			name = stack.ShieldInstance.Name;
			detail = DescribeShieldMultiline(stack.ShieldInstance);
			icon = "🛡";
			rarity = stack.ShieldInstance.Rarity;
		}
		else
		{
			// Может быть базовым shieldId из ShieldsDB.
			var sd = ShieldsDB.Get(stack.ItemId);
			if (sd != null)
			{
				name = sd.Name;
				detail = DescribeShieldMultiline(sd);
				icon = "🛡";
				rarity = sd.Rarity;
			}
			else
			{
				(name, detail, icon) = DescribeItem(stack.ItemId);
				rarity = GetItemRarity(stack.ItemId);
			}
		}

		var ch = GameData.Instance.Character;

		var panel = new PanelContainer { CustomMinimumSize = new Vector2(ItemCardWidth, ItemCardHeight) };
		var hovered = false;
		ApplyItemStyle(panel, hovered, rarity);
		panel.MouseFilter = MouseFilterEnum.Stop;
		panel.TooltipText = BuildItemTooltip(stack, name, detail, ch);
		panel.MouseEntered += () => { hovered = true; ApplyItemStyle(panel, hovered, rarity); };
		panel.MouseExited  += () => { hovered = false; ApplyItemStyle(panel, hovered, rarity); };
		panel.GuiInput += ev =>
		{
			if (ev is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left)
				onClick();
		};

		var h = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
		h.AddThemeConstantOverride("separation", 6);
		panel.AddChild(h);

		var iconL = UIStyle.MakeLabel(icon, 22, UIStyle.GoldBright);
		iconL.CustomMinimumSize = new Vector2(28, 0);
		iconL.HorizontalAlignment = HorizontalAlignment.Center;
		h.AddChild(iconL);

		var nameL = UIStyle.MakeLabel(name, 11, UIStyle.RarityColor(rarity));
		nameL.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		nameL.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		h.AddChild(nameL);

		if (stack.Count > 1)
		{
			var countL = UIStyle.MakeLabel($"×{stack.Count}", 14, UIStyle.WarnAmber);
			h.AddChild(countL);
		}

		return panel;
	}

	private Control MakeEmptyCard()
	{
		var panel = new PanelContainer { CustomMinimumSize = new Vector2(ItemCardWidth, ItemCardHeight) };
		panel.MouseFilter = MouseFilterEnum.Ignore;
		var sb = UIStyle.MiniPanelStyle();
		sb.BorderColor = UIStyle.GoldDark * 0.4f;
		sb.BgColor = new Color(0.08f, 0.07f, 0.10f);
		panel.AddThemeStyleboxOverride("panel", sb);

		var l = UIStyle.MakeLabel("—", 14, UIStyle.TextDim);
		l.HorizontalAlignment = HorizontalAlignment.Center;
		panel.AddChild(l);
		return panel;
	}

	private static void ApplySlotStyle(PanelContainer p, bool hovered, bool isEmpty)
	{
		var sb = UIStyle.MiniPanelStyle();
		sb.BorderWidthLeft = 2; sb.BorderWidthRight = 2;
		sb.BorderWidthTop = 2; sb.BorderWidthBottom = 2;
		if (isEmpty)
		{
			sb.BgColor = new Color(0.10f, 0.09f, 0.13f);
			sb.BorderColor = UIStyle.GoldDark * 0.5f;
		}
		else if (hovered)
		{
			sb.BgColor = new Color(0.20f, 0.16f, 0.10f);
			sb.BorderColor = UIStyle.GoldBright;
		}
		else
		{
			sb.BgColor = UIStyle.PanelBgLight;
			sb.BorderColor = UIStyle.GoldMid;
		}
		p.AddThemeStyleboxOverride("panel", sb);
	}

	private static void ApplyItemStyle(PanelContainer p, bool hovered, ItemRarity rarity = ItemRarity.Common)
	{
		var sb = UIStyle.MiniPanelStyle();
		sb.BorderWidthLeft = 2; sb.BorderWidthRight = 2;
		sb.BorderWidthTop = 2; sb.BorderWidthBottom = 2;
		var rarityCol = UIStyle.RarityColor(rarity);
		if (hovered)
		{
			sb.BgColor = new Color(0.20f, 0.18f, 0.26f);
			sb.BorderColor = UIStyle.GoldBright;
		}
		else
		{
			sb.BgColor = UIStyle.PanelBgLight;
			sb.BorderColor = rarityCol;
		}
		p.AddThemeStyleboxOverride("panel", sb);
	}
}

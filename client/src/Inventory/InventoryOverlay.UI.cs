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
		_dim.SetAnchorsPreset(LayoutPreset.FullRect);
		_dim.MouseFilter = MouseFilterEnum.Stop;
		AddChild(_dim);

		_panel = new PanelContainer
		{
			Position = new Vector2(70, 30),
			Size = new Vector2(1140, 660),
			CustomMinimumSize = new Vector2(1140, 660),
		};
		_panel.AddThemeStyleboxOverride("panel", UIStyle.PanelStyle());
		AddChild(_panel);

		var v = new VBoxContainer();
		v.AddThemeConstantOverride("separation", 12);
		_panel.AddChild(v);

		// Шапка: иконка слева для центровки + название по центру + ✕ справа.
		// Кнопка ✕ дублирует "Закрыть" внизу — гарантия что закрыть можно
		// независимо от размера экрана и количества контента.
		var titleRow = new HBoxContainer();
		titleRow.AddThemeConstantOverride("separation", 8);
		v.AddChild(titleRow);

		var leftSpacer = new Control { CustomMinimumSize = new Vector2(44, 0) };
		titleRow.AddChild(leftSpacer);

		var title = UIStyle.MakeLabel("🎒 Инвентарь", 22, UIStyle.GoldBright);
		title.HorizontalAlignment = HorizontalAlignment.Center;
		title.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		titleRow.AddChild(title);

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
		var summary = new PanelContainer();
		summary.AddThemeStyleboxOverride("panel", UIStyle.MiniPanelStyle());
		v.AddChild(summary);

		var summaryV = new VBoxContainer();
		summaryV.AddThemeConstantOverride("separation", 2);
		summary.AddChild(summaryV);
		_summaryAtk  = UIStyle.MakeLabel("", 13, UIStyle.TextPrimary);
		_summaryDef  = UIStyle.MakeLabel("", 13, UIStyle.TextPrimary);
		_summaryCrit = UIStyle.MakeLabel("", 13, UIStyle.WarnAmber);
		_summarySets = UIStyle.MakeLabel("", 12, UIStyle.RarityUncommon);
		_summarySets.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		summaryV.AddChild(_summaryAtk);
		summaryV.AddChild(_summaryDef);
		summaryV.AddChild(_summaryCrit);
		summaryV.AddChild(_summarySets);

		// Две колонки: надето слева, инвентарь справа.
		var columns = new HBoxContainer();
		columns.AddThemeConstantOverride("separation", 24);
		columns.SizeFlagsVertical = SizeFlags.ExpandFill;
		v.AddChild(columns);

		// === Левая колонка: надето ===
		var left = new VBoxContainer { CustomMinimumSize = new Vector2(280, 0) };
		left.AddThemeConstantOverride("separation", 8);
		columns.AddChild(left);

		left.AddChild(UIStyle.MakeSectionTitle("Надето"));

		// 8 слотов могут не помещаться по высоте на маленьких экранах —
		// ScrollContainer гарантирует что всё остаётся доступно через скролл.
		var equipScroll = new ScrollContainer();
		equipScroll.SizeFlagsVertical = SizeFlags.ExpandFill;
		equipScroll.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;
		left.AddChild(equipScroll);

		_equipmentList = new VBoxContainer();
		_equipmentList.AddThemeConstantOverride("separation", 6);
		_equipmentList.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		equipScroll.AddChild(_equipmentList);

		var hint = UIStyle.MakeLabel("Клик — снять в инвентарь", 11, UIStyle.TextDim);
		left.AddChild(hint);

		// === Правая колонка: инвентарь ===
		var right = new VBoxContainer();
		right.AddThemeConstantOverride("separation", 8);
		right.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		columns.AddChild(right);

		_capacityLabel = UIStyle.MakeSectionTitle("Содержимое");
		right.AddChild(_capacityLabel);

		// Кошель: цветные номиналы золото / серебро / медь. Reused в Refresh.
		_currencyLabel = new RichTextLabel
		{
			BbcodeEnabled = true,
			FitContent = true,
			ScrollActive = false,
			CustomMinimumSize = new Vector2(0, 22),
		};
		_currencyLabel.AddThemeFontSizeOverride("normal_font_size", 14);
		_currencyLabel.AddThemeColorOverride("default_color", UIStyle.TextPrimary);
		right.AddChild(_currencyLabel);

		_inventoryGrid = new GridContainer { Columns = GridColumns };
		_inventoryGrid.AddThemeConstantOverride("h_separation", 8);
		_inventoryGrid.AddThemeConstantOverride("v_separation", 8);
		right.AddChild(_inventoryGrid);

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
		closeBtn.CustomMinimumSize = new Vector2(180, 44);
		closeBtn.Pressed += Close;
		btnRow.AddChild(closeBtn);
	}

	// =====================================================================
	// Фабрики карточек слотов
	// =====================================================================

	private Control MakeWeaponSlotRow(string icon, string slotName, WeaponData item, Action onClick)
	{
		string itemName = item?.Name ?? "—";
		string detail   = item == null ? "(пусто)" : ItemsDB.DescribeWeapon(item);
		var rarity = item?.Rarity ?? ItemRarity.Common;
		return BuildSlotRow(icon, slotName, itemName, detail, item == null, rarity, onClick);
	}

	private Control MakeShieldSlotRow(string icon, string slotName, ShieldData item, Action onClick)
	{
		string itemName = item?.Name ?? "—";
		string detail   = item == null ? "(пусто)" : DescribeShield(item);
		var rarity = item?.Rarity ?? ItemRarity.Common;
		return BuildSlotRow(icon, slotName, itemName, detail, item == null, rarity, onClick);
	}

	// Off-hand: одна строка, варьируется по состоянию.
	//   weapon занят 2H → "Двуручное оружие — обе руки"
	//   offhand != null → как WeaponSlotRow
	//   shield  != null → как ShieldSlotRow
	//   ничего → пустой слот "вторая рука свободна"
	private Control MakeOffhandRow(CharacterData ch)
	{
		if (ch.Weapon != null && ch.Weapon.IsTwoHanded)
			return BuildSlotRow("✊", "Вторая рука", "—",
				"Двуручное оружие занимает обе руки", true, ItemRarity.Common, () => { });

		if (ch.Offhand != null)
			return MakeWeaponSlotRow("⚔", "Левая рука", ch.Offhand, () => UnequipOffhand());

		if (ch.Shield != null)
			return MakeShieldSlotRow("🛡", "Щит", ch.Shield, () => UnequipShield());

		return BuildSlotRow("✊", "Левая рука", "—", "Можно вторую 1H-руку или щит",
			true, ItemRarity.Common, () => { });
	}

	private static string DescribeShield(ShieldData s)
	{
		if (s == null) return "—";
		var parts = new System.Collections.Generic.List<string>();
		if (s.PhysDef > 0) parts.Add($"{s.PhysDef} ФизЗащ");
		if (s.MagDef > 0)  parts.Add($"{s.MagDef} МагЗащ");
		string effect = s.Type switch
		{
			ShieldType.Magic    => $"+{s.EffectMagnitude}% возврат маг.урона",
			ShieldType.Physical => $"+{s.EffectMagnitude}% MaxHP в блок начала хода",
			ShieldType.Balanced => $"+{s.EffectMagnitude}% counter-buff (1 ход)",
			_                    => "",
		};
		if (!string.IsNullOrEmpty(effect)) parts.Add(effect);
		return $"{s.Name}: {string.Join(", ", parts)}";
	}

	private Control MakeArmorSlotRow(string icon, string slotName, ArmorData item, Action onClick)
	{
		string itemName = item?.Name ?? "—";
		string detail   = item == null ? "(пусто)" : ItemsDB.DescribeArmor(item);
		var rarity = item?.Rarity ?? ItemRarity.Common;
		return BuildSlotRow(icon, slotName, itemName, detail, item == null, rarity, onClick);
	}

	private Control BuildSlotRow(string icon, string slotName, string itemName, string detail,
		bool isEmpty, ItemRarity rarity, Action onClick)
	{
		var panel = new PanelContainer { CustomMinimumSize = new Vector2(280, 48) };
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

	private Control MakeItemCard(InventoryStack stack, Action onClick)
	{
		// Если в стаке instance — берём имя/детали с него, чтобы аффиксы попали
		// в карточку. Иначе fallback на DescribeItem(baseId).
		string name; string detail; string icon; ItemRarity rarity;
		if (stack.WeaponInstance != null)
		{
			name = stack.WeaponInstance.Name;
			detail = ItemsDB.DescribeWeapon(stack.WeaponInstance);
			icon = "⚔";
			rarity = stack.WeaponInstance.Rarity;
		}
		else if (stack.ArmorInstance != null)
		{
			name = stack.ArmorInstance.Name;
			detail = ItemsDB.DescribeArmor(stack.ArmorInstance);
			icon = SlotIcon(stack.ArmorInstance.Slot);
			rarity = stack.ArmorInstance.Rarity;
		}
		else if (stack.ShieldInstance != null)
		{
			name = stack.ShieldInstance.Name;
			detail = DescribeShield(stack.ShieldInstance);
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
				detail = DescribeShield(sd);
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

		var panel = new PanelContainer { CustomMinimumSize = new Vector2(190, 56) };
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
		var panel = new PanelContainer { CustomMinimumSize = new Vector2(190, 56) };
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

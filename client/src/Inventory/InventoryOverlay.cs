using Godot;
using System;
using System.Collections.Generic;
using GuildOfGreed.Shared.Domain;
using GuildOfGreed.Shared.Data;

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
	private Label _summaryAtk, _summaryDef, _summaryCrit, _summarySets;
	private Label _summaryLevel;
	private RichTextLabel _currencyLabel;

	// Виджет распределения очков — показывается только когда UnspentStatPoints > 0.
	private PanelContainer _statSpendPanel;
	private Label _statPointsAvailLabel;
	private readonly Button[] _statSpendButtons = new Button[6];
	private static readonly string[] StatIds = { "STR", "INT", "CON", "WIT", "MEN", "DEX" };

	// Для slide-in/out анимаций.
	private PanelContainer _panel;
	private ColorRect _dim;
	private Vector2 _panelTargetPos;

	private const int GridColumns = 4;

	public override void _Ready()
	{
		SetAnchorsPreset(LayoutPreset.FullRect);
		MouseFilter = MouseFilterEnum.Stop;
		BuildUI();
		Refresh();
		PlayOpenAnimation();
	}

	// Закрытие через анимацию: панель уезжает вниз, фон гаснет, потом сигнал.
	// Все callsite'ы (✕, "Закрыть", внешние кнопки) должны вызывать Close(),
	// а не EmitSignal(SignalName.Closed) напрямую.
	public void Close()
	{
		var t = CreateTween().SetParallel(true)
			.SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.In);
		t.TweenProperty(_panel, "position", _panelTargetPos + new Vector2(0, 30), 0.18f);
		t.TweenProperty(_panel, "modulate:a", 0f, 0.18f);
		t.TweenProperty(_dim, "modulate:a", 0f, 0.18f);
		t.Chain().TweenCallback(Callable.From(() => EmitSignal(SignalName.Closed)));
	}

	private void PlayOpenAnimation()
	{
		if (_panel == null) return;
		_panelTargetPos = _panel.Position;
		_panel.Position = _panelTargetPos + new Vector2(0, 30);
		_panel.Modulate = new Color(1, 1, 1, 0);
		_dim.Modulate = new Color(1, 1, 1, 0);

		var t = CreateTween().SetParallel(true)
			.SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
		t.TweenProperty(_panel, "position", _panelTargetPos, 0.22f);
		t.TweenProperty(_panel, "modulate:a", 1f, 0.22f);
		t.TweenProperty(_dim, "modulate:a", 1f, 0.22f);
	}

	// =====================================================================
	// Refresh
	// =====================================================================

	private void Refresh()
	{
		var ch = GameData.Instance.Character;
		if (ch == null) return;

		_capacityLabel.Text = $"Содержимое ({ch.Inventory.Slots.Count}/{Inventory.Capacity})";
		RefreshCurrency(ch);
		RefreshSummary(ch);
		RefreshStatPoints(ch);

		ClearChildren(_equipmentList);
		_equipmentList.AddChild(MakeWeaponSlotRow("⚔",  "Оружие",   ch.Weapon, () => UnequipWeapon()));
		// Off-hand: либо второе одноручное (Offhand), либо щит (Shield),
		// либо подсказка "вторая рука свободна" / "двуручное занимает обе".
		_equipmentList.AddChild(MakeOffhandRow(ch));
		_equipmentList.AddChild(MakeArmorSlotRow("👕", "Грудь",    ch.Chest,  () => UnequipArmor(ArmorSlot.Chest)));
		_equipmentList.AddChild(MakeArmorSlotRow("⛑",  "Шлем",     ch.Helmet, () => UnequipArmor(ArmorSlot.Helmet)));
		_equipmentList.AddChild(MakeArmorSlotRow("🧤", "Перчатки", ch.Gloves, () => UnequipArmor(ArmorSlot.Gloves)));
		_equipmentList.AddChild(MakeArmorSlotRow("👢", "Сапоги",   ch.Boots,  () => UnequipArmor(ArmorSlot.Boots)));
		_equipmentList.AddChild(MakeArmorSlotRow("📿", "Амулет",   ch.Amulet, () => UnequipArmor(ArmorSlot.Amulet)));
		_equipmentList.AddChild(MakeArmorSlotRow("💍", "Кольцо 1", ch.Ring1,  () => UnequipArmor(ArmorSlot.Ring1)));
		_equipmentList.AddChild(MakeArmorSlotRow("💍", "Кольцо 2", ch.Ring2,  () => UnequipArmor(ArmorSlot.Ring2)));

		ClearChildren(_inventoryGrid);
		int total = Inventory.Capacity;
		var slots = ch.Inventory.Slots;
		for (int i = 0; i < total; i++)
		{
			if (i < slots.Count)
			{
				int idx = i;
				_inventoryGrid.AddChild(MakeItemCard(slots[i], () => UseInventorySlot(idx)));
			}
			else
			{
				_inventoryGrid.AddChild(MakeEmptyCard());
			}
		}
	}

	// Hex-цвета для номиналов кошеля. GoldBright — почти как UIStyle, но для
	// BBCode нужен литерал. Серебро/медь — отдельные оттенки, чтобы три номинала
	// читались с одного взгляда.
	private const string HexGold   = "#f3d172";
	private const string HexSilver = "#cfd2d8";
	private const string HexCopper = "#d18a4d";

	private void RefreshCurrency(CharacterData ch)
	{
		var (g, s, c) = Currency.Split(ch.Inventory.Money);
		_currencyLabel.Text =
			$"🪙 Кошель:  " +
			$"[color={HexGold}]{g}з[/color]  " +
			$"[color={HexSilver}]{s}с[/color]  " +
			$"[color={HexCopper}]{c}м[/color]";
	}

	// Прячет/показывает виджет распределения очков и обновляет тексты на
	// кнопках. Тратить очки во время боя нельзя — иначе мид-fight рост
	// статов разъехался бы с серверной копией.
	private void RefreshStatPoints(CharacterData ch)
	{
		bool hasPoints = ch.UnspentStatPoints > 0;
		_statSpendPanel.Visible = hasPoints;
		if (!hasPoints) return;

		_statPointsAvailLabel.Text =
			ReadOnly
				? $"⭐ Очков на распределение: {ch.UnspentStatPoints} (доступно после боя)"
				: $"⭐ Очков на распределение: {ch.UnspentStatPoints} — выберите стат для +1";

		int[] vals = { ch.Str, ch.Int, ch.Con, ch.Wit, ch.Men, ch.Dex };
		for (int i = 0; i < 6; i++)
		{
			_statSpendButtons[i].Text = $"+1 {StatIds[i]} ({vals[i]})";
			_statSpendButtons[i].Disabled = ReadOnly;
		}
	}

	private void OnSpendStatPressed(string stat)
	{
		if (BlockedByReadOnly()) return;
		var ch = GameData.Instance.Character;
		if (ch == null) return;
		if (!ch.TrySpendStatPoint(stat))
		{
			SetStatus("Нет очков для распределения.", error: true);
			return;
		}
		SetStatus($"+1 {stat}. Осталось очков: {ch.UnspentStatPoints}.", error: false);
		Refresh();
	}

	private void RefreshSummary(CharacterData ch)
	{
		int physAtk = ch.Str / 3 + ch.WeaponPhysAtk() + ch.PhysAtkBonus();
		int magAtk  = ch.Int / 3 + ch.WeaponMagAtk() + ch.MagicAtkBonus();
		int def = ch.PhysDef();
		int hp = ch.MaxHp();
		int mp = ch.MaxMp();
		int regen = ch.MpRegen();
		// Уровень персонажа + (если есть оружие) уровень навыка оружия.
		string levelLine = $"⭐ Уровень {ch.Level} ({ch.Exp}/{ch.XpForNextCharacterLevel()} XP)";
		if (ch.Weapon != null)
		{
			string wt = ch.Weapon.Type;
			int wlvl = ch.GetWeaponLevel(wt);
			int wxp  = ch.GetWeaponXp(wt);
			int wnext = ch.XpForNextWeaponLevel(wt);
			levelLine += $"   🗡 {ItemsDB.WeaponTypeName(wt)} ур.{wlvl} ({wxp}/{wnext})";
		}
		_summaryLevel.Text = levelLine;
		_summaryAtk.Text =
			$"⚔ ФизАтк {physAtk} × {ch.PhysMult():F1}    🔮 МагАтк {magAtk} × {ch.MagicMult():F1}";
		_summaryDef.Text =
			$"🛡 Защ {def}    ❤ ХП {hp}    💧 МП {mp} (+{regen}/ход)    ✋ Хэнд {ch.HandSize()}";
		string critText = ch.Weapon != null
			? $"🎯 Крит каждые {ch.EffectiveCritEveryN()} ат. × {ch.CritMultiplier():F2}"
			: "🎯 Без оружия — нет крита";
		_summaryCrit.Text = critText;
		_summarySets.Text = BuildSetsSummary(ch);
	}

	// Текстовая сводка активных сетов: "🔗 Кожанка следопыта 4/4: +2 ФизАтк, +3 ФизЗащ, +15 ХП".
	// Если ни один сет не активирован (нет соответствующих бонусов на пороге parts) —
	// возвращает пустую строку, чтобы лишний пробел не съел место.
	private static string BuildSetsSummary(CharacterData ch)
	{
		var active = ch.ActiveSets();
		if (active.Count == 0) return "";
		var lines = new System.Collections.Generic.List<string>();
		foreach (var kv in active)
		{
			var set = SetsDB.Get(kv.Key);
			if (set == null) continue;
			int parts = kv.Value;
			int total = set.PartIds.Count;
			var bonuses = new System.Collections.Generic.List<string>();
			foreach (var b in SetsDB.ActiveBonusesFor(set, parts))
				bonuses.Add(FormatSetBonus(b));
			if (bonuses.Count == 0)
			{
				lines.Add($"🔗 {set.Name} {parts}/{total} — нет активных бонусов");
				continue;
			}
			lines.Add($"🔗 {set.Name} {parts}/{total}: {string.Join(", ", bonuses)}");
		}
		return string.Join("\n", lines);
	}

	private static string FormatSetBonus(SetBonus b)
	{
		string sign = b.IsPercent ? "%" : "";
		return $"+{b.Magnitude}{sign} {AffixesDB.StatName(b.Kind)}";
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

	private void UnequipOffhand()
	{
		if (BlockedByReadOnly()) return;
		if (!GameData.Instance.UnequipOffhand())
			SetStatus("Не получилось снять — инвентарь полон.", error: true);
		else SetStatus("Второе оружие снято.", error: false);
		Refresh();
	}

	private void UnequipShield()
	{
		if (BlockedByReadOnly()) return;
		if (!GameData.Instance.UnequipShield())
			SetStatus("Не получилось снять — инвентарь полон.", error: true);
		else SetStatus("Щит снят.", error: false);
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

	private void UseInventorySlot(int slotIndex)
	{
		if (BlockedByReadOnly()) return;
		var slots = GameData.Instance.Character?.Inventory?.Slots;
		if (slots == null || slotIndex < 0 || slotIndex >= slots.Count) return;
		var st = slots[slotIndex];

		// Зелье — пьём. Только для стак-предметов: instance не бывает зельем.
		if (st.WeaponInstance == null && st.ArmorInstance == null
			&& PotionsDB.Get(st.ItemId) != null)
		{
			if (GameData.Instance.UsePotion(st.ItemId))
				SetStatus("Зелье выпито.", error: false);
			else SetStatus("Не удалось применить зелье.", error: true);
			Refresh();
			return;
		}

		// Оружие/броня — надеваем.
		if (GameData.Instance.EquipFromInventory(slotIndex))
			SetStatus("Надето.", error: false);
		else SetStatus("Не удалось надеть.", error: true);
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

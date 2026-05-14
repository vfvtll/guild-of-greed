using System.Collections.Generic;
using Godot;
using GuildOfGreed.Shared.Domain;
using GuildOfGreed.Shared.Data;

// Хаб между забегами. Игрок выбирает локацию для следующего run'а или
// открывает инвентарь чтобы перераспределить экипировку.
//
// Сигналы:
//   LocationChosen(int index)     — игрок выбрал локацию.
//   ResetCharacterRequested()      — кнопка «Новый персонаж».
//
// Локации сгруппированы по грейду (E/D/C/...) — вверху лента табов, переключают
// фильтр. Карточки выбранного грейда лежат в ScrollContainer (на случай 4+ карт).
public partial class LocationSelectView : Control
{
	[Signal] public delegate void LocationChosenEventHandler(int index);
	[Signal] public delegate void ResetCharacterRequestedEventHandler();
	[Signal] public delegate void TownRequestedEventHandler();

	private InventoryOverlay _inventoryOverlay;
	private string _selectedGradeTab = "E";
	private HBoxContainer _tabRow;
	private HFlowContainer _cardsRow;

	public override void _Ready()
	{
		UIStyle.FillParent(this);
		BuildUI();
	}

	private void BuildUI()
	{
		var bg = new ColorRect { Color = UIStyle.BgDeep };
		UIStyle.FillParent(bg);
		bg.MouseFilter = MouseFilterEnum.Ignore;
		AddChild(bg);

		// === Top bar ===
		var top = new HBoxContainer { Position = new Vector2(20, 12) };
		top.AddThemeConstantOverride("separation", 10);
		AddChild(top);

		var inventoryBtn = new Button { Text = "🎒 Инвентарь" };
		UIStyle.StyleButton(inventoryBtn);
		inventoryBtn.Pressed += OnInventoryPressed;
		top.AddChild(inventoryBtn);

		var townBtn = new Button { Text = "🏛 Город" };
		UIStyle.StyleButton(townBtn);
		townBtn.Pressed += () => EmitSignal(SignalName.TownRequested);
		top.AddChild(townBtn);

		var resetBtn = new Button { Text = "👤 Новый персонаж" };
		UIStyle.StyleButton(resetBtn);
		resetBtn.Pressed += () => EmitSignal(SignalName.ResetCharacterRequested);
		top.AddChild(resetBtn);

		// === Player snippet ===
		var p = GameData.Instance.Character;
		if (p != null)
		{
			var hp = UIStyle.MakeLabel(
				$"{p.CharacterName}   ❤ {p.CurrentHp}/{p.MaxHp()}   ✦ {p.CurrentMp}/{p.MaxMp()}   ⚔ {p.Weapon?.Name ?? "—"}",
				14, UIStyle.TextPrimary);
			hp.Position = new Vector2(440, 22);
			AddChild(hp);
		}

		// === Title ===
		var title = UIStyle.MakeLabel("🗡  Выбор подземелья", 28, UIStyle.GoldBright);
		title.Position = new Vector2(440, 90);
		AddChild(title);

		var sub = UIStyle.MakeLabel(
			"Каждое подземелье — карта с врагами, отдыхом, сокровищами и боссом в конце.",
			14, UIStyle.TextSecondary);
		sub.Position = new Vector2(440, 130);
		AddChild(sub);

		// === Табы по грейдам ===
		// Дефолтная вкладка — грейд персонажа, если есть локации для неё.
		var available = AvailableGradeTabs();
		if (p != null && available.Contains(p.Grade))
			_selectedGradeTab = p.Grade;
		else if (available.Count > 0)
			_selectedGradeTab = available[0];

		_tabRow = new HBoxContainer
		{
			Position = new Vector2(60, 170),
			Size = new Vector2(1160, 40),
		};
		_tabRow.AddThemeConstantOverride("separation", 8);
		AddChild(_tabRow);

		// === Карточки в ScrollContainer ===
		var scroll = new ScrollContainer
		{
			Position = new Vector2(60, 220),
			Size = new Vector2(1160, 760),
		};
		scroll.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;
		scroll.VerticalScrollMode = ScrollContainer.ScrollMode.Auto;
		AddChild(scroll);

		_cardsRow = new HFlowContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill,
		};
		_cardsRow.AddThemeConstantOverride("h_separation", 18);
		_cardsRow.AddThemeConstantOverride("v_separation", 18);
		scroll.AddChild(_cardsRow);

		RebuildTabs();
		RebuildCards();
	}

	// Список грейдов, которые встречаются среди локаций (для рисования табов).
	// Сохраняет порядок E → D → C → B → A → S.
	private static List<string> AvailableGradeTabs()
	{
		var seen = new HashSet<string>();
		for (int i = 0; i < GameData.LocationRequiredLevel.Length; i++)
			seen.Add(GradeForLocation(i));
		var order = new[] { "E", "D", "C", "B", "A", "S" };
		var result = new List<string>();
		foreach (var g in order) if (seen.Contains(g)) result.Add(g);
		return result;
	}

	private static string GradeForLocation(int index)
	{
		int req = index < GameData.LocationRequiredLevel.Length
			? GameData.LocationRequiredLevel[index] : 1;
		return CharacterData.GradeForLevel(req);
	}

	private void RebuildTabs()
	{
		foreach (Node c in _tabRow.GetChildren())
		{
			_tabRow.RemoveChild(c);
			c.QueueFree();
		}
		foreach (var grade in AvailableGradeTabs())
		{
			var btn = new Button { Text = $"{grade}-грейд" };
			UIStyle.StyleButton(btn, primary: grade == _selectedGradeTab);
			btn.CustomMinimumSize = new Vector2(140, 36);
			string captured = grade;
			btn.Pressed += () =>
			{
				_selectedGradeTab = captured;
				RebuildTabs();
				RebuildCards();
			};
			_tabRow.AddChild(btn);
		}
	}

	private void RebuildCards()
	{
		foreach (Node c in _cardsRow.GetChildren())
		{
			_cardsRow.RemoveChild(c);
			c.QueueFree();
		}
		for (int i = 0; i < GameData.LocationNames.Length; i++)
		{
			if (GradeForLocation(i) != _selectedGradeTab) continue;
			_cardsRow.AddChild(MakeLocationCard(i));
		}
	}

	private PanelContainer MakeLocationCard(int index)
	{
		var card = new PanelContainer
		{
			CustomMinimumSize = new Vector2(360, 220),
		};
		card.AddThemeStyleboxOverride("panel", UIStyle.PanelStyle());

		var v = new VBoxContainer();
		v.AddThemeConstantOverride("separation", 8);
		card.AddChild(v);

		v.AddChild(UIStyle.MakeSectionTitle(GameData.LocationNames[index]));

		// Требование уровня — показываем только если оно есть (>1).
		// Если игрок не дорос, подсвечиваем красным и блокируем кнопку.
		var ch = GameData.Instance.Character;
		int requiredLevel = index < GameData.LocationRequiredLevel.Length
			? GameData.LocationRequiredLevel[index] : 1;
		bool locked = ch != null && requiredLevel > 1 && ch.Level < requiredLevel;
		if (requiredLevel > 1)
		{
			var color = locked ? UIStyle.DangerRed : UIStyle.WarnAmber;
			var req = UIStyle.MakeLabel($"Требуется уровень: {requiredLevel}", 13, color);
			v.AddChild(req);
		}

		var hint = UIStyle.MakeLabel(GameData.LocationHints[index], 13, UIStyle.TextSecondary);
		hint.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		hint.CustomMinimumSize = new Vector2(330, 0);
		v.AddChild(hint);

		// Заполняющий пробел чтобы кнопка прижалась к низу.
		var spacer = new Control { SizeFlagsVertical = SizeFlags.ExpandFill };
		v.AddChild(spacer);

		var btn = new Button { Text = locked ? $"🔒 Уровень < {requiredLevel}" : "Войти →" };
		UIStyle.StyleButton(btn, primary: !locked);
		btn.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		btn.Disabled = locked;
		int captured = index;
		btn.Pressed += () => EmitSignal(SignalName.LocationChosen, captured);
		v.AddChild(btn);

		return card;
	}

	private void OnInventoryPressed()
	{
		if (_inventoryOverlay != null) return;
		_inventoryOverlay = new InventoryOverlay { ReadOnly = false };
		_inventoryOverlay.Closed += OnInventoryClosed;
		AddChild(_inventoryOverlay);
	}

	private void OnInventoryClosed()
	{
		if (_inventoryOverlay == null) return;
		RemoveChild(_inventoryOverlay);
		_inventoryOverlay.QueueFree();
		_inventoryOverlay = null;
	}
}

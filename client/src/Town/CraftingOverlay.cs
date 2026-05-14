using Godot;
using System.Collections.Generic;
using GuildOfGreed.Shared.Domain;
using GuildOfGreed.Shared.Data;

// Мастерская крафта. Базовый уровень: рецепты на E и D предметы,
// одна кнопка "Скрафтить" на каждую запись. Стиль повторяет ForgeOverlay
// (общий с городскими overlays паттерн dim+panel+scroll).
//
// Левая колонка — переключатель грейда (E / D). Правая — список рецептов
// текущего грейда с ингредиентами и кнопкой крафта.
public partial class CraftingOverlay : Control
{
	[Signal] public delegate void ClosedEventHandler();

	private PanelContainer _panel;
	private ColorRect _dim;
	private VBoxContainer _recipeList;
	private Label _statusLabel;
	private Label _resourcesLabel;
	private Label _skillSummary;
	private string _selectedGrade = "E";
	private Button _gradeEBtn;
	private Button _gradeDBtn;

	public override void _Ready()
	{
		UIStyle.FillParent(this);
		MouseFilter = MouseFilterEnum.Stop;
		BuildUI();
		Refresh();
		PlayOpenAnimation();
	}

	public void Close()
	{
		var t = CreateTween().SetParallel(true)
			.SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.In);
		t.TweenProperty(_panel, "modulate:a", 0f, 0.18f);
		t.TweenProperty(_dim, "modulate:a", 0f, 0.18f);
		t.Chain().TweenCallback(Callable.From(() => EmitSignal(SignalName.Closed)));
	}

	private void PlayOpenAnimation()
	{
		_panel.Modulate = new Color(1, 1, 1, 0);
		_dim.Modulate = new Color(1, 1, 1, 0);
		var t = CreateTween().SetParallel(true)
			.SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
		t.TweenProperty(_panel, "modulate:a", 1f, 0.22f);
		t.TweenProperty(_dim, "modulate:a", 1f, 0.22f);
	}

	private void BuildUI()
	{
		_dim = new ColorRect { Color = new Color(0, 0, 0, 0.7f) };
		_dim.MouseFilter = MouseFilterEnum.Stop;
		AddChild(_dim);
		UIStyle.FillParent(_dim);

		_panel = new PanelContainer();
		_panel.AddThemeStyleboxOverride("panel", UIStyle.PanelStyle());
		AddChild(_panel);
		UIStyle.FillParent(_panel, marginX: 40, marginY: 30);

		var v = new VBoxContainer();
		v.AddThemeConstantOverride("separation", 10);
		_panel.AddChild(v);

		// === Title ===
		var titleRow = new HBoxContainer();
		titleRow.AddThemeConstantOverride("separation", 8);
		v.AddChild(titleRow);

		var leftSpacer = new Control { CustomMinimumSize = new Vector2(44, 0) };
		titleRow.AddChild(leftSpacer);

		var title = UIStyle.MakeLabel("🛠 Мастерская", 22, UIStyle.GoldBright);
		title.HorizontalAlignment = HorizontalAlignment.Center;
		title.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		titleRow.AddChild(title);

		var xBtn = new Button { Text = "✕" };
		UIStyle.StyleButton(xBtn);
		xBtn.CustomMinimumSize = new Vector2(44, 44);
		xBtn.Pressed += Close;
		titleRow.AddChild(xBtn);

		_resourcesLabel = UIStyle.MakeLabel("", 13, UIStyle.TextSecondary);
		_resourcesLabel.HorizontalAlignment = HorizontalAlignment.Center;
		_resourcesLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		v.AddChild(_resourcesLabel);

		_skillSummary = UIStyle.MakeLabel("", 12, UIStyle.TextDim);
		_skillSummary.HorizontalAlignment = HorizontalAlignment.Center;
		_skillSummary.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		v.AddChild(_skillSummary);

		v.AddChild(new HSeparator());

		// === Grade tabs ===
		var tabs = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
		tabs.AddThemeConstantOverride("separation", 8);
		v.AddChild(tabs);

		_gradeEBtn = new Button { Text = "Грейд E" };
		UIStyle.StyleButton(_gradeEBtn, primary: true);
		_gradeEBtn.CustomMinimumSize = new Vector2(140, 36);
		_gradeEBtn.Pressed += () => { _selectedGrade = "E"; Refresh(); };
		tabs.AddChild(_gradeEBtn);

		_gradeDBtn = new Button { Text = "Грейд D" };
		UIStyle.StyleButton(_gradeDBtn);
		_gradeDBtn.CustomMinimumSize = new Vector2(140, 36);
		_gradeDBtn.Pressed += () => { _selectedGrade = "D"; Refresh(); };
		tabs.AddChild(_gradeDBtn);

		// === Scroll with recipe list ===
		var scroll = new ScrollContainer();
		scroll.SizeFlagsVertical = SizeFlags.ExpandFill;
		scroll.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;
		v.AddChild(scroll);

		_recipeList = new VBoxContainer();
		_recipeList.AddThemeConstantOverride("separation", 6);
		_recipeList.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		scroll.AddChild(_recipeList);

		_statusLabel = UIStyle.MakeLabel("", 13, UIStyle.WarnAmber);
		_statusLabel.HorizontalAlignment = HorizontalAlignment.Center;
		v.AddChild(_statusLabel);

		var btnRow = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
		v.AddChild(btnRow);

		var closeBtn = new Button { Text = "Закрыть" };
		UIStyle.StyleButton(closeBtn, primary: true);
		closeBtn.CustomMinimumSize = new Vector2(180, 44);
		closeBtn.Pressed += Close;
		btnRow.AddChild(closeBtn);
	}

	private void Refresh()
	{
		var ch = GameData.Instance.Character;
		if (ch == null) return;

		// Подсветить активную вкладку.
		UIStyle.StyleButton(_gradeEBtn, primary: _selectedGrade == "E");
		UIStyle.StyleButton(_gradeDBtn, primary: _selectedGrade == "D");

		_resourcesLabel.Text = BuildResourcesLine(ch);
		_skillSummary.Text = BuildSkillSummaryLine(ch);

		ClearChildren(_recipeList);
		int shown = 0;
		foreach (var r in CraftingDB.RecipesByGrade(_selectedGrade))
		{
			_recipeList.AddChild(MakeRecipeRow(ch, r));
			shown++;
		}
		if (shown == 0)
			_recipeList.AddChild(UIStyle.MakeLabel(
				"Нет рецептов на этом грейде.", 13, UIStyle.TextDim));
	}

	private static string BuildResourcesLine(CharacterData ch)
	{
		var parts = new List<string>();
		foreach (var res in ResourcesDB.AllResources())
		{
			int n = ch.Inventory.CountOf(res.Id);
			if (n <= 0) continue;
			parts.Add($"{res.Icon}{res.Name} ({res.Grade}) ×{n}");
		}
		if (parts.Count == 0) return "Ресурсов в инвентаре нет — добудьте материалы в подземелье.";
		return string.Join("   ", parts);
	}

	private static string BuildSkillSummaryLine(CharacterData ch)
	{
		var parts = new List<string>();
		foreach (var skillId in CraftingDB.AllSkillIds())
		{
			int xp = ch.Crafting?.GetXp(skillId) ?? 0;
			int lvl = CraftingDB.LevelFromXp(xp);
			if (lvl == 0 && xp == 0) continue;     // не качали — не засоряем.
			parts.Add($"{CraftingDB.SkillDisplayName(skillId)}: ур. {lvl} ({xp} XP)");
		}
		if (parts.Count == 0) return "Все скиллы крафта на 0 уровне — крафт E/D даст первый опыт.";
		return string.Join("   ·   ", parts);
	}

	// === Recipe row ===

	private Control MakeRecipeRow(CharacterData ch, CraftingDB.Recipe r)
	{
		var panel = new PanelContainer();
		panel.AddThemeStyleboxOverride("panel", UIStyle.MiniPanelStyle());
		var col = new VBoxContainer();
		col.AddThemeConstantOverride("separation", 4);
		panel.AddChild(col);

		// Заголовок: имя предмета + грейд/tier.
		var header = new HBoxContainer();
		header.AddThemeConstantOverride("separation", 8);
		col.AddChild(header);

		string itemName = ResolveItemName(r.ResultItemId);
		var nameL = UIStyle.MakeLabel(itemName, 14, UIStyle.TextPrimary);
		nameL.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		header.AddChild(nameL);

		var tagL = UIStyle.MakeLabel($"{r.Grade}-{r.Tier}", 11, UIStyle.GoldBright);
		header.AddChild(tagL);

		// Скилл + текущий уровень.
		int skillXp = ch.Crafting?.GetXp(r.SkillId) ?? 0;
		int skillLvl = CraftingDB.LevelFromXp(skillXp);
		int minLvl = CraftingDB.MinLevelForGrade(r.Grade);
		var skillL = UIStyle.MakeLabel(
			$"{CraftingDB.SkillDisplayName(r.SkillId)} — ур. {skillLvl}" +
			(minLvl > skillLvl ? $" (нужно {minLvl})" : ""),
			11, skillLvl >= minLvl ? UIStyle.TextSecondary : UIStyle.WarnAmber);
		col.AddChild(skillL);

		// Ингредиенты.
		var ingRow = new HBoxContainer();
		ingRow.AddThemeConstantOverride("separation", 12);
		col.AddChild(ingRow);

		bool haveAll = true;
		foreach (var ing in r.Ingredients)
		{
			int have = ch.Inventory.CountOf(ing.ResourceId);
			bool ok = have >= ing.Count;
			if (!ok) haveAll = false;
			var resData = ResourcesDB.Get(ing.ResourceId);
			string label = resData != null
				? $"{resData.Icon} {resData.Name} {have}/{ing.Count}"
				: $"{ing.ResourceId} {have}/{ing.Count}";
			var l = UIStyle.MakeLabel(label, 12,
				ok ? UIStyle.TextSecondary : UIStyle.DangerRed);
			ingRow.AddChild(l);
		}

		// Кнопка крафта.
		var actions = new HBoxContainer();
		actions.AddThemeConstantOverride("separation", 6);
		col.AddChild(actions);

		bool unlocked = skillLvl >= minLvl;
		int gainXp = CraftingDB.CraftXp(r.Grade, r.Tier);
		var craftBtn = new Button
		{
			Text = unlocked
				? (haveAll ? $"🛠 Скрафтить (+{gainXp} XP)" : "🛠 Не хватает ресурсов")
				: $"🔒 Нужен ур. {minLvl}",
			Disabled = !unlocked || !haveAll,
		};
		UIStyle.StyleButton(craftBtn, primary: unlocked && haveAll);
		craftBtn.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		if (unlocked && haveAll)
			craftBtn.Pressed += () => OnCraftPressed(r.ResultItemId);
		actions.AddChild(craftBtn);

		return panel;
	}

	private static string ResolveItemName(string itemId)
	{
		var w = ItemsDB.GetWeapon(itemId);
		if (w != null) return w.Name;
		var a = ItemsDB.GetArmor(itemId);
		if (a != null) return a.Name;
		return itemId;
	}

	private async void OnCraftPressed(string itemId)
	{
		var outcome = await GameData.Instance.CraftItemAsync(itemId);
		if (!outcome.Ok)
		{
			SetStatus(TranslateError(outcome.Error, "Не удалось скрафтить."), error: true);
			Refresh();
			return;
		}
		SetStatus($"Готово! +{outcome.Value} XP в скилл крафта.", error: false);
		Refresh();
	}

	private static string TranslateError(string code, string fallback) => code switch
	{
		"no_recipe"        => "Этот предмет нельзя скрафтить.",
		"low_skill"        => "Уровень скилла слишком низкий для этого грейда.",
		"no_resources"     => "Не хватает ресурсов.",
		"no_space"         => "Нет свободного места в инвентаре.",
		"locked_in_run"    => "Нельзя в подземелье — выйдите в город.",
		"locked_in_battle" => "Нельзя во время боя.",
		"network_error"    => "Нет связи с сервером.",
		_                  => fallback,
	};

	private void SetStatus(string text, bool error)
	{
		_statusLabel.Text = text;
		_statusLabel.AddThemeColorOverride("font_color",
			error ? UIStyle.DangerRed : UIStyle.HealGreen);
	}

	private static void ClearChildren(Node container)
	{
		foreach (Node child in container.GetChildren())
		{
			container.RemoveChild(child);
			child.QueueFree();
		}
	}
}

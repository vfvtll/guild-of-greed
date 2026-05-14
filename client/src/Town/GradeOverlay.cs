using Godot;
using GuildOfGreed.Shared.Domain;

// Гильдия — окно повышения грейда. Dev-режим: бесплатно, без требований к
// уровню или ресурсам. Показывает текущий грейд/уровень внутри грейда и
// сквозной отображаемый уровень (DisplayLevel). Кнопка "Повысить грейд"
// шлёт PromoteGrade на сервер. На S-грейде кнопка задизейблена.
public partial class GradeOverlay : Control
{
	[Signal] public delegate void ClosedEventHandler();

	private PanelContainer _panel;
	private ColorRect _dim;
	private Label _currentGradeLabel;
	private Label _levelLabel;
	private Label _xpLabel;
	private Label _ladderLabel;
	private Label _statusLabel;
	private Button _promoteBtn;

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
		UIStyle.FillParent(_panel, marginX: 80, marginY: 60);

		var v = new VBoxContainer();
		v.AddThemeConstantOverride("separation", 12);
		_panel.AddChild(v);

		// === Title ===
		var titleRow = new HBoxContainer();
		titleRow.AddThemeConstantOverride("separation", 8);
		v.AddChild(titleRow);

		var leftSpacer = new Control { CustomMinimumSize = new Vector2(44, 0) };
		titleRow.AddChild(leftSpacer);

		var title = UIStyle.MakeLabel("🛡 Гильдия авантюристов", 22, UIStyle.GoldBright);
		title.HorizontalAlignment = HorizontalAlignment.Center;
		title.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		titleRow.AddChild(title);

		var xBtn = new Button { Text = "✕" };
		UIStyle.StyleButton(xBtn);
		xBtn.CustomMinimumSize = new Vector2(44, 44);
		xBtn.Pressed += Close;
		titleRow.AddChild(xBtn);

		var sub = UIStyle.MakeLabel(
			"В Гильдии оценивают силу авантюриста и выдают новый ранг.\n" +
			"Каждый грейд даёт " + CharacterData.LevelsPerGrade + " уровней. Дальше нужен следующий ранг.",
			13, UIStyle.TextSecondary);
		sub.HorizontalAlignment = HorizontalAlignment.Center;
		sub.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		v.AddChild(sub);

		v.AddChild(new HSeparator());

		// === Текущий грейд (большая плашка) ===
		_currentGradeLabel = UIStyle.MakeLabel("", 48, UIStyle.GoldBright);
		_currentGradeLabel.HorizontalAlignment = HorizontalAlignment.Center;
		v.AddChild(_currentGradeLabel);

		_levelLabel = UIStyle.MakeLabel("", 16, UIStyle.TextPrimary);
		_levelLabel.HorizontalAlignment = HorizontalAlignment.Center;
		v.AddChild(_levelLabel);

		_xpLabel = UIStyle.MakeLabel("", 13, UIStyle.TextDim);
		_xpLabel.HorizontalAlignment = HorizontalAlignment.Center;
		v.AddChild(_xpLabel);

		v.AddChild(new HSeparator());

		// === Лестница грейдов ===
		_ladderLabel = UIStyle.MakeLabel("", 14, UIStyle.TextSecondary);
		_ladderLabel.HorizontalAlignment = HorizontalAlignment.Center;
		v.AddChild(_ladderLabel);

		// === Кнопка промоушна ===
		var btnRow = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
		btnRow.AddThemeConstantOverride("separation", 16);
		v.AddChild(btnRow);

		_promoteBtn = new Button();
		UIStyle.StyleButton(_promoteBtn, primary: true);
		_promoteBtn.CustomMinimumSize = new Vector2(240, 48);
		_promoteBtn.Pressed += OnPromotePressed;
		btnRow.AddChild(_promoteBtn);

		_statusLabel = UIStyle.MakeLabel("", 13, UIStyle.WarnAmber);
		_statusLabel.HorizontalAlignment = HorizontalAlignment.Center;
		v.AddChild(_statusLabel);

		var closeRow = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
		v.AddChild(closeRow);
		var closeBtn = new Button { Text = "Закрыть" };
		UIStyle.StyleButton(closeBtn);
		closeBtn.CustomMinimumSize = new Vector2(180, 40);
		closeBtn.Pressed += Close;
		closeRow.AddChild(closeBtn);
	}

	private void Refresh()
	{
		var ch = GameData.Instance.Character;
		if (ch == null) return;

		_currentGradeLabel.Text = $"Грейд {ch.Grade}";
		_levelLabel.Text =
			$"Уровень {ch.Level}   ·   в грейде {ch.LevelWithinGrade()}/{CharacterData.LevelsPerGrade}";
		_xpLabel.Text = $"Опыт: {ch.Exp} / {ch.XpForNextCharacterLevel()}";

		_ladderLabel.Text = BuildLadder(ch.Grade);

		bool canPromote = ch.CanPromoteGrade();
		if (canPromote)
		{
			string next = NextGradeOf(ch.Grade);
			_promoteBtn.Text = $"Повысить до грейда {next}";
			_promoteBtn.Disabled = false;
		}
		else
		{
			_promoteBtn.Text = "Достигнут максимальный грейд";
			_promoteBtn.Disabled = true;
		}
	}

	private static string BuildLadder(string current)
	{
		string[] grades = { "E", "D", "C", "B", "A", "S" };
		var sb = new System.Text.StringBuilder();
		for (int i = 0; i < grades.Length; i++)
		{
			if (i > 0) sb.Append("  →  ");
			sb.Append(grades[i] == current ? $"[ {grades[i]} ]" : grades[i]);
		}
		return sb.ToString();
	}

	private static string NextGradeOf(string current) => current switch
	{
		"E" => "D",
		"D" => "C",
		"C" => "B",
		"B" => "A",
		"A" => "S",
		_   => current,
	};

	private async void OnPromotePressed()
	{
		_promoteBtn.Disabled = true;
		SetStatus("", error: false);
		var outcome = await GameData.Instance.PromoteGradeAsync();
		if (!outcome.Ok)
		{
			SetStatus(TranslateError(outcome.Error, "Не удалось повысить грейд."), error: true);
			Refresh();
			return;
		}
		var ch = GameData.Instance.Character;
		SetStatus($"Грейд повышен до {ch.Grade}. Удачи, авантюрист.", error: false);
		Refresh();
	}

	private static string TranslateError(string code, string fallback) => code switch
	{
		"cant_promote"     => "Грейд уже максимальный.",
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
}

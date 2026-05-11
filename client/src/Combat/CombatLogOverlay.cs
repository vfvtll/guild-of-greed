using Godot;
using GuildOfGreed.Shared.Domain;
using GuildOfGreed.Shared.Data;

// Модальный оверлей боевого лога. Открывается из Combat по кнопке 📜.
// Combat хранит лог-буфер (List<string>); при открытии вкачивает всё в RichTextLabel.
// Если лог дополняется во время открытого оверлея — Combat вызывает AppendLine.
public partial class CombatLogOverlay : Control
{
	[Signal]
	public delegate void ClosedEventHandler();

	private RichTextLabel _logText;

	// Для slide-in/out анимаций.
	private PanelContainer _panel;
	private ColorRect _dim;
	private Vector2 _panelTargetPos;

	public override void _Ready()
	{
		SetAnchorsPreset(LayoutPreset.FullRect);
		MouseFilter = MouseFilterEnum.Stop;
		BuildUI();
		PlayOpenAnimation();
	}

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

	// Полная замена содержимого лога (вызывается при открытии).
	public void SetContent(string bbcodeContent)
	{
		if (_logText == null) return;
		_logText.Clear();
		_logText.AppendText(bbcodeContent);
	}

	// Дописать одну строку (для live-обновления когда оверлей открыт).
	public void AppendLine(string bbcodeLine)
	{
		_logText?.AppendText(bbcodeLine + "\n");
	}

	private void BuildUI()
	{
		_dim = new ColorRect { Color = new Color(0, 0, 0, 0.7f) };
		_dim.SetAnchorsPreset(LayoutPreset.FullRect);
		_dim.MouseFilter = MouseFilterEnum.Stop;
		AddChild(_dim);

		_panel = new PanelContainer
		{
			Position = new Vector2(140, 50),
			Size = new Vector2(1000, 620),
			CustomMinimumSize = new Vector2(1000, 620),
		};
		_panel.AddThemeStyleboxOverride("panel", UIStyle.PanelStyle());
		AddChild(_panel);

		var v = new VBoxContainer();
		v.AddThemeConstantOverride("separation", 12);
		_panel.AddChild(v);

		// Шапка: спейсер | заголовок | ✕
		var titleRow = new HBoxContainer();
		titleRow.AddThemeConstantOverride("separation", 8);
		v.AddChild(titleRow);

		var leftSpacer = new Control { CustomMinimumSize = new Vector2(44, 0) };
		titleRow.AddChild(leftSpacer);

		var title = UIStyle.MakeLabel("📜 Лог боя", 22, UIStyle.GoldBright);
		title.HorizontalAlignment = HorizontalAlignment.Center;
		title.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		titleRow.AddChild(title);

		var xBtn = new Button { Text = "✕" };
		UIStyle.StyleButton(xBtn);
		xBtn.CustomMinimumSize = new Vector2(44, 44);
		xBtn.TooltipText = "Закрыть лог";
		xBtn.Pressed += Close;
		titleRow.AddChild(xBtn);

		var sep = new HSeparator();
		var sepStyle = new StyleBoxFlat
		{
			BgColor = UIStyle.GoldDark,
			ContentMarginTop = 1, ContentMarginBottom = 1,
		};
		sep.AddThemeStyleboxOverride("separator", sepStyle);
		v.AddChild(sep);

		_logText = new RichTextLabel
		{
			BbcodeEnabled = true,
			ScrollFollowing = true,
		};
		_logText.SizeFlagsVertical = SizeFlags.ExpandFill;
		_logText.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		v.AddChild(_logText);

		var btnRow = new HBoxContainer();
		btnRow.Alignment = BoxContainer.AlignmentMode.Center;
		v.AddChild(btnRow);

		var closeBtn = new Button { Text = "Закрыть" };
		UIStyle.StyleButton(closeBtn, primary: true);
		closeBtn.CustomMinimumSize = new Vector2(180, 44);
		closeBtn.Pressed += Close;
		btnRow.AddChild(closeBtn);
	}
}

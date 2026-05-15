using Godot;

// Главное меню — первый экран после запуска. До нажатия "Играть"
// сетевое соединение не открывается; так пользователь сразу видит брендинг,
// а не loading-спиннер.
public partial class TitleView : Control
{
	[Signal] public delegate void PlayRequestedEventHandler();
	[Signal] public delegate void SettingsRequestedEventHandler();
	[Signal] public delegate void QuitRequestedEventHandler();

	public override void _Ready()
	{
		UIStyle.FillParent(this);
		BuildUI();
	}

	private void BuildUI()
	{
		// Фон: Carl Probst, "Vor dem Kamin" (1905), Public Domain.
		// См. client/assets/CREDITS.md.
		var bg = new TextureRect
		{
			Texture = GD.Load<Texture2D>("res://assets/title_bg.jpg"),
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
		};
		UIStyle.FillParent(bg);
		bg.MouseFilter = MouseFilterEnum.Ignore;
		AddChild(bg);

		// Затемняющий слой поверх фона: панель с кнопками должна оставаться
		// читабельной даже когда картинка яркая.
		var dim = new ColorRect { Color = new Color(0, 0, 0, 0.45f) };
		UIStyle.FillParent(dim);
		dim.MouseFilter = MouseFilterEnum.Ignore;
		AddChild(dim);

		var center = new CenterContainer();
		UIStyle.FillParent(center);
		center.MouseFilter = MouseFilterEnum.Ignore;
		AddChild(center);

		var panel = new PanelContainer();
		panel.AddThemeStyleboxOverride("panel", UIStyle.PanelStyle());
		panel.CustomMinimumSize = new Vector2(460, 0);
		center.AddChild(panel);

		var v = new VBoxContainer();
		v.AddThemeConstantOverride("separation", 18);
		panel.AddChild(v);

		var title = UIStyle.MakeLabel("⚔  GUILD OF GREED", 36, UIStyle.GoldBright);
		title.HorizontalAlignment = HorizontalAlignment.Center;
		v.AddChild(title);

		var subtitle = UIStyle.MakeLabel("Гильдия алчности", 16, UIStyle.TextSecondary);
		subtitle.HorizontalAlignment = HorizontalAlignment.Center;
		v.AddChild(subtitle);

		v.AddChild(new HSeparator());

		var playBtn = new Button { Text = "Играть" };
		UIStyle.StyleButton(playBtn, primary: true);
		playBtn.CustomMinimumSize = new Vector2(0, 48);
		playBtn.Pressed += () => EmitSignal(SignalName.PlayRequested);
		v.AddChild(playBtn);

		var settingsBtn = new Button { Text = "Настройки" };
		UIStyle.StyleButton(settingsBtn);
		settingsBtn.Disabled = true;
		settingsBtn.Pressed += () => EmitSignal(SignalName.SettingsRequested);
		v.AddChild(settingsBtn);

		var quitBtn = new Button { Text = "Выход" };
		UIStyle.StyleButton(quitBtn);
		quitBtn.Pressed += () => EmitSignal(SignalName.QuitRequested);
		v.AddChild(quitBtn);

		var version = UIStyle.MakeLabel(
			$"v0.1 · protocol {GuildOfGreed.Shared.Net.ProtocolVersion.Current}",
			11, UIStyle.TextDim);
		version.HorizontalAlignment = HorizontalAlignment.Center;
		v.AddChild(version);
	}
}

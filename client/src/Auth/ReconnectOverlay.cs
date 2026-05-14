using Godot;

// Полупрозрачный overlay поверх любого экрана при потере соединения.
// Блокирует input (MouseFilter=Stop), показывает статус и кнопку Повторить
// при сбое восстановления. Main управляет состоянием через SetStatus/SetError.
public partial class ReconnectOverlay : Control
{
	[Signal] public delegate void RetryRequestedEventHandler();

	private Label _statusLabel;
	private Button _retryBtn;

	public override void _Ready()
	{
		UIStyle.FillParent(this);
		MouseFilter = MouseFilterEnum.Stop;
		BuildUI();
	}

	private void BuildUI()
	{
		// Полупрозрачная заслонка перекрывает экран под собой.
		var dim = new ColorRect { Color = new Color(0, 0, 0, 0.7f) };
		dim.MouseFilter = MouseFilterEnum.Stop;
		AddChild(dim);
		UIStyle.FillParent(dim);

		// Узкий центрированный popup — анкоримся в центр экрана, чтобы он не
		// уезжал в угол на нестандартных viewport.
		var panel = new PanelContainer { CustomMinimumSize = new Vector2(420, 180) };
		panel.AddThemeStyleboxOverride("panel", UIStyle.PanelStyle());
		AddChild(panel);
		panel.SetAnchorsPreset(LayoutPreset.Center);
		panel.OffsetLeft = -210;
		panel.OffsetRight = 210;
		panel.OffsetTop = -90;
		panel.OffsetBottom = 90;

		var v = new VBoxContainer();
		v.AddThemeConstantOverride("separation", 12);
		panel.AddChild(v);

		var title = UIStyle.MakeLabel("⚡ Соединение с сервером", 20, UIStyle.GoldBright);
		title.HorizontalAlignment = HorizontalAlignment.Center;
		v.AddChild(title);

		_statusLabel = UIStyle.MakeLabel("Восстановление...", 14, UIStyle.TextSecondary);
		_statusLabel.HorizontalAlignment = HorizontalAlignment.Center;
		_statusLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		_statusLabel.CustomMinimumSize = new Vector2(370, 0);
		v.AddChild(_statusLabel);

		_retryBtn = new Button { Text = "Повторить" };
		UIStyle.StyleButton(_retryBtn, primary: true);
		_retryBtn.Visible = false;
		_retryBtn.Pressed += () => EmitSignal(SignalName.RetryRequested);
		v.AddChild(_retryBtn);
	}

	public void SetStatus(string text)
	{
		_statusLabel.Text = text;
		_retryBtn.Visible = false;
	}

	public void SetError(string text)
	{
		_statusLabel.Text = text;
		_retryBtn.Visible = true;
	}
}

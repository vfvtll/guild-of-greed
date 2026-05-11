using System;
using Godot;

// Простой loading-экран на время connect+handshake. Показывает текущее
// действие, сообщение об ошибке и кнопку «Повторить» — когда фоновый Task
// упал и Main просит юзера нажать ещё раз.
public partial class ConnectingView : Control
{
	[Signal] public delegate void RetryRequestedEventHandler();

	private Label _statusLabel;
	private Button _retryBtn;

	public override void _Ready()
	{
		SetAnchorsPreset(LayoutPreset.FullRect);
		BuildUI();
	}

	private void BuildUI()
	{
		var bg = new ColorRect { Color = UIStyle.BgDeep };
		bg.SetAnchorsPreset(LayoutPreset.FullRect);
		bg.MouseFilter = MouseFilterEnum.Ignore;
		AddChild(bg);

		var v = new VBoxContainer
		{
			Position = new Vector2(440, 280),
			CustomMinimumSize = new Vector2(400, 0),
		};
		v.AddThemeConstantOverride("separation", 14);
		AddChild(v);

		var title = UIStyle.MakeLabel("⚔  GUILD OF GREED", 28, UIStyle.GoldBright);
		title.HorizontalAlignment = HorizontalAlignment.Center;
		v.AddChild(title);

		_statusLabel = UIStyle.MakeLabel("Соединение с сервером...", 14, UIStyle.TextSecondary);
		_statusLabel.HorizontalAlignment = HorizontalAlignment.Center;
		_statusLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
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

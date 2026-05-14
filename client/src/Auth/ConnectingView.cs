using System;
using Godot;

// Простой loading-экран на время connect+handshake. Показывает текущее
// действие, сообщение об ошибке и кнопку «Повторить» — когда фоновый Task
// упал и Main просит юзера нажать ещё раз.
//
// Особый случай: TLS pin mismatch (фингерпринт серверного cert изменился).
// Кнопка "Повторить" не помогает — нужно явно сбросить доверие к серверу
// через ServerTrustStore.Clear. В этом случае показываем выделенную
// "Сбросить доверие к серверу" — после неё следующий connect TOFU-пиннит заново.
public partial class ConnectingView : Control
{
	[Signal] public delegate void RetryRequestedEventHandler();

	private Label _statusLabel;
	private Button _retryBtn;
	private Button _resetTrustBtn;
	private string _resetHost;
	private int _resetPort;

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

		_resetTrustBtn = new Button { Text = "Сбросить доверие к серверу и повторить" };
		UIStyle.StyleButton(_resetTrustBtn);
		_resetTrustBtn.Visible = false;
		_resetTrustBtn.Pressed += OnResetTrustPressed;
		v.AddChild(_resetTrustBtn);
	}

	public void SetStatus(string text)
	{
		_statusLabel.Text = text;
		_retryBtn.Visible = false;
		_resetTrustBtn.Visible = false;
	}

	public void SetError(string text)
	{
		_statusLabel.Text = text;
		_retryBtn.Visible = true;
		_resetTrustBtn.Visible = false;
	}

	public void SetPinMismatch(string host, int port)
	{
		_resetHost = host;
		_resetPort = port;
		_statusLabel.Text =
			$"Сертификат сервера {host}:{port} изменился.\n" +
			"Это может означать: сервер перевыпустил cert, либо MITM-атака.\n" +
			"Сбросьте доверие только если ожидаете изменения сертификата.";
		_retryBtn.Visible = false;
		_resetTrustBtn.Visible = true;
	}

	private void OnResetTrustPressed()
	{
		if (!string.IsNullOrEmpty(_resetHost))
		{
			ServerTrustStore.Clear(_resetHost, _resetPort);
			GD.Print($"ConnectingView: trust cleared for {_resetHost}:{_resetPort}");
		}
		EmitSignal(SignalName.RetryRequested);
	}
}

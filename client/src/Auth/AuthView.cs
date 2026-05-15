using System;
using System.Threading.Tasks;
using Godot;
using GuildOfGreed.Shared.Net;

// Экран авторизации. Две формы: Login и Register, переключаются кнопками.
// При успехе шлёт сигнал AuthSucceeded(token, accountId, login) — Main роутит
// дальше на CharacterSelectView.
//
// Все запросы блокируют форму (показывается _statusLabel = "Загрузка..."),
// чтобы пользователь не спамил кнопки. ServerException мапится на русский
// текст по Error-коду (см. ErrorMessage).
public partial class AuthView : Control
{
	[Signal] public delegate void AuthSucceededEventHandler(string token, string accountId, string login);

	private enum Mode { Login, Register }

	private readonly NetworkClient _net;
	private Mode _mode = Mode.Login;

	private Button _loginTab, _registerTab, _submit;
	private LineEdit _loginEdit, _emailEdit, _passwordEdit;
	private Label _emailLabel, _statusLabel;
	private Control _emailRow;

	public AuthView(NetworkClient net)
	{
		_net = net;
	}

	public override void _Ready()
	{
		UIStyle.FillParent(this);
		BuildUI();
		ApplyMode();
	}

	private void BuildUI()
	{
		var bg = new ColorRect { Color = UIStyle.BgDeep };
		UIStyle.FillParent(bg);
		bg.MouseFilter = MouseFilterEnum.Ignore;
		AddChild(bg);

		var title = UIStyle.MakeLabel("⚔  GUILD OF GREED", 36, UIStyle.GoldBright);
		title.Position = new Vector2(420, 60);
		AddChild(title);

		var sub = UIStyle.MakeLabel("Войдите или создайте аккаунт", 14, UIStyle.TextSecondary);
		sub.Position = new Vector2(440, 110);
		AddChild(sub);

		// === Форма по центру ===
		var form = new PanelContainer
		{
			Position = new Vector2(390, 170),
			CustomMinimumSize = new Vector2(500, 400),
		};
		form.AddThemeStyleboxOverride("panel", UIStyle.PanelStyle());
		AddChild(form);

		var v = new VBoxContainer();
		v.AddThemeConstantOverride("separation", 14);
		form.AddChild(v);

		// Tabs
		var tabs = new HBoxContainer();
		tabs.AddThemeConstantOverride("separation", 8);
		tabs.Alignment = BoxContainer.AlignmentMode.Center;
		v.AddChild(tabs);

		_loginTab = new Button { Text = "Войти" };
		UIStyle.StyleButton(_loginTab, primary: true);
		_loginTab.Pressed += () => SetMode(Mode.Login);
		tabs.AddChild(_loginTab);

		_registerTab = new Button { Text = "Создать аккаунт" };
		UIStyle.StyleButton(_registerTab);
		_registerTab.Pressed += () => SetMode(Mode.Register);
		tabs.AddChild(_registerTab);

		v.AddChild(MakeSeparator());

		// Login row
		v.AddChild(UIStyle.MakeLabel("Логин", 13, UIStyle.TextPrimary));
		_loginEdit = MakeLineEdit("3-24 символа");
		v.AddChild(_loginEdit);

		// Email row (только для Register)
		_emailRow = new VBoxContainer();
		_emailRow.AddThemeConstantOverride("separation", 4);
		_emailLabel = UIStyle.MakeLabel("Email", 13, UIStyle.TextPrimary);
		_emailRow.AddChild(_emailLabel);
		_emailEdit = MakeLineEdit("name@example.com");
		_emailRow.AddChild(_emailEdit);
		v.AddChild(_emailRow);

		// Password row
		v.AddChild(UIStyle.MakeLabel("Пароль", 13, UIStyle.TextPrimary));
		_passwordEdit = MakeLineEdit("минимум 6 символов");
		_passwordEdit.Secret = true;
		v.AddChild(_passwordEdit);

		// Submit
		_submit = new Button { Text = "Войти" };
		UIStyle.StyleButton(_submit, primary: true);
		_submit.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		_submit.Pressed += OnSubmitPressed;
		v.AddChild(_submit);

		// Status
		_statusLabel = UIStyle.MakeLabel("", 12, UIStyle.WarnAmber);
		_statusLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		_statusLabel.CustomMinimumSize = new Vector2(0, 40);
		v.AddChild(_statusLabel);
	}

	private LineEdit MakeLineEdit(string placeholder)
	{
		var e = new LineEdit
		{
			PlaceholderText = placeholder,
			CustomMinimumSize = new Vector2(0, 36),
		};
		return e;
	}

	private HSeparator MakeSeparator()
	{
		var sep = new HSeparator();
		var sb = new StyleBoxFlat
		{
			BgColor = UIStyle.GoldDark,
			ContentMarginTop = 1, ContentMarginBottom = 1,
		};
		sep.AddThemeStyleboxOverride("separator", sb);
		return sep;
	}

	private void SetMode(Mode mode)
	{
		_mode = mode;
		_statusLabel.Text = "";
		ApplyMode();
	}

	private void ApplyMode()
	{
		_emailRow.Visible = _mode == Mode.Register;
		_submit.Text = _mode == Mode.Login ? "Войти" : "Создать аккаунт";
		UIStyle.StyleButton(_loginTab, primary: _mode == Mode.Login);
		UIStyle.StyleButton(_registerTab, primary: _mode == Mode.Register);
	}

	private async void OnSubmitPressed()
	{
		string login = _loginEdit.Text?.Trim() ?? "";
		string email = _emailEdit.Text?.Trim() ?? "";
		string password = _passwordEdit.Text ?? "";

		if (login.Length < 3) { Status("Логин слишком короткий (минимум 3 символа)."); return; }
		if (password.Length < 6) { Status("Пароль слишком короткий (минимум 6 символов)."); return; }
		if (_mode == Mode.Register && (string.IsNullOrEmpty(email) || !email.Contains('@')))
		{ Status("Укажите корректный email."); return; }

		SetBusy(true);
		Status(_mode == Mode.Login ? "Вход..." : "Создание аккаунта...");
		try
		{
			if (_mode == Mode.Login) await DoLogin(login, password);
			else                     await DoRegister(login, email, password);
		}
		catch (ServerException ex)
		{
			Status(ErrorMessage(ex.Code));
		}
		catch (Exception ex)
		{
			Status($"Ошибка соединения: {ex.Message}");
		}
		finally
		{
			SetBusy(false);
		}
	}

	private async Task DoLogin(string login, string password)
	{
		var resp = await _net.LoginAsync(login, password);
		if (!resp.Success) { Status(ErrorMessage(resp.Error)); return; }
		EmitSignal(SignalName.AuthSucceeded, resp.Token, resp.AccountId.ToString(), login);
	}

	private async Task DoRegister(string login, string email, string password)
	{
		var resp = await _net.RegisterAsync(login, email, password);
		if (!resp.Success) { Status(ErrorMessage(resp.Error)); return; }
		EmitSignal(SignalName.AuthSucceeded, resp.Token, resp.AccountId.ToString(), login);
	}

	private void SetBusy(bool busy)
	{
		_submit.Disabled = busy;
		_loginTab.Disabled = busy;
		_registerTab.Disabled = busy;
		_loginEdit.Editable = !busy;
		_emailEdit.Editable = !busy;
		_passwordEdit.Editable = !busy;
	}

	private void Status(string text)
	{
		_statusLabel.Text = text;
	}

	private static string ErrorMessage(string code) => code switch
	{
		"login_taken"          => "Этот логин уже занят.",
		"email_taken"          => "Этот email уже зарегистрирован.",
		"weak_password"        => "Пароль слишком короткий.",
		"invalid_login"        => "Неверный формат логина (3-24 символа).",
		"invalid_email"        => "Неверный формат email.",
		"invalid_credentials"  => "Неверный логин или пароль.",
		"rate_limited"         => "Слишком много попыток. Подождите минуту.",
		_                      => $"Ошибка: {code}",
	};
}

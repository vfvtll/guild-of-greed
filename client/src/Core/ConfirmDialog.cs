using Godot;

// Универсальный confirm-диалог. Перекрывает родителя полупрозрачным фоном,
// в центре панель с заголовком, телом и двумя кнопками.
//
// Использование:
//   var d = new ConfirmDialog { Title = "Удалить?", Body = "Действие необратимо",
//                                ConfirmText = "Удалить", CancelText = "Отмена" };
//   d.Confirmed += () => DoTheThing();
//   AddChild(d);
public partial class ConfirmDialog : Control
{
	[Signal] public delegate void ConfirmedEventHandler();
	[Signal] public delegate void CancelledEventHandler();

	public string Title { get; set; } = "Подтверждение";
	public string Body { get; set; } = "";
	public string ConfirmText { get; set; } = "OK";
	public string CancelText { get; set; } = "Отмена";

	// Если задано — кнопка подтверждения disabled пока в LineEdit не введут
	// эту строку. Используется для удаления персонажа (вводят имя для подтверждения).
	public string RequireTypedConfirmation { get; set; }

	private Button _confirmBtn;

	public override void _Ready()
	{
		UIStyle.FillParent(this);
		// Перехватываем клики, чтобы фоновые контролы не реагировали.
		MouseFilter = MouseFilterEnum.Stop;
		BuildUI();
	}

	private void BuildUI()
	{
		// Полупрозрачный затемняющий фон.
		var dim = new ColorRect { Color = new Color(0, 0, 0, 0.65f) };
		UIStyle.FillParent(dim);
		dim.MouseFilter = MouseFilterEnum.Stop;
		AddChild(dim);

		var center = new CenterContainer();
		UIStyle.FillParent(center);
		center.MouseFilter = MouseFilterEnum.Ignore;
		AddChild(center);

		var panel = new PanelContainer();
		panel.AddThemeStyleboxOverride("panel", UIStyle.PanelStyle());
		panel.CustomMinimumSize = new Vector2(420, 0);
		center.AddChild(panel);

		var v = new VBoxContainer();
		v.AddThemeConstantOverride("separation", 14);
		panel.AddChild(v);

		var title = UIStyle.MakeLabel(Title, 18, UIStyle.GoldBright);
		title.HorizontalAlignment = HorizontalAlignment.Center;
		v.AddChild(title);

		v.AddChild(new HSeparator());

		var body = UIStyle.MakeLabel(Body, 13, UIStyle.TextPrimary);
		body.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		body.HorizontalAlignment = HorizontalAlignment.Center;
		v.AddChild(body);

		LineEdit typedInput = null;
		if (!string.IsNullOrEmpty(RequireTypedConfirmation))
		{
			var hint = UIStyle.MakeLabel(
				$"Введите «{RequireTypedConfirmation}» для подтверждения:",
				12, UIStyle.TextSecondary);
			hint.HorizontalAlignment = HorizontalAlignment.Center;
			v.AddChild(hint);

			typedInput = new LineEdit { PlaceholderText = RequireTypedConfirmation };
			typedInput.AddThemeColorOverride("font_color", UIStyle.TextPrimary);
			v.AddChild(typedInput);
		}

		var btnBar = new HBoxContainer();
		btnBar.AddThemeConstantOverride("separation", 14);
		btnBar.Alignment = BoxContainer.AlignmentMode.Center;
		v.AddChild(btnBar);

		var cancelBtn = new Button { Text = CancelText };
		UIStyle.StyleButton(cancelBtn);
		cancelBtn.Pressed += () =>
		{
			EmitSignal(SignalName.Cancelled);
			QueueFree();
		};
		btnBar.AddChild(cancelBtn);

		_confirmBtn = new Button { Text = ConfirmText };
		UIStyle.StyleButton(_confirmBtn, primary: true);
		_confirmBtn.Pressed += () =>
		{
			EmitSignal(SignalName.Confirmed);
			QueueFree();
		};
		btnBar.AddChild(_confirmBtn);

		if (typedInput != null)
		{
			_confirmBtn.Disabled = true;
			typedInput.TextChanged += (text) =>
			{
				_confirmBtn.Disabled = text != RequireTypedConfirmation;
			};
		}
	}
}

using System;
using System.Threading.Tasks;
using Godot;
using GuildOfGreed.Shared.Net;

// Экран выбора персонажа после логина. Тащит список с сервера через
// ListCharactersAsync, рендерит карточки + слот "Создать". Удаление —
// с подтверждением (двойной клик по кнопке).
public partial class CharacterSelectView : Control
{
	[Signal] public delegate void CharacterSelectedEventHandler(string characterId);
	[Signal] public delegate void CreateCharacterRequestedEventHandler();
	[Signal] public delegate void LogoutRequestedEventHandler();

	private const int MaxSlots = 7;

	private readonly NetworkClient _net;
	private readonly string _login;

	private VBoxContainer _list;
	private Label _statusLabel;
	private Button _logoutBtn;

	public CharacterSelectView(NetworkClient net, string login)
	{
		_net = net;
		_login = login;
	}

	public override void _Ready()
	{
		UIStyle.FillParent(this);
		BuildUI();
		_ = LoadCharactersAsync();
	}

	private void BuildUI()
	{
		var bg = new ColorRect { Color = UIStyle.BgDeep };
		UIStyle.FillParent(bg);
		bg.MouseFilter = MouseFilterEnum.Ignore;
		AddChild(bg);

		// Top bar
		var top = new HBoxContainer { Position = new Vector2(20, 12) };
		top.AddThemeConstantOverride("separation", 10);
		AddChild(top);

		_logoutBtn = new Button { Text = "🚪 Выйти" };
		UIStyle.StyleButton(_logoutBtn);
		_logoutBtn.Pressed += () => EmitSignal(SignalName.LogoutRequested);
		top.AddChild(_logoutBtn);

		var login = UIStyle.MakeLabel($"👤 {_login}", 14, UIStyle.TextPrimary);
		login.Position = new Vector2(160, 22);
		AddChild(login);

		// Title
		var title = UIStyle.MakeLabel("🗡  Выбор персонажа", 28, UIStyle.GoldBright);
		title.Position = new Vector2(440, 70);
		AddChild(title);

		_statusLabel = UIStyle.MakeLabel("", 14, UIStyle.TextSecondary);
		_statusLabel.Position = new Vector2(440, 110);
		AddChild(_statusLabel);

		// Список карточек персонажей
		var scroll = new ScrollContainer
		{
			Position = new Vector2(60, 150),
			Size = new Vector2(1160, 540),
		};
		scroll.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;
		AddChild(scroll);

		_list = new VBoxContainer();
		_list.AddThemeConstantOverride("separation", 12);
		_list.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		scroll.AddChild(_list);
	}

	private async Task LoadCharactersAsync()
	{
		_statusLabel.Text = "Загрузка персонажей...";
		_statusLabel.AddThemeColorOverride("font_color", UIStyle.TextSecondary);
		try
		{
			var resp = await _net.ListCharactersAsync();
			RenderCharacters(resp);
			_statusLabel.Text = "";
		}
		catch (Exception ex)
		{
			_statusLabel.Text = $"Не удалось загрузить персонажей: {ex.Message}";
			_statusLabel.AddThemeColorOverride("font_color", UIStyle.DangerRed);
		}
	}

	private void RenderCharacters(ListCharactersResponse resp)
	{
		ClearList();
		int count = resp.Characters?.Count ?? 0;

		if (count == 0)
		{
			var empty = UIStyle.MakeLabel(
				"У вас пока нет персонажей. Создайте первого:",
				14, UIStyle.TextSecondary);
			_list.AddChild(empty);
		}
		else
		{
			foreach (var ch in resp.Characters) _list.AddChild(MakeCharacterCard(ch));
		}

		if (count < MaxSlots)
		{
			var createBtn = new Button { Text = "➕ Создать персонажа" };
			UIStyle.StyleButton(createBtn, primary: true);
			createBtn.SizeFlagsHorizontal = SizeFlags.ExpandFill;
			createBtn.CustomMinimumSize = new Vector2(0, 60);
			createBtn.Pressed += () => EmitSignal(SignalName.CreateCharacterRequested);
			_list.AddChild(createBtn);
		}
		else
		{
			var full = UIStyle.MakeLabel(
				$"Все {MaxSlots} слотов заняты. Удалите кого-нибудь чтобы создать нового.",
				13, UIStyle.WarnAmber);
			_list.AddChild(full);
		}
	}

	private PanelContainer MakeCharacterCard(CharacterSummary c)
	{
		var card = new PanelContainer
		{
			CustomMinimumSize = new Vector2(0, 80),
		};
		card.AddThemeStyleboxOverride("panel", UIStyle.PanelStyle());

		var row = new HBoxContainer();
		row.AddThemeConstantOverride("separation", 16);
		card.AddChild(row);

		var info = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
		info.AddChild(UIStyle.MakeLabel(c.Name, 18, UIStyle.GoldBright));
		info.AddChild(UIStyle.MakeLabel(
			$"Уровень {c.Level} · Грейд {c.Grade}",
			13, UIStyle.TextSecondary));
		row.AddChild(info);

		var actions = new VBoxContainer();
		actions.AddThemeConstantOverride("separation", 6);

		var enterBtn = new Button { Text = "Войти" };
		UIStyle.StyleButton(enterBtn, primary: true);
		var capturedId = c.Id;
		enterBtn.Pressed += () => EmitSignal(SignalName.CharacterSelected, capturedId.ToString());
		actions.AddChild(enterBtn);

		var deleteBtn = new Button { Text = "Удалить" };
		UIStyle.StyleButton(deleteBtn);
		deleteBtn.Pressed += () => OpenDeleteDialog(c);
		actions.AddChild(deleteBtn);
		row.AddChild(actions);

		return card;
	}

	private void OpenDeleteDialog(CharacterSummary c)
	{
		var dialog = new ConfirmDialog
		{
			Title = "Удалить персонажа?",
			Body =
				$"«{c.Name}», уровень {c.Level}, грейд {c.Grade}.\n\n" +
				"Удаление переводит персонажа в скрытый список. " +
				"Восстановление возможно в течение 7 дней через поддержку.",
			ConfirmText = "Удалить навсегда",
			CancelText = "Отмена",
			RequireTypedConfirmation = c.Name,
		};
		var capturedId = c.Id;
		dialog.Confirmed += () => _ = DoDeleteAsync(capturedId);
		AddChild(dialog);
	}

	private async Task DoDeleteAsync(Guid characterId)
	{
		try
		{
			var resp = await _net.DeleteCharacterAsync(characterId);
			if (resp.Success)
			{
				_statusLabel.Text = "";
				await LoadCharactersAsync();
				return;
			}
			_statusLabel.Text = TranslateDeleteError(resp.Error);
			_statusLabel.AddThemeColorOverride("font_color", UIStyle.DangerRed);
		}
		catch (Exception ex)
		{
			_statusLabel.Text = $"Ошибка удаления: {ex.Message}";
			_statusLabel.AddThemeColorOverride("font_color", UIStyle.DangerRed);
		}
	}

	private static string TranslateDeleteError(string code) => code switch
	{
		"not_found"    => "Персонаж не найден (возможно уже удалён).",
		"rate_limited" => "Слишком много удалений за час. Подождите.",
		_              => $"Не удалось удалить: {code}",
	};

	private void ClearList()
	{
		foreach (Node n in _list.GetChildren())
		{
			_list.RemoveChild(n);
			n.QueueFree();
		}
	}
}

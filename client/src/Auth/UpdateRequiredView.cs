using Godot;
using GuildOfGreed.Shared.Net;

// Экран при несовместимости версий протокола.
// ServerWelcome.Compatible == false → клиент попадает сюда вместо AuthView.
// Кнопка единственная: закрыть приложение (UpdateUrl пуст на dev).
public partial class UpdateRequiredView : Control
{
	private readonly ServerWelcome _welcome;

	public UpdateRequiredView(ServerWelcome welcome)
	{
		_welcome = welcome;
	}

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

		var panel = new PanelContainer
		{
			Position = new Vector2(360, 200),
			CustomMinimumSize = new Vector2(560, 320),
		};
		panel.AddThemeStyleboxOverride("panel", UIStyle.PanelStyle());
		AddChild(panel);

		var v = new VBoxContainer();
		v.AddThemeConstantOverride("separation", 16);
		panel.AddChild(v);

		v.AddChild(UIStyle.MakeLabel("⚠ Требуется обновление", 24, UIStyle.WarnAmber));

		var msg = UIStyle.MakeLabel(
			$"Сервер использует протокол версии {_welcome.ServerProtocolVersion},\n" +
			$"а клиент — {ProtocolVersion.Current}.\n\n" +
			$"Минимальная поддерживаемая версия клиента: {_welcome.MinSupportedClientVersion}.\n" +
			"Обновите клиент и попробуйте снова.",
			14, UIStyle.TextPrimary);
		msg.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		v.AddChild(msg);

		if (!string.IsNullOrEmpty(_welcome.UpdateUrl))
		{
			var url = UIStyle.MakeLabel(_welcome.UpdateUrl, 12, UIStyle.MpFill);
			v.AddChild(url);
		}

		var quitBtn = new Button { Text = "Закрыть игру" };
		UIStyle.StyleButton(quitBtn, primary: true);
		quitBtn.Pressed += () => GetTree().Quit();
		v.AddChild(quitBtn);
	}
}

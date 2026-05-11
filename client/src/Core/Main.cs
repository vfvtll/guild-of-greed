using System;
using System.Text.Json;
using Godot;
using GuildOfGreed.Shared.Domain;
using GuildOfGreed.Shared.Net;

// Корневой роутер.
//
// Поток (после введения сети):
//   Connecting → handshake → если несовместимо: UpdateRequired
//                          → если есть токен: Resume → CharacterSelect (или AuthView при failure)
//                          → иначе: AuthView
//   AuthView → CharacterSelect
//   CharacterSelect → CharacterCreation (создать) или сразу LocationSelect (выбрать существующего)
//   LocationSelect → MapView → Combat → ... → LocationSelect (как раньше)
//   Logout → AuthPrefs.Clear → AuthView
public partial class Main : Control
{
	private NetworkClient _net;
	private string _login;          // login текущего залогиненного пользователя

	public override void _Ready()
	{
		SetAnchorsPreset(LayoutPreset.FullRect);
		MouseFilter = MouseFilterEnum.Ignore;
		StartFlow();
	}

	private void StartFlow()
	{
		_net?.Dispose();
		_net = new NetworkClient();
		ShowConnecting();
	}

	// =====================================================================
	// Connecting / Handshake / Auto-resume
	// =====================================================================

	private void ShowConnecting()
	{
		ClearContent();
		var view = new ConnectingView();
		view.RetryRequested += StartFlow;
		AddChild(view);
		_ = ConnectAndAuthenticateAsync(view);
	}

	private async System.Threading.Tasks.Task ConnectAndAuthenticateAsync(ConnectingView view)
	{
		try
		{
			view.SetStatus($"Соединение с {NetPrefs.Host}:{NetPrefs.Port}...");
			await _net.ConnectAsync(NetPrefs.Host, NetPrefs.Port);

			view.SetStatus("Сверка версий...");
			var welcome = await _net.HandshakeAsync();
			if (!welcome.Compatible)
			{
				ShowUpdateRequired(welcome);
				return;
			}

			if (AuthPrefs.HasSession())
			{
				view.SetStatus("Восстановление сессии...");
				var prefs = AuthPrefs.Load();
				try
				{
					var resp = await _net.ResumeAsync(prefs.Token);
					if (resp.Success)
					{
						_login = resp.Login;
						ShowCharacterSelect();
						return;
					}
					AuthPrefs.Clear();
				}
				catch (ServerException)
				{
					AuthPrefs.Clear();
				}
			}

			ShowAuth();
		}
		catch (TlsPinMismatchException ex)
		{
			view.SetPinMismatch(ex.Host, ex.Port);
		}
		catch (Exception ex)
		{
			view.SetError($"Не удалось подключиться: {ex.Message}");
		}
	}

	// =====================================================================
	// Auth screens
	// =====================================================================

	private void ShowAuth()
	{
		ClearContent();
		var view = new AuthView(_net);
		view.AuthSucceeded += OnAuthSucceeded;
		AddChild(view);
	}

	private void OnAuthSucceeded(string token, string accountId, string login)
	{
		AuthPrefs.Save(token, Guid.Parse(accountId), login);
		_login = login;
		ShowCharacterSelect();
	}

	private void ShowCharacterSelect()
	{
		ClearContent();
		var view = new CharacterSelectView(_net, _login);
		view.CharacterSelected += OnCharacterSelected;
		view.CreateCharacterRequested += ShowCharacterCreation;
		view.LogoutRequested += OnLogoutRequested;
		AddChild(view);
	}

	private async void OnCharacterSelected(string characterId)
	{
		try
		{
			var resp = await _net.SelectCharacterAsync(Guid.Parse(characterId));
			if (!resp.Success)
			{
				GD.PrintErr($"SelectCharacter failed: {resp.Error}");
				ShowCharacterSelect();
				return;
			}
			var character = JsonSerializer.Deserialize<CharacterData>(resp.CharacterJson,
				new JsonSerializerOptions { IncludeFields = true });
			GameData.Instance.SetCharacter(character);
			ShowLocationSelect();
		}
		catch (Exception ex)
		{
			GD.PrintErr($"SelectCharacter exception: {ex.Message}");
			ShowCharacterSelect();
		}
	}

	private async void OnLogoutRequested()
	{
		try { await _net.LogoutAsync(); } catch { /* swallow — token уже на сервере мог истечь */ }
		AuthPrefs.Clear();
		_login = null;
		StartFlow();
	}

	private void ShowUpdateRequired(ServerWelcome welcome)
	{
		ClearContent();
		AddChild(new UpdateRequiredView(welcome));
	}

	// =====================================================================
	// Character creation
	// =====================================================================

	private void ShowCharacterCreation()
	{
		ClearContent();
		var cc = new CharacterCreation();
		cc.Confirmed += OnCharacterCreationConfirmed;
		AddChild(cc);
	}

	private async void OnCharacterCreationConfirmed(CharacterData ch)
	{
		try
		{
			var resp = await _net.CreateCharacterAsync(
				ch.CharacterName, ch.Str, ch.Int, ch.Con, ch.Wit, ch.Men, ch.Dex);
			if (!resp.Success)
			{
				GD.PrintErr($"CreateCharacter failed: {resp.Error}");
			}
		}
		catch (Exception ex)
		{
			GD.PrintErr($"CreateCharacter exception: {ex.Message}");
		}
		ShowCharacterSelect();
	}

	// =====================================================================
	// Game (как было до этой главы)
	// =====================================================================

	private void ShowLocationSelect()
	{
		GameData.Instance.EndRun();
		ClearContent();
		var view = new LocationSelectView();
		view.LocationChosen += OnLocationChosen;
		view.ResetCharacterRequested += OnResetCharacterFromGame;
		AddChild(view);
	}

	private void OnLocationChosen(int index)
	{
		GameData.Instance.StartRun(index);
		ShowMap();
	}

	private void ShowMap()
	{
		ClearContent();
		var map = new MapView();
		map.NodeSelected += OnMapNodeSelected;
		map.BackToLocationsRequested += ShowLocationSelect;
		map.ResetCharacterRequested += OnResetCharacterFromGame;
		AddChild(map);
	}

	private void OnMapNodeSelected(int nodeId)
	{
		var run = GameData.Instance.CurrentRun;
		if (run == null) return;
		var node = run.GetNode(nodeId);
		if (node == null) return;

		switch (node.Type)
		{
			case MapNodeType.Battle:
			case MapNodeType.Elite:
			case MapNodeType.Boss:
				ShowCombatForNode(nodeId);
				break;
			default:
				GameData.Instance.AdvanceTo(nodeId);
				ShowMap();
				break;
		}
	}

	private void ShowCombatForNode(int nodeId)
	{
		var run = GameData.Instance.CurrentRun;
		int prevNodeId = run.CurrentNodeId;
		run.Advance(nodeId);
		var n = run.GetNode(nodeId);
		if (n != null) n.Visited = false;

		ClearContent();
		var combat = new Combat { Net = _net };
		combat.ResetCharacterRequested += OnResetCharacterFromGame;
		combat.CombatExitRequested += (advance) => OnCombatExit(advance, nodeId, prevNodeId);
		AddChild(combat);
	}

	private void OnCombatExit(bool advance, int nodeId, int prevNodeId)
	{
		var run = GameData.Instance.CurrentRun;
		var node = run?.GetNode(nodeId);
		var character = GameData.Instance.Character;
		bool died = character != null && character.CurrentHp <= 0;

		if (died)
		{
			ShowLocationSelect();
			return;
		}

		if (advance && node != null)
		{
			node.Visited = true;
			if (node.Type == MapNodeType.Boss)
			{
				ShowLocationSelect();
				return;
			}
			ShowMap();
			return;
		}

		if (run != null) run.CurrentNodeId = prevNodeId;
		ShowMap();
	}

	// «Новый персонаж» из игровых экранов теперь = вернуться к выбору слота
	// (а не удалять профиль локально). Удаление делается из CharacterSelectView.
	private void OnResetCharacterFromGame()
	{
		GameData.Instance.EndRun();
		GameData.Instance.SetCharacter(null);
		ShowCharacterSelect();
	}

	// =====================================================================

	private void ClearContent()
	{
		foreach (Node child in GetChildren())
		{
			RemoveChild(child);
			child.QueueFree();
		}
	}

	public override void _ExitTree()
	{
		_net?.Dispose();
	}
}

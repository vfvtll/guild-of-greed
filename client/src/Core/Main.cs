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
	private ReconnectOverlay _reconnectOverlay;
	private bool _reconnecting;

	public override void _Ready()
	{
		SetAnchorsPreset(LayoutPreset.FullRect);
		MouseFilter = MouseFilterEnum.Ignore;
		StartFlow();
	}

	private void StartFlow()
	{
		_net?.Dispose();
		_net = NewNet();
		// GameData использует _net для пуша CharacterData после любой мутации
		// (equip/buy/sell/стэш/spend). Должен быть выставлен ДО первых мутаций —
		// фактически как только handshake пройдёт (для пушей нужен авторизованный
		// канал), но безопаснее ставить сразу: PushCharacterToServer проверит Net != null.
		GameData.Instance.Net = _net;
		ShowConnecting();
	}

	// Фабрика: каждый новый NetworkClient заново подписывается на Disconnected.
	private NetworkClient NewNet()
	{
		var net = new NetworkClient();
		net.Disconnected += OnNetDisconnected;
		return net;
	}

	private void OnNetDisconnected(string reason)
	{
		// Disconnected эмитится из network-таска. Все Godot-операции
		// маршалим обратно в main thread через CallDeferred.
		GD.Print($"Main: net disconnected: {reason}");
		CallDeferred(MethodName.BeginReconnect);
	}

	private void BeginReconnect()
	{
		if (_reconnecting) return;
		_reconnecting = true;
		_reconnectOverlay = new ReconnectOverlay();
		_reconnectOverlay.RetryRequested += () => _ = ReconnectAsync();
		AddChild(_reconnectOverlay);
		_ = ReconnectAsync();
	}

	private async System.Threading.Tasks.Task ReconnectAsync()
	{
		if (_reconnectOverlay == null) return;
		_reconnectOverlay.SetStatus($"Соединение с {NetPrefs.Host}:{NetPrefs.Port}...");
		try
		{
			_net?.Dispose();
			_net = NewNet();
			await _net.ConnectAsync(NetPrefs.Host, NetPrefs.Port);
			_reconnectOverlay.SetStatus("Сверка версий...");
			var welcome = await _net.HandshakeAsync();
			if (!welcome.Compatible)
			{
				FinishReconnect();
				ShowUpdateRequired(welcome);
				return;
			}
			if (AuthPrefs.HasSession())
			{
				_reconnectOverlay.SetStatus("Восстановление сессии...");
				var prefs = AuthPrefs.Load();
				var resp = await _net.ResumeAsync(prefs.Token);
				if (!resp.Success)
				{
					AuthPrefs.Clear();
					FinishReconnect();
					ShowAuth();
					return;
				}
				_login = resp.Login;
			}
			// Активный бой/run на сервере уже не существует (in-memory). Кидаем
			// игрока в хаб — это известно-хорошее состояние.
			FinishReconnect();
			if (string.IsNullOrEmpty(_login)) ShowAuth();
			else                              ShowLocationSelect();
		}
		catch (System.Exception ex)
		{
			_reconnectOverlay?.SetError($"Не удалось восстановить: {ex.Message}");
		}
	}

	private void FinishReconnect()
	{
		_reconnecting = false;
		if (_reconnectOverlay != null)
		{
			RemoveChild(_reconnectOverlay);
			_reconnectOverlay.QueueFree();
			_reconnectOverlay = null;
		}
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
				ShowCharacterSelect();
				return;
			}

			// Сразу выбираем созданного персонажа и кидаем в стартовый бой —
			// игрок не должен видеть промежуточный экран со списком слотов.
			var selectResp = await _net.SelectCharacterAsync(resp.CharacterId);
			if (!selectResp.Success)
			{
				GD.PrintErr($"SelectCharacter after create failed: {selectResp.Error}");
				ShowCharacterSelect();
				return;
			}
			var character = JsonSerializer.Deserialize<CharacterData>(selectResp.CharacterJson,
				new JsonSerializerOptions { IncludeFields = true });
			GameData.Instance.SetCharacter(character);
			ShowStarterBattle();
		}
		catch (Exception ex)
		{
			GD.PrintErr($"CreateCharacter exception: {ex.Message}");
			ShowCharacterSelect();
		}
	}

	// =====================================================================
	// Стартовый бой (Tutorial)
	// =====================================================================

	private void ShowStarterBattle()
	{
		// Никакого StartRun: RunMap не создаётся, и Combat использует
		// LocationOverride/NodeTypeOverride. После боя — обычный LocationSelect.
		GameData.Instance.EndRun();
		ClearContent();
		var combat = new Combat
		{
			Net = _net,
			LocationOverride = 1,                          // "Тёмный лес"
			NodeTypeOverride = (int)MapNodeType.Tutorial,
		};
		combat.ResetCharacterRequested += OnResetCharacterFromGame;
		combat.CombatExitRequested += OnStarterCombatExit;
		AddChild(combat);
	}

	private void OnStarterCombatExit(bool advance)
	{
		var character = GameData.Instance.Character;
		bool died = character != null && character.CurrentHp <= 0;

		// При поражении (или ручном Flee) — повторяем стартовый бой:
		// без оружия не пройти дальше, а флаг IsNewCharacter ещё стоит.
		if (died || !advance)
		{
			ShowStarterBattle();
			return;
		}
		// Победа: сервер уже сбросил IsNewCharacter и положил лут в инвентарь.
		// Локально тоже обнуляем флаг — иначе при повторном SetCharacter в
		// этой сессии EnsureDefaults будет считать персонажа новым.
		if (character != null) character.IsNewCharacter = false;
		ShowEquipmentTutorial();
	}

	// =====================================================================
	// Туториал по экипировке (после стартового боя)
	// =====================================================================

	private void ShowEquipmentTutorial()
	{
		ClearContent();
		var view = new EquipmentTutorialView();
		view.OpenInventoryRequested += OnTutorialOpenInventory;
		view.SkipRequested += ShowLocationSelect;
		AddChild(view);
	}

	private void OnTutorialOpenInventory()
	{
		// Открываем существующий InventoryOverlay поверх туториала. Когда игрок
		// закроет инвентарь — уходим в LocationSelect (без второго подтверждения).
		var overlay = new InventoryOverlay { ReadOnly = false };
		overlay.Closed += ShowLocationSelect;
		AddChild(overlay);
	}

	// =====================================================================
	// Game (как было до этой главы)
	// =====================================================================

	private void ShowLocationSelect()
	{
		EndCurrentRun();
		ClearContent();
		var view = new LocationSelectView();
		view.LocationChosen += OnLocationChosen;
		view.ResetCharacterRequested += OnResetCharacterFromGame;
		view.TownRequested += ShowTown;
		AddChild(view);
	}

	private void ShowTown()
	{
		EndCurrentRun();
		ClearContent();
		var view = new TownView();
		view.LeaveTownRequested += ShowLocationSelect;
		AddChild(view);
	}

	// Локально + сетевой EndRun (fire-and-forget). Зовётся всеми путями
	// выхода из подземелья: победа на боссе, смерть, ручной flee, переход
	// в город. Сервер сбросит свой _runSnapshot — следующий StartRun снимет
	// новый снэпшот с актуального DB-состояния.
	private void EndCurrentRun()
	{
		bool wasInRun = GameData.Instance.CurrentRun != null;
		GameData.Instance.EndRun();
		if (wasInRun && _net != null) _ = SafeEndRunAsync();
	}

	private async System.Threading.Tasks.Task SafeEndRunAsync()
	{
		try { await _net.EndRunAsync(); }
		catch (System.Exception ex) { GD.PrintErr($"EndRun network error: {ex.Message}"); }
	}

	private async void OnLocationChosen(int index)
	{
		// 1. Локально восстанавливаем HP/MP — забег начинается отдохнувшим.
		GameData.Instance.Character?.ResetForCombat();

		// 2. Пушим обновлённый Character в БД ДО того как сервер снимет run-snapshot.
		try { await GameData.Instance.PushCharacterAndAwaitAsync(); }
		catch (System.Exception ex) { GD.PrintErr($"OnLocationChosen push error: {ex.Message}"); }

		// 3. Сервер генерит runSeed (один на всё подземелье) и снимает character snapshot.
		StartRunResponse resp;
		try { resp = await _net.StartRunAsync(index); }
		catch (System.Exception ex)
		{
			GD.PrintErr($"StartRun network error: {ex.Message}");
			ShowLocationSelect();
			return;
		}
		if (!resp.Success)
		{
			GD.PrintErr($"StartRun failed: {resp.Error}");
			ShowLocationSelect();
			return;
		}

		// 4. Локально строим карту и фиксируем колоду по серверному seed'у.
		GameData.Instance.StartRun(index, resp.RunSeed);
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
			// Reconnect overlay переживает переходы между экранами.
			if (child == _reconnectOverlay) continue;
			RemoveChild(child);
			child.QueueFree();
		}
	}

	public override void _ExitTree()
	{
		_net?.Dispose();
	}
}

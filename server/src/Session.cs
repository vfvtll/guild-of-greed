using System;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using GuildOfGreed.Shared.Auth;
using GuildOfGreed.Shared.Combat;
using GuildOfGreed.Shared.Commands;
using GuildOfGreed.Shared.Crypto;
using GuildOfGreed.Shared.Data;
using GuildOfGreed.Shared.Domain;
using GuildOfGreed.Shared.Net;

namespace GuildOfGreed.Server;

// Одно TCP-соединение клиента. Состояния:
//   1. Handshake     — ждём ClientHello, шлём ServerWelcome.
//   2. Unauthenticated — ждём Register/Login/ResumeSession.
//   3. Authenticated — ждём List/Create/Select/DeleteCharacter, Logout.
//   (4. Playing       — после SelectCharacter; обработка пойдёт в И5.)
//
// Любое нарушение протокола (неподходящий тип в стейте, исключение, разрыв TCP)
// приводит к закрытию соединения. Никаких retry на серверной стороне —
// клиент сам переподключается.
public class Session
{
	// Минимальная версия клиента, которую мы ещё поддерживаем. Поднимать когда
	// ломаем обратную совместимость на серверной стороне.
	//
	// v15 удалил PushCharacterRequest и ввёл CharacterCommand*. Старые клиенты
	// (v14 и ниже) будут пытаться слепо пушить полный JSON — это уже не работает,
	// поэтому минимум поднимаем до 15.
	public const int MinSupportedClientVersion = 15;

	private readonly TcpClient _tcp;
	private readonly Stream _stream;
	private readonly AccountStore _store;
	private readonly string _peer;
	private readonly CancellationToken _shutdown;

	private Guid? _accountId;
	private string _login;
	private Guid? _selectedCharacterId;
	private BattleSession _battle;
	private readonly Random _serverRng = new();

	// Снэпшот персонажа на старте забега — используется для расчёта колоды
	// (CardsDB.DeckFor) во всех боях внутри run. Мутации мид-ран (например
	// ап оружия) на колоду не влияют. null = вне забега (туториал/хаб).
	private CharacterData _runSnapshot;
	// Сид забега. Из него генерится карта и выводятся все боевые seed'ы
	// (deriveBattleSeed). 0 = вне забега; ставится в HandleStartRun.
	private int _runSeed;

	public Session(TcpClient tcp, Stream stream, AccountStore store, string peer, CancellationToken shutdown)
	{
		_tcp = tcp;
		_stream = stream;
		_store = store;
		_peer = peer;
		_shutdown = shutdown;
	}

	public async Task RunAsync()
	{
		try
		{
			if (!await DoHandshakeAsync().ConfigureAwait(false)) return;
			await DoMessageLoopAsync().ConfigureAwait(false);
		}
		catch (EndOfStreamException)
		{
			Logger.Info($"[{_peer}] disconnected");
		}
		catch (OperationCanceledException)
		{
			// Сервер shutdown — норма.
		}
		catch (Exception ex)
		{
			Logger.Error($"[{_peer}] session crashed", ex);
			await TrySendErrorAsync("internal_error", "unexpected error").ConfigureAwait(false);
		}
		finally
		{
			try { _tcp.Close(); } catch { /* swallow */ }
		}
	}

	private async Task<bool> DoHandshakeAsync()
	{
		ClientMessage msg = await Codec.ReceiveAsync<ClientMessage>(_stream, _shutdown).ConfigureAwait(false);
		if (msg is not ClientHello hello)
		{
			await SendAsync(new ServerError { Code = "protocol_violation", Message = "expected ClientHello" }).ConfigureAwait(false);
			return false;
		}

		bool compatible = hello.ProtocolVersion >= MinSupportedClientVersion
		               && hello.ProtocolVersion <= ProtocolVersion.Current;

		await SendAsync(new ServerWelcome
		{
			Compatible = compatible,
			ServerProtocolVersion = ProtocolVersion.Current,
			MinSupportedClientVersion = MinSupportedClientVersion,
			UpdateUrl = "",
		}).ConfigureAwait(false);

		if (!compatible)
		{
			Logger.Warn($"[{_peer}] incompatible client v{hello.ProtocolVersion} (build={hello.ClientBuild ?? ""})");
			return false;
		}

		Logger.Info($"[{_peer}] handshake ok, client v{hello.ProtocolVersion} (build={hello.ClientBuild ?? ""})");
		return true;
	}

	private async Task DoMessageLoopAsync()
	{
		while (!_shutdown.IsCancellationRequested)
		{
			ClientMessage msg = await Codec.ReceiveAsync<ClientMessage>(_stream, _shutdown).ConfigureAwait(false);
			ServerMessage reply = await DispatchAsync(msg).ConfigureAwait(false);
			if (reply != null) await SendAsync(reply).ConfigureAwait(false);
		}
	}

	// =======================================================================
	// Dispatch
	// =======================================================================

	// Все текущие хендлеры синхронные; обёртка в Task оставлена под будущее
	// (async access к БД, ожидание игровых событий и т.п.).
	private Task<ServerMessage> DispatchAsync(ClientMessage msg)
	{
		ServerMessage reply;
		if (_accountId == null)
		{
			reply = msg switch
			{
				RegisterRequest r       => HandleRegister(r),
				LoginRequest r          => HandleLogin(r),
				ResumeSessionRequest r  => HandleResume(r),
				_ => UnauthorizedReply(msg),
			};
		}
		else
		{
			reply = msg switch
			{
				LogoutRequest r              => HandleLogout(r),
				ListCharactersRequest r      => HandleListCharacters(r),
				CreateCharacterRequest r     => HandleCreateCharacter(r),
				SelectCharacterRequest r     => HandleSelectCharacter(r),
				DeleteCharacterRequest r     => HandleDeleteCharacter(r),
				StartBattleRequest r         => HandleStartBattle(r),
				BattleActionRequest r        => HandleBattleAction(r),
				GetBattleStateRequest r      => HandleGetBattleState(r),
				StartRunRequest r            => HandleStartRun(r),
				EndRunRequest r              => HandleEndRun(r),
				BuyItemRequest r             => HandleBuyItem(r),
				SellSlotRequest r            => HandleSellSlot(r),
				EquipFromInventoryRequest r  => HandleEquipFromInventory(r),
				UnequipSlotRequest r         => HandleUnequipSlot(r),
				UsePotionRequest r           => HandleUsePotion(r),
				DepositToStashRequest r      => HandleDepositToStash(r),
				WithdrawFromStashRequest r   => HandleWithdrawFromStash(r),
				ForgeDismantleRequest r      => HandleForgeDismantle(r),
				ForgeUpgradeRequest r        => HandleForgeUpgrade(r),
				ForgeRerollRequest r         => HandleForgeReroll(r),
				SpendStatPointRequest r      => HandleSpendStatPoint(r),
				CraftItemRequest r           => HandleCraftItem(r),
				PromoteGradeRequest r        => HandlePromoteGrade(r),
				_ => UnknownReply(msg),
			};
		}
		return Task.FromResult(reply);
	}

	private ServerMessage HandleRegister(RegisterRequest r)
	{
		var result = _store.CreateAccount(r.Login, r.Email, r.Password, out var account);
		if (result != AccountStore.CreateResult.Ok)
		{
			Logger.Info($"[{_peer}] register failed: {result}");
			return new RegisterResponse { Success = false, Error = ErrorString(result) };
		}
		var session = _store.IssueSession(account.Id);
		_accountId = account.Id;
		_login = account.Login;
		Logger.Info($"[{_peer}] register ok login={account.Login}");
		return new RegisterResponse
		{
			Success = true,
			Token = session.Token,
			AccountId = account.Id,
		};
	}

	private ServerMessage HandleLogin(LoginRequest r)
	{
		var account = _store.FindByLogin(r.Login ?? "");
		if (account == null || !PasswordHasher.Verify(r.Password ?? "", account.PasswordHash))
		{
			Logger.Info($"[{_peer}] login failed for '{r.Login}'");
			return new LoginResponse { Success = false, Error = "invalid_credentials" };
		}
		var session = _store.IssueSession(account.Id);
		_accountId = account.Id;
		_login = account.Login;
		Logger.Info($"[{_peer}] login ok login={account.Login}");
		return new LoginResponse
		{
			Success = true,
			Token = session.Token,
			AccountId = account.Id,
		};
	}

	private ServerMessage HandleResume(ResumeSessionRequest r)
	{
		var session = _store.FindSession(r.Token ?? "");
		if (session == null)
		{
			return new ResumeSessionResponse { Success = false, Error = "expired_or_unknown" };
		}
		var account = _store.FindById(session.AccountId);
		if (account == null)
		{
			_store.DeleteSession(r.Token);
			return new ResumeSessionResponse { Success = false, Error = "expired_or_unknown" };
		}
		_accountId = account.Id;
		_login = account.Login;
		Logger.Info($"[{_peer}] resumed login={account.Login}");
		return new ResumeSessionResponse
		{
			Success = true,
			AccountId = account.Id,
			Login = account.Login,
		};
	}

	private ServerMessage HandleLogout(LogoutRequest r)
	{
		// На прототипе токен у сессии не сохраняется в RAM (использует БД).
		// Realистичный logout будет принимать токен явным полем; добавим если понадобится.
		_accountId = null;
		_login = null;
		Logger.Info($"[{_peer}] logout");
		return new LogoutResponse { Success = true };
	}

	private ServerMessage HandleListCharacters(ListCharactersRequest r)
	{
		var chars = _store.ListCharacters(_accountId.Value);
		return new ListCharactersResponse
		{
			Characters = chars.ConvertAll(c => new CharacterSummary
			{
				Id = c.Id,
				Name = c.CharacterName,
				Level = c.Level,
				Grade = c.Grade,
			}),
		};
	}

	private ServerMessage HandleCreateCharacter(CreateCharacterRequest r)
	{
		var ch = new CharacterData
		{
			Id = Guid.NewGuid(),
			CharacterName = r.CharacterName ?? "",
			Str = r.Str, Int = r.Int, Con = r.Con, Wit = r.Wit, Men = r.Men, Dex = r.Dex,
			IsNewCharacter = true,
		};
		// Новый персонаж должен начать с полным HP/MP (поля persist'ятся).
		ch.ResetForCombat();
		var result = _store.CreateCharacter(_accountId.Value, ch, out var newId);
		if (result != AccountStore.CreateCharResult.Ok)
		{
			Logger.Info($"[{_peer}] create char failed: {result}");
			return new CreateCharacterResponse { Success = false, Error = ErrorString(result) };
		}
		Logger.Info($"[{_peer}] created char '{ch.CharacterName}' id={newId}");
		return new CreateCharacterResponse { Success = true, CharacterId = newId };
	}

	private ServerMessage HandleSelectCharacter(SelectCharacterRequest r)
	{
		var ch = _store.LoadCharacter(_accountId.Value, r.CharacterId);
		if (ch == null) return new SelectCharacterResponse { Success = false, Error = "not_found" };
		_selectedCharacterId = r.CharacterId;

		// Миграция старых сейвов: до И5в.6 CurrentHp/MP были [JsonIgnore] и в
		// БД лежат как 0. Без этого fix-up нельзя играть.
		if (ch.CurrentHp <= 0 || ch.CurrentMp <= 0)
		{
			ch.ResetForCombat();
			_store.UpdateCharacter(_accountId.Value, ch);
		}

		// Миграция Level → Grade (введён cap LevelsPerGrade=20). Старые сейвы
		// могут иметь Level=22 / Grade=E — переводим в Level=2 / Grade=D, чтобы
		// сквозной DisplayLevel остался прежним, а внутренний счётчик соответствовал
		// новой модели. Сохраняем обратно если что-то поменялось.
		if (ch.MigrateLevelToGrade())
		{
			_store.UpdateCharacter(_accountId.Value, ch);
			Logger.Info($"[{_peer}] migrated lvl/grade → {ch.Grade}/{ch.Level}");
		}

		// TODO(dev, crafting v0): убрать после теста крафта. Топим каждый
		// крафт-ресурс до 100 если меньше — чтобы тестовый персонаж всегда
		// мог сразу скрафтить top-E робу/комплект.
		if (TopUpDevResources(ch))
		{
			_store.UpdateCharacter(_accountId.Value, ch);
			Logger.Info($"[{_peer}] dev-grant: топ-ап крафт-ресурсов до 100");
		}

		// TODO(dev, D-grade content): убрать после теста D-локаций. Если Level
		// меньше DevTargetLevel — бустим до него и обнуляем Exp. Сразу пускает
		// тестового персонажа в D-grade локации (req. 21+) и к C-trial (38+).
		if (TopUpDevLevel(ch, DevTargetLevel))
		{
			_store.UpdateCharacter(_accountId.Value, ch);
			Logger.Info($"[{_peer}] dev-grant: уровень поднят до {ch.Level}/{ch.Grade}");
		}

		Logger.Info($"[{_peer}] selected char id={r.CharacterId} hp={ch.CurrentHp}/{ch.MaxHp()}");
		return new SelectCharacterResponse
		{
			Success = true,
			CharacterJson = System.Text.Json.JsonSerializer.Serialize(ch,
				new System.Text.Json.JsonSerializerOptions { IncludeFields = true }),
		};
	}

	// =======================================================================
	// Combat (И5в.4)
	// =======================================================================

	private ServerMessage HandleStartBattle(StartBattleRequest r)
	{
		if (_selectedCharacterId == null)
			return new BattleStartedResponse { Success = false, Error = "no_character_selected" };

		var character = _store.LoadCharacter(_accountId.Value, _selectedCharacterId.Value);
		if (character == null)
			return new BattleStartedResponse { Success = false, Error = "character_missing" };


		var nodeType = (MapNodeType)r.NodeType;
		// Сид боя: в run выводится детерминированно из (runSeed, nodeId) — один
		// runSeed на всё подземелье. Вне забега (туториал) — свежий из _serverRng.
		int seed = _runSeed != 0
			? DeriveBattleSeed(_runSeed, r.NodeId)
			: _serverRng.Next();
		// Спавн врагов от seed'а — обе стороны (клиент и сервер) получают
		// идентичный пул через детерминированный RandomSource.
		var enemies = EnemyData.SpawnFor(r.LocationIndex, nodeType, seed);
		// Колода — авторитетно с сервера. В run-режиме строим из _runSnapshot
		// (зафиксированного на StartRun), в туториал-режиме — из живого
		// персонажа. Клиент колоду не присылает.
		var charForDeck = _runSnapshot ?? character;
		var deck = CardsDB.DeckFor(charForDeck);
		// Run-эффекты подземелья — приходят от клиента (трастуются как и
		// остальная мета-прогрессия). Резолвим в объекты RunEffect через DB,
		// неизвестные ID просто отбрасываем.
		var runEffects = new System.Collections.Generic.List<RunEffect>();
		if (r.ActiveRunEffects != null)
			foreach (var id in r.ActiveRunEffects)
			{
				var eff = RunEffectsDB.Get(id);
				if (eff != null) runEffects.Add(eff);
			}

		_battle = new BattleSession(character, enemies, deck, seed, nodeType, r.LocationIndex, runEffects);
		Logger.Info($"[{_peer}] battle started loc={r.LocationIndex} node={r.NodeType} " +
			$"seed={seed} enemies={enemies.Count} effects={runEffects.Count}");

		return new BattleStartedResponse { Success = true, Seed = seed };
	}

	private ServerMessage HandleBattleAction(BattleActionRequest r)
	{
		if (_battle == null)
			return new BattleActionResponse { Confirmed = false, Error = "no_active_battle" };

		var action = new BattleAction
		{
			Type = (BattleActionType)r.ActionType,
			HandIndex = r.HandIndex,
			TargetEnemyIndex = r.TargetEnemyIndex,
			PotionId = r.PotionId,
		};

		_battle.ApplyAction(action);
		bool ended = _battle.State.CombatOver;
		bool victory = _battle.State.Victory;

		if (ended)
		{
			// Если игрок умер в подземелье — восстанавливаем HP/MP перед save,
			// иначе после relog он не сможет начать новый бой (HP=0).
			// Loot loss / экстракция-штраф — отдельный И5в.9+.
			if (_battle.Character.CurrentHp <= 0)
				_battle.Character.ResetForCombat();

			// И6.1: победа в стартовом бою (Tutorial) снимает флаг IsNewCharacter.
			// При поражении флаг сохраняется — стартовый бой будет повторён.
			if (victory && _battle.NodeType == MapNodeType.Tutorial)
				_battle.Character.IsNewCharacter = false;

			// C-trial: победа над боссом локации TrialLocationIndex даёт авто-промо
			// в C-грейд, если перс на D. Сделано серверной логикой, чтобы клиент
			// не мог сфабриковать промоушн. Идемпотентно: повтор боя ничего не
			// добавляет — Grade уже C/выше.
			if (victory
				&& _battle.NodeType == MapNodeType.Boss
				&& _battle.LocationIndex == TrialLocationIndex
				&& _battle.Character.Grade == "D")
			{
				_battle.Character.PromoteGrade();
				Logger.Info($"[{_peer}] C-trial passed → promoted to {_battle.Character.Grade}/{_battle.Character.Level}");
			}

			// И5в.5 persistence: сохраняем character_json с актуальным
			// инвентарём, эффектами, HP/MP. Engine уже мутировал state.Player.
			if (!_store.UpdateCharacter(_accountId.Value, _battle.Character))
				Logger.Warn($"[{_peer}] failed to persist character after battle");
			Logger.Info($"[{_peer}] battle ended victory={victory} hp={_battle.Character.CurrentHp}");
			_battle = null;
		}

		return new BattleActionResponse
		{
			Confirmed = true,
			BattleEnded = ended,
			Victory = victory,
		};
	}

	private ServerMessage HandleGetBattleState(GetBattleStateRequest r)
	{
		if (_battle == null)
			return new BattleStateResponse { Success = false, Error = "no_active_battle" };

		var jsonOpts = new System.Text.Json.JsonSerializerOptions { IncludeFields = true };
		var s = _battle.State;
		return new BattleStateResponse
		{
			Success = true,
			PlayerJson = System.Text.Json.JsonSerializer.Serialize(s.Player, jsonOpts),
			EnemiesJson = System.Text.Json.JsonSerializer.Serialize(s.Enemies, jsonOpts),
			Deck = new System.Collections.Generic.List<string>(s.Deck),
			Hand = new System.Collections.Generic.List<string>(s.Hand),
			Discard = new System.Collections.Generic.List<string>(s.Discard),
			TurnCount = s.TurnCount,
			Seed = s.Seed,
			RngCalls = s.Rng.Calls,
			CombatOver = s.CombatOver,
			Victory = s.Victory,
		};
	}

	// =======================================================================
	// Run lifecycle + character commands (anti-cheat)
	// =======================================================================
	//
	// Архитектура: сервер — единственный источник правды для CharacterData.
	// Клиент шлёт командные параметры (BuyItem/Equip/Forge/...), сервер сам
	// загружает character из БД, применяет CharacterCommands.<X>, сохраняет
	// обратно и отдаёт авторитетный JSON. Клиент клобберит свой Character
	// этой копией. PushCharacterRequest удалён в v15.

	private static readonly System.Text.Json.JsonSerializerOptions CharJsonOpts =
		new() { IncludeFields = true };

	private ServerMessage HandleStartRun(StartRunRequest r)
	{
		if (_accountId == null || _selectedCharacterId == null)
			return new StartRunResponse { Success = false, Error = "no_character_selected" };
		if (_battle != null)
			return new StartRunResponse { Success = false, Error = "battle_in_progress" };
		var ch = _store.LoadCharacter(_accountId.Value, _selectedCharacterId.Value);
		if (ch == null) return new StartRunResponse { Success = false, Error = "character_missing" };

		// Полный restore HP/MP — "вы отдохнули в хабе перед заходом". Между
		// узлами одного забега HP/MP переносятся, но вход в подземелье — фреш.
		ch.ResetForCombat();
		if (!_store.UpdateCharacter(_accountId.Value, ch))
			return new StartRunResponse { Success = false, Error = "save_failed" };

		// Снэпшот = глубокая копия через JSON-цикл. Дешевле собственного DeepCopy,
		// безопаснее: новые поля Character'а попадут в снэпшот без правок здесь.
		var json = System.Text.Json.JsonSerializer.Serialize(ch, CharJsonOpts);
		_runSnapshot = System.Text.Json.JsonSerializer.Deserialize<CharacterData>(json, CharJsonOpts);

		// Сид забега. Гарантируем ненулевое значение (0 — sentinel "вне забега").
		do { _runSeed = _serverRng.Next(); } while (_runSeed == 0);

		Logger.Info($"[{_peer}] run started loc={r.LocationIndex} seed={_runSeed} " +
			$"snapshot weapon={_runSnapshot.Weapon?.Type ?? "(none)"}");
		return new StartRunResponse { Success = true, RunSeed = _runSeed, CharacterJson = json };
	}

	private ServerMessage HandleEndRun(EndRunRequest r)
	{
		_runSnapshot = null;
		_runSeed = 0;
		Logger.Info($"[{_peer}] run ended");
		return new EndRunResponse { Success = true };
	}

	// =======================================================================
	// Character commands
	// =======================================================================
	//
	// Делегат: applies — функция (char) → Result, где Result.Ok/Error/Value
	// идут в ответ, а character (если ok) персистится в БД и сериализуется в
	// CharacterJson. opName — короткая метка для логов.
	//
	// allowDuringRun: false → команда отвергается во время забега (equip/buy/
	// stash/forge/spend). UsePotion разрешён всегда.
	// allowDuringBattle: false → во время активного боя любая town-команда
	// отвергается. Внутри боя мутации идут через BattleAction.
	private ServerMessage RunCharacterCommand(
		string opName,
		Func<CharacterData, CharacterCommands.Result> applies,
		bool allowDuringRun = false,
		bool allowDuringBattle = false)
	{
		if (_accountId == null || _selectedCharacterId == null)
			return CommandError(CharacterCommandError.NoCharacter);
		if (!allowDuringBattle && _battle != null)
			return CommandError(CharacterCommandError.LockedInBattle);
		if (!allowDuringRun && _runSnapshot != null)
			return CommandError(CharacterCommandError.LockedInRun);

		var ch = _store.LoadCharacter(_accountId.Value, _selectedCharacterId.Value);
		if (ch == null) return CommandError(CharacterCommandError.NoCharacter);

		var result = applies(ch);
		if (!result.Ok)
		{
			Logger.Info($"[{_peer}] cmd {opName}: rejected ({result.Error})");
			// На fail возвращаем серверную копию — клиент сможет перерисовать
			// state, если до этого был рассинхрон.
			return new CharacterCommandResponse
			{
				Success = false,
				Error = result.Error,
				CharacterJson = System.Text.Json.JsonSerializer.Serialize(ch, CharJsonOpts),
				Value = 0,
			};
		}

		if (!_store.UpdateCharacter(_accountId.Value, ch))
		{
			Logger.Warn($"[{_peer}] cmd {opName}: UpdateCharacter failed");
			return CommandError("save_failed");
		}

		return new CharacterCommandResponse
		{
			Success = true,
			Error = null,
			CharacterJson = System.Text.Json.JsonSerializer.Serialize(ch, CharJsonOpts),
			Value = result.Value,
		};
	}

	private static CharacterCommandResponse CommandError(string code)
		=> new() { Success = false, Error = code, CharacterJson = null, Value = 0 };

	private ServerMessage HandleBuyItem(BuyItemRequest r)
		=> RunCharacterCommand("BuyItem", ch => CharacterCommands.BuyItem(ch, r.ItemId));

	private ServerMessage HandleSellSlot(SellSlotRequest r)
		=> RunCharacterCommand("SellSlot", ch => CharacterCommands.SellSlot(ch, r.SlotIndex));

	private ServerMessage HandleEquipFromInventory(EquipFromInventoryRequest r)
		=> RunCharacterCommand("Equip", ch => CharacterCommands.EquipFromInventory(ch, r.SlotIndex));

	private ServerMessage HandleUnequipSlot(UnequipSlotRequest r)
		=> RunCharacterCommand("Unequip", ch =>
			CharacterCommands.UnequipSlot(ch, (CharacterCommands.EquipSlotKind)r.Slot));

	// Зелья пьются и в подземелье (между боями) — единственная команда, которая
	// разрешена во время run. В бою — через BattleAction.UsePotion, не через эту.
	private ServerMessage HandleUsePotion(UsePotionRequest r)
		=> RunCharacterCommand("UsePotion", ch => CharacterCommands.UsePotion(ch, r.ItemId),
			allowDuringRun: true);

	private ServerMessage HandleDepositToStash(DepositToStashRequest r)
		=> RunCharacterCommand("Deposit", ch => CharacterCommands.DepositToStash(ch, r.SlotIndex));

	private ServerMessage HandleWithdrawFromStash(WithdrawFromStashRequest r)
		=> RunCharacterCommand("Withdraw", ch => CharacterCommands.WithdrawFromStash(ch, r.SlotIndex));

	private ServerMessage HandleForgeDismantle(ForgeDismantleRequest r)
		=> RunCharacterCommand("ForgeDismantle", ch => CharacterCommands.ForgeDismantle(ch, r.SlotIndex));

	// Forge upgrade/reroll используют серверный RNG. Клиент НЕ может предиктить
	// результирующие аффиксы — replace state после ответа.
	private ServerMessage HandleForgeUpgrade(ForgeUpgradeRequest r)
		=> RunCharacterCommand("ForgeUpgrade", ch =>
			CharacterCommands.ForgeUpgrade(ch, r.SlotIndex, MakeForgeRng()));

	private ServerMessage HandleForgeReroll(ForgeRerollRequest r)
		=> RunCharacterCommand("ForgeReroll", ch =>
			CharacterCommands.ForgeReroll(ch, r.SlotIndex, MakeForgeRng()));

	private ServerMessage HandleSpendStatPoint(SpendStatPointRequest r)
		=> RunCharacterCommand("SpendStat", ch => CharacterCommands.SpendStatPoint(ch, r.Stat));

	// Крафт E/D предметов. Серверный RNG для роллa rarity и аффиксов
	// (см. CharacterCommands.Craft). Клиент replace'ит state после ответа.
	private ServerMessage HandleCraftItem(CraftItemRequest r)
		=> RunCharacterCommand("Craft", ch =>
			CharacterCommands.Craft(ch, r.ItemId, MakeForgeRng()));

	// Промоушн грейда (E→D→C→B→A→S). Dev-режим: без требований к уровню.
	private ServerMessage HandlePromoteGrade(PromoteGradeRequest r)
		=> RunCharacterCommand("PromoteGrade", ch => CharacterCommands.PromoteGrade(ch));

	// RandomSource для одной forge-операции. Сид берётся из _serverRng — не
	// детерминирован между запусками, но это и не нужно (форж не воспроизводим).
	private RandomSource MakeForgeRng() => new(_serverRng.Next());

	// Целевой уровень dev-бэкдора (см. TopUpDevLevel). Убрать вместе с вызовом
	// в HandleSelectCharacter после теста D-grade контента.
	private const int DevTargetLevel = 40;

	// Dev-only: бустит Level персонажа до target, если он меньше. Через
	// LevelUpCharacter — статпоинты копятся как при обычном апе. Затем
	// RecomputeGrade синхронизирует Grade с новым Level и Exp обнуляется.
	// Возвращает true если что-то поменялось.
	private static bool TopUpDevLevel(CharacterData ch, int target)
	{
		if (ch.Level >= target) return false;
		while (ch.Level < target)
			ch.LevelUpCharacter();
		ch.RecomputeGrade();
		ch.Exp = 0;
		return true;
	}

	// Dev-only: добивает каждый крафт-ресурс до 100 в инвентаре, если меньше.
	// Возвращает true если что-то добавилось (требуется UpdateCharacter).
	// Использовать как короткий бэкдор на время теста крафт-системы; убрать
	// вместе с вызовом из HandleSelectCharacter.
	private static bool TopUpDevResources(CharacterData ch)
	{
		const int target = 100;
		bool changed = false;
		foreach (var res in ResourcesDB.AllResources())
		{
			int have = ch.Inventory.CountOf(res.Id);
			if (have >= target) continue;
			int need = target - have;
			if (ch.Inventory.TryAdd(res.Id, need, ResourcesDB.MaxStack))
				changed = true;
		}
		return changed;
	}

	// Индекс локации, прохождение боссa которой даёт C-грейд (см. HandleBattleAction).
	// Совпадает с порядком в GameData.LocationNames на клиенте.
	private const int TrialLocationIndex = 8;

	// Детерминированный вывод seed'а боя из runSeed + nodeId. Простая mix-функция
	// (multiplicative hash с золотым отношением 0x9E3779B9). Достаточно для
	// разнообразия RNG-потоков между узлами одного забега. Server-only: клиент
	// получает готовое значение в BattleStartedResponse.
	private static int DeriveBattleSeed(int runSeed, int nodeId)
	{
		unchecked
		{
			uint h = (uint)runSeed * 0x9E3779B9u;
			h ^= (uint)nodeId + 0x9E3779B9u + (h << 6) + (h >> 2);
			return (int)h;
		}
	}

	private ServerMessage HandleDeleteCharacter(DeleteCharacterRequest r)
	{
		bool deleted = _store.DeleteCharacter(_accountId.Value, r.CharacterId);
		if (!deleted) return new DeleteCharacterResponse { Success = false, Error = "not_found" };
		Logger.Info($"[{_peer}] deleted char id={r.CharacterId}");
		return new DeleteCharacterResponse { Success = true };
	}

	private ServerMessage UnauthorizedReply(ClientMessage msg)
	{
		Logger.Warn($"[{_peer}] unauthorized {msg.GetType().Name}");
		return new ServerError { Code = "unauthorized", Message = "auth required" };
	}

	private ServerMessage UnknownReply(ClientMessage msg)
	{
		Logger.Warn($"[{_peer}] unknown {msg.GetType().Name}");
		return new ServerError { Code = "unknown_message", Message = msg.GetType().Name };
	}

	// =======================================================================
	// Wire
	// =======================================================================

	private Task SendAsync(ServerMessage msg)
		=> Codec.SendAsync(_stream, msg, _shutdown);

	private async Task TrySendErrorAsync(string code, string message)
	{
		try
		{
			await Codec.SendAsync(_stream, new ServerError { Code = code, Message = message }, _shutdown)
				.ConfigureAwait(false);
		}
		catch
		{
			// Поток уже мёртв — норма.
		}
	}

	private static string ErrorString(AccountStore.CreateResult r) => r switch
	{
		AccountStore.CreateResult.LoginTaken    => "login_taken",
		AccountStore.CreateResult.EmailTaken    => "email_taken",
		AccountStore.CreateResult.WeakPassword  => "weak_password",
		AccountStore.CreateResult.InvalidLogin  => "invalid_login",
		AccountStore.CreateResult.InvalidEmail  => "invalid_email",
		_ => "unknown",
	};

	private static string ErrorString(AccountStore.CreateCharResult r) => r switch
	{
		AccountStore.CreateCharResult.SlotsFull    => "slots_full",
		AccountStore.CreateCharResult.InvalidStats => "invalid_stats",
		_ => "unknown",
	};
}

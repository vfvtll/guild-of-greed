using System;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using GuildOfGreed.Shared.Auth;
using GuildOfGreed.Shared.Combat;
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
	public const int MinSupportedClientVersion = 1;

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
				LogoutRequest r           => HandleLogout(r),
				ListCharactersRequest r   => HandleListCharacters(r),
				CreateCharacterRequest r  => HandleCreateCharacter(r),
				SelectCharacterRequest r  => HandleSelectCharacter(r),
				DeleteCharacterRequest r  => HandleDeleteCharacter(r),
				StartBattleRequest r      => HandleStartBattle(r),
				BattleActionRequest r     => HandleBattleAction(r),
				GetBattleStateRequest r   => HandleGetBattleState(r),
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
		ch.ResolveEquipment();
		if (ch.CurrentHp <= 0 || ch.CurrentMp <= 0)
		{
			ch.ResetForCombat();
			_store.UpdateCharacter(_accountId.Value, ch);
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

		character.ResolveEquipment();   // тянем Weapon/Armor по EquippedXxxId — нужно для расчётов.

		var nodeType = (MapNodeType)r.NodeType;
		var enemies = EnemyData.SpawnFor(r.LocationIndex, nodeType);
		var deck = CardsDB.DeckFor(character);
		int seed = _serverRng.Next();

		_battle = new BattleSession(character, enemies, deck, seed, nodeType);
		Logger.Info($"[{_peer}] battle started loc={r.LocationIndex} node={r.NodeType} " +
			$"seed={seed} enemies={enemies.Count}");

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

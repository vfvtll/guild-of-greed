using System;
using System.Collections.Generic;
using MessagePack;

namespace GuildOfGreed.Shared.Net;

// Все wire-сообщения протокола. Сгруппированы в два полиморфных дерева:
//   ClientMessage — то, что отправляет клиент.
//   ServerMessage — то, что отправляет сервер.
//
// MessagePack [Union] даёт type-safe дискриминатор: при добавлении нового
// сообщения нужно вписать его сюда — компилятор и сериализатор увидят
// рассинхрон, если забыть.
//
// Правила:
//   - Каждое поле помечено [Key(N)]. N не переиспользовать после удаления —
//     это ломает совместимость со старыми клиентами.
//   - Любое изменение здесь обязано бампать ProtocolVersion.Current.
//   - PasswordHash на проводе НЕ передаётся: клиент шлёт открытый пароль
//     по TLS-каналу (см. Инкремент 2), сервер сам хеширует.

// === Client → Server ===========================================================

[Union(0, typeof(ClientHello))]
[Union(1, typeof(RegisterRequest))]
[Union(2, typeof(LoginRequest))]
[Union(3, typeof(ResumeSessionRequest))]
[Union(4, typeof(LogoutRequest))]
[Union(5, typeof(ListCharactersRequest))]
[Union(6, typeof(CreateCharacterRequest))]
[Union(7, typeof(SelectCharacterRequest))]
[Union(8, typeof(DeleteCharacterRequest))]
[Union(9, typeof(StartBattleRequest))]
[Union(10, typeof(BattleActionRequest))]
[Union(11, typeof(GetBattleStateRequest))]
[Union(12, typeof(PushCharacterRequest))]
[Union(13, typeof(StartRunRequest))]
[Union(14, typeof(EndRunRequest))]
public abstract class ClientMessage { }

// Самое первое сообщение в сессии. Если ProtocolVersion несовместим, сервер
// шлёт ServerWelcome { Compatible = false } и закрывает соединение.
[MessagePackObject]
public class ClientHello : ClientMessage
{
	[Key(0)] public int ProtocolVersion;
	[Key(1)] public string ClientBuild;     // Свободный текст версии клиента (для логов сервера).
}

[MessagePackObject]
public class RegisterRequest : ClientMessage
{
	[Key(0)] public string Login;
	[Key(1)] public string Email;
	[Key(2)] public string Password;        // Открытый текст; летит по TLS.
}

[MessagePackObject]
public class LoginRequest : ClientMessage
{
	[Key(0)] public string Login;           // Только login (не email — email только для восстановления).
	[Key(1)] public string Password;
}

[MessagePackObject]
public class ResumeSessionRequest : ClientMessage
{
	[Key(0)] public string Token;
}

[MessagePackObject]
public class LogoutRequest : ClientMessage { }

[MessagePackObject]
public class ListCharactersRequest : ClientMessage { }

[MessagePackObject]
public class CreateCharacterRequest : ClientMessage
{
	[Key(0)] public string CharacterName;
	[Key(1)] public int Str;
	[Key(2)] public int Int;
	[Key(3)] public int Con;
	[Key(4)] public int Wit;
	[Key(5)] public int Men;
	[Key(6)] public int Dex;
}

[MessagePackObject]
public class SelectCharacterRequest : ClientMessage
{
	[Key(0)] public Guid CharacterId;
}

[MessagePackObject]
public class DeleteCharacterRequest : ClientMessage
{
	[Key(0)] public Guid CharacterId;
}

// Старт боя. Клиент сообщает только координаты узла на карте — список врагов
// и стартовую колоду сервер вычисляет сам через EnemyData.SpawnFor / CardsDB.DeckFor.
// Так обе стороны заведомо в синхроне и анти-чит не зависит от того что прислал клиент.
[MessagePackObject]
public class StartBattleRequest : ClientMessage
{
	[Key(0)] public int LocationIndex;
	[Key(1)] public int NodeType;       // (int)MapNodeType
	// Key(2) был LockedDeck — удалён в protocol v12. Сервер сам строит колоду
	// из своего _runSnapshot (см. StartRunRequest); клиент колоду не присылает.
}

// Одно действие игрока в активном бою. Маппится на BattleAction в shared/Combat.
// Сервер прокатывает через идентичный CombatEngine с тем же seed что выдал клиенту.
[MessagePackObject]
public class BattleActionRequest : ClientMessage
{
	[Key(0)] public int ActionType;          // (int)BattleActionType
	[Key(1)] public int HandIndex = -1;
	[Key(2)] public int TargetEnemyIndex = -1;
	[Key(3)] public string PotionId;
}

// Просит полное состояние активного боя — клиент пересоберёт свой BattleState
// из ответа. Используется при rejection (Confirmed=false): вместо kick-out
// в LocationSelect клиент берёт авторитетную копию и продолжает играть.
[MessagePackObject]
public class GetBattleStateRequest : ClientMessage { }

// Полный JSON CharacterData → сохраняется в БД. Клиент шлёт после ЛЮБОЙ
// локальной мутации (equip/unequip, покупка/продажа, стэш, очки статов),
// чтобы DB всегда отражала актуальное состояние и логин с другого устройства
// показывал ту же экипировку и деньги. ВРЕМЕННО доверяем JSON от клиента —
// валидация серверная появится в отдельном инкременте.
[MessagePackObject]
public class PushCharacterRequest : ClientMessage
{
	[Key(0)] public string CharacterJson;
}

// Старт забега: сервер снимает снэпшот текущего DB-состояния персонажа
// (для расчёта колоды через CardsDB.DeckFor). Снэпшот живёт на Session
// до EndRunRequest. Во время run все StartBattleRequest используют
// этот снэпшот для построения колоды — никакие мутации мид-ран
// (например, ап оружия) на колоду не влияют. Клиент обязан вызвать
// PushCharacterRequest ДО этого RPC, чтобы DB содержала актуальное
// состояние (HP/MP/equip).
[MessagePackObject]
public class StartRunRequest : ClientMessage
{
	[Key(0)] public int LocationIndex;
}

// Завершение забега: сервер сбрасывает _runSnapshot. Дальше StartBattleRequest
// будет считать колоду по живому персонажу (туториал-режим или новый run).
[MessagePackObject]
public class EndRunRequest : ClientMessage { }

// === Server → Client ===========================================================

[Union(0, typeof(ServerWelcome))]
[Union(1, typeof(RegisterResponse))]
[Union(2, typeof(LoginResponse))]
[Union(3, typeof(ResumeSessionResponse))]
[Union(4, typeof(LogoutResponse))]
[Union(5, typeof(ListCharactersResponse))]
[Union(6, typeof(CreateCharacterResponse))]
[Union(7, typeof(SelectCharacterResponse))]
[Union(8, typeof(DeleteCharacterResponse))]
[Union(9, typeof(ServerError))]
[Union(10, typeof(BattleStartedResponse))]
[Union(11, typeof(BattleActionResponse))]
[Union(12, typeof(BattleStateResponse))]
[Union(13, typeof(PushCharacterResponse))]
[Union(14, typeof(StartRunResponse))]
[Union(15, typeof(EndRunResponse))]
public abstract class ServerMessage { }

[MessagePackObject]
public class ServerWelcome : ServerMessage
{
	[Key(0)] public bool Compatible;
	[Key(1)] public int ServerProtocolVersion;
	[Key(2)] public int MinSupportedClientVersion;
	[Key(3)] public string UpdateUrl;        // Куда вести игрока обновляться (пусто на dev).
}

[MessagePackObject]
public class RegisterResponse : ServerMessage
{
	[Key(0)] public bool Success;
	[Key(1)] public string Token;            // Если успех — выдаём сразу сессионный токен.
	[Key(2)] public Guid AccountId;
	[Key(3)] public string Error;            // "login_taken" / "email_taken" / "weak_password" / ...
}

[MessagePackObject]
public class LoginResponse : ServerMessage
{
	[Key(0)] public bool Success;
	[Key(1)] public string Token;
	[Key(2)] public Guid AccountId;
	[Key(3)] public string Error;            // "invalid_credentials" / "account_locked" / ...
}

[MessagePackObject]
public class ResumeSessionResponse : ServerMessage
{
	[Key(0)] public bool Success;
	[Key(1)] public Guid AccountId;
	[Key(2)] public string Login;            // Чтобы клиент не угадывал чьим именем подписан.
	[Key(3)] public string Error;            // "expired" / "unknown" / ...
}

[MessagePackObject]
public class LogoutResponse : ServerMessage
{
	[Key(0)] public bool Success;
}

[MessagePackObject]
public class ListCharactersResponse : ServerMessage
{
	[Key(0)] public List<CharacterSummary> Characters;
}

// Лёгкая выжимка персонажа для экрана выбора (не тащим весь Inventory с собой).
[MessagePackObject]
public class CharacterSummary
{
	[Key(0)] public Guid Id;
	[Key(1)] public string Name;
	[Key(2)] public int Level;
	[Key(3)] public string Grade;
}

[MessagePackObject]
public class CreateCharacterResponse : ServerMessage
{
	[Key(0)] public bool Success;
	[Key(1)] public Guid CharacterId;
	[Key(2)] public string Error;            // "name_taken" / "slots_full" / "invalid_stats" / ...
}

[MessagePackObject]
public class SelectCharacterResponse : ServerMessage
{
	[Key(0)] public bool Success;
	[Key(1)] public string Error;
	// JSON CharacterData выбранного персонажа. Кладём в этот ответ чтобы
	// клиенту не делать второй round-trip за полным состоянием. Пусто при Success=false.
	[Key(2)] public string CharacterJson;
}

[MessagePackObject]
public class DeleteCharacterResponse : ServerMessage
{
	[Key(0)] public bool Success;
	[Key(1)] public string Error;
}

// Универсальный ответ при невалидном/неожиданном сообщении (а не reply на запрос).
// Закрывает сессию: сервер обычно отправит ServerError и сразу разорвёт соединение.
[MessagePackObject]
public class ServerError : ServerMessage
{
	[Key(0)] public string Code;             // "protocol_violation" / "internal_error" / ...
	[Key(1)] public string Message;
}

// Подтверждение старта боя. Seed используется обеими сторонами для
// инициализации RandomSource в BattleState — гарантирует синхрон.
[MessagePackObject]
public class BattleStartedResponse : ServerMessage
{
	[Key(0)] public bool Success;
	[Key(1)] public int Seed;
	[Key(2)] public string Error;
}

// Ответ на действие игрока. Confirmed=true → клиент применил оптимистично
// корректно. Confirmed=false → рассинхрон или невалидное действие; клиент
// шлёт GetBattleStateRequest, перестраивает state из ответа и продолжает.
//
// BattleEnded флаг — сервер увидел исход (победа/смерть/бегство) и уже
// сохранил character_json. Клиенту достаточно вызвать CombatExitRequested.
[MessagePackObject]
public class BattleActionResponse : ServerMessage
{
	[Key(0)] public bool Confirmed;
	[Key(1)] public string Error;
	[Key(2)] public bool BattleEnded;
	[Key(3)] public bool Victory;
}

// Серверный snapshot активного боя. Передаются:
//   - PlayerJson / EnemiesJson — JSON-сериализация POCO (через System.Text.Json
//     с IncludeFields), потому что [MessagePackObject] на CharacterData/EnemyData
//     не хочется навешивать ради этого случая.
//   - RngCalls — сколько раз серверный RandomSource был вызван; клиент
//     пересоздаёт свой RandomSource(seed) и прокручивает его столько же раз
//     через AdvanceTo, чтобы оба RNG-потока снова совпали.
[MessagePackObject]
public class BattleStateResponse : ServerMessage
{
	[Key(0)] public bool Success;
	[Key(1)] public string Error;
	[Key(2)] public string PlayerJson;
	[Key(3)] public string EnemiesJson;
	[Key(4)] public System.Collections.Generic.List<string> Deck;
	[Key(5)] public System.Collections.Generic.List<string> Hand;
	[Key(6)] public System.Collections.Generic.List<string> Discard;
	[Key(7)] public int TurnCount;
	[Key(8)] public int Seed;
	[Key(9)] public int RngCalls;
	[Key(10)] public bool CombatOver;
	[Key(11)] public bool Victory;
}

[MessagePackObject]
public class PushCharacterResponse : ServerMessage
{
	[Key(0)] public bool Success;
	[Key(1)] public string Error;
}

[MessagePackObject]
public class StartRunResponse : ServerMessage
{
	[Key(0)] public bool Success;
	[Key(1)] public string Error;
}

[MessagePackObject]
public class EndRunResponse : ServerMessage
{
	[Key(0)] public bool Success;
	[Key(1)] public string Error;
}

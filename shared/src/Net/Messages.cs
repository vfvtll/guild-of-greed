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

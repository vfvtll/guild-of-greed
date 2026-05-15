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
// Union(12) был PushCharacterRequest — удалён в protocol v15. Все мутации
// персонажа теперь идут как отдельные RPC ниже (CharacterCommand*). Номер
// 12 НЕ переиспользуем — иначе старые клиенты могут случайно совпасть.
[Union(13, typeof(StartRunRequest))]
[Union(14, typeof(EndRunRequest))]
[Union(15, typeof(BuyItemRequest))]
[Union(16, typeof(SellSlotRequest))]
[Union(17, typeof(EquipFromInventoryRequest))]
[Union(18, typeof(UnequipSlotRequest))]
[Union(19, typeof(UsePotionRequest))]
[Union(20, typeof(DepositToStashRequest))]
[Union(21, typeof(WithdrawFromStashRequest))]
[Union(22, typeof(ForgeDismantleRequest))]
[Union(23, typeof(ForgeUpgradeRequest))]
[Union(24, typeof(ForgeRerollRequest))]
[Union(25, typeof(SpendStatPointRequest))]
[Union(26, typeof(CraftItemRequest))]
[Union(27, typeof(PromoteGradeRequest))]
[Union(28, typeof(RespecStatsRequest))]
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
	// Итоговые статы (база + 10 распределённых очков).
	[Key(1)] public int Str;
	[Key(2)] public int Int;
	[Key(3)] public int Con;
	[Key(4)] public int Wit;
	[Key(5)] public int Men;
	[Key(6)] public int Dex;
	// База до раздачи (35..45 рандом). Нужна серверу чтобы сохранить её в
	// CharacterData.BaseXxx — для будущего респека в Гильдии.
	[Key(7)] public int BaseStr;
	[Key(8)] public int BaseInt;
	[Key(9)] public int BaseCon;
	[Key(10)] public int BaseWit;
	[Key(11)] public int BaseMen;
	[Key(12)] public int BaseDex;
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
	// Id узла карты в текущем забеге — нужен серверу для детерминированного
	// вывода битвенного seed'а из (runSeed, nodeId). -1 для туториал-боёв
	// и любых случаев вне забега.
	[Key(3)] public int NodeId = -1;
	// ID-ы активных эффектов забега (RunMap.ActiveEffects). Сервер использует
	// их при построении BattleState — применяет в CombatEngine. null/пусто
	// для туториал-боёв.
	[Key(4)] public List<string> ActiveRunEffects;
	// ID-ы активных артефактов забега (RunMap.ActiveArtifacts). Сервер
	// резолвит через ArtifactsDB.Get и передаёт в CombatEngine как Artifact-список.
	[Key(5)] public List<string> ActiveArtifacts;
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

// Старт забега: сервер сам делает ResetForCombat (полное HP/MP), снимает
// снэпшот персонажа (для расчёта колоды через CardsDB.DeckFor). Снэпшот живёт
// на Session до EndRunRequest. Во время run все StartBattleRequest используют
// этот снэпшот для построения колоды — никакие мутации мид-ран не влияют.
// Клиенту больше не нужно пушить персонажа перед StartRun — мутации шли
// через CharacterCommand* RPC и БД уже актуальна.
[MessagePackObject]
public class StartRunRequest : ClientMessage
{
	[Key(0)] public int LocationIndex;
}

// Завершение забега: сервер сбрасывает _runSnapshot. Дальше StartBattleRequest
// будет считать колоду по живому персонажу (туториал-режим или новый run).
[MessagePackObject]
public class EndRunRequest : ClientMessage { }

// === Character commands (anti-cheat: каждая мутация — отдельный RPC) =========
//
// До v15 клиент шлёт полный CharacterData JSON в PushCharacterRequest, сервер
// слепо принимал. С v15 любая мутация инвентаря/экипа/денег/стэша/кузницы —
// отдельная команда; сервер хранит persistent state в БД, применяет команду
// сам через CharacterCommands из shared, отвечает CharacterCommandResponse
// с авторитетным CharacterJson. Клиент replace'ит свой Character копией с
// сервера.

[MessagePackObject]
public class BuyItemRequest : ClientMessage
{
	[Key(0)] public string ItemId;
}

[MessagePackObject]
public class SellSlotRequest : ClientMessage
{
	[Key(0)] public int SlotIndex;
}

[MessagePackObject]
public class EquipFromInventoryRequest : ClientMessage
{
	[Key(0)] public int SlotIndex;
}

[MessagePackObject]
public class UnequipSlotRequest : ClientMessage
{
	[Key(0)] public int Slot;          // (int)CharacterCommands.EquipSlotKind
}

[MessagePackObject]
public class UsePotionRequest : ClientMessage
{
	[Key(0)] public string ItemId;
}

[MessagePackObject]
public class DepositToStashRequest : ClientMessage
{
	[Key(0)] public int SlotIndex;     // индекс в Inventory
}

[MessagePackObject]
public class WithdrawFromStashRequest : ClientMessage
{
	[Key(0)] public int SlotIndex;     // индекс в Stash
}

[MessagePackObject]
public class ForgeDismantleRequest : ClientMessage
{
	[Key(0)] public int SlotIndex;
}

[MessagePackObject]
public class ForgeUpgradeRequest : ClientMessage
{
	[Key(0)] public int SlotIndex;
}

[MessagePackObject]
public class ForgeRerollRequest : ClientMessage
{
	[Key(0)] public int SlotIndex;
}

[MessagePackObject]
public class SpendStatPointRequest : ClientMessage
{
	[Key(0)] public string Stat;       // "STR" / "INT" / "CON" / "WIT" / "MEN" / "DEX"
}

[MessagePackObject]
public class CraftItemRequest : ClientMessage
{
	[Key(0)] public string ItemId;     // base item id из ItemsDB (E/D предмет)
}

// Промоушн грейда в городе (Гильдия). Параметров нет — сервер сам решает что
// делать с текущим персонажем. В dev-режиме бесплатно и без требований.
[MessagePackObject]
public class PromoteGradeRequest : ClientMessage { }

// Респек статов в Гильдии: возвращает все распределённые очки (поверх базы)
// в UnspentStatPoints, статы откатываются на BaseXxx. Списывает цену
// (см. CharacterCommands.RespecStats — формула по Level).
[MessagePackObject]
public class RespecStatsRequest : ClientMessage { }

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
// Union(13) был PushCharacterResponse — удалён в protocol v15.
[Union(14, typeof(StartRunResponse))]
[Union(15, typeof(EndRunResponse))]
[Union(16, typeof(CharacterCommandResponse))]
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
	// Если у персонажа был активный бой (например, сервер крашился или игрок
	// разорвал соединение в середине боя) — здесь JSON BattleState + метаданные.
	// Клиент при HasActiveBattle=true переходит сразу в Combat в режиме resume
	// вместо обычного LocationSelect/Map. См. A1 в tasks.md.
	[Key(3)] public bool HasActiveBattle;
	[Key(4)] public string ActiveBattleJson;
	[Key(5)] public int ActiveBattleNodeType;
	[Key(6)] public int ActiveBattleLocationIndex;
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
public class StartRunResponse : ServerMessage
{
	[Key(0)] public bool Success;
	[Key(1)] public string Error;
	// Сид забега — определяет геометрию RunMap и все боевые RNG-потоки.
	// Клиент использует его в MapGenerator.Generate. Все StartBattleRequest
	// внутри забега получают на сервере battleSeed = deriveFrom(RunSeed, NodeId).
	[Key(2)] public int RunSeed;
	// Авторитетная копия персонажа после server-side ResetForCombat. Клиент
	// клобберит свой Character этой копией, чтобы HP/MP и runtime-флаги
	// гарантированно совпадали с серверным снэпшотом забега.
	[Key(3)] public string CharacterJson;
}

// Универсальный ответ на любую CharacterCommand* (Buy/Sell/Equip/Unequip/Use/
// Stash/Forge/SpendStat). Success=true — клиент должен заменить свой Character
// на десериализованный CharacterJson, чтобы гарантировать синхрон с сервером
// (включая аффиксы при rolling-операциях). Value — опциональное значение для
// команд с numeric result (ForgeDismantle yield, SellSlot price).
[MessagePackObject]
public class CharacterCommandResponse : ServerMessage
{
	[Key(0)] public bool Success;
	[Key(1)] public string Error;          // см. CharacterCommandError
	[Key(2)] public string CharacterJson;  // полный авторитетный state (пусто при Success=false)
	[Key(3)] public long Value;            // dismantle yield / sold price; 0 если нет
}

[MessagePackObject]
public class EndRunResponse : ServerMessage
{
	[Key(0)] public bool Success;
	[Key(1)] public string Error;
}

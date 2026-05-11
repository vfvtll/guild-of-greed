using System.Collections.Generic;
using GuildOfGreed.Shared.Domain;

namespace GuildOfGreed.Shared.Combat;

// Полное состояние активного боя. Идентичная копия живёт на клиенте и на
// сервере; CSP-логика поддерживает их в синхроне через те же BattleAction
// и идентичную последовательность Rng.
//
// Player — это та же CharacterData, что лежит в GameData. CombatEngine
// мутирует CurrentHp/CurrentMp/CurrentBlock/Effects/AttacksSinceLastCrit
// напрямую. После BattleEnded итог сохраняется на сервере в БД.
public class BattleState
{
	public CharacterData Player;
	public List<EnemyData> Enemies = new();

	public List<string> Deck = new();
	public List<string> Hand = new();
	public List<string> Discard = new();

	public int TurnCount;
	public bool CombatOver;
	public bool Victory;

	// Per-battle seed. Сервер выдаёт его в BattleStarted; клиент использует
	// для своего Rng. RngState и сам объект Rng отдельно — для удобства
	// сериализации (если когда-нибудь будем восстанавливать бой после
	// reconnect, сериализуем Seed + counter).
	public int Seed;
	public RandomSource Rng;
}

using System.Collections.Generic;
using GuildOfGreed.Shared.Combat;
using GuildOfGreed.Shared.Domain;

namespace GuildOfGreed.Server;

// Серверная in-memory сессия боя одного игрока. Тонкая обёртка над
// CombatEngine: держит BattleState + резолвленый CharacterData. Создаётся
// при HandleStartBattle и существует до BattleEnded.
//
// CSP-контракт: client использует тот же CombatEngine с тем же seed —
// результаты Apply должны полностью совпадать. Server не возвращает события
// клиенту, он лишь сообщает Confirmed/NotConfirmed и BattleEnded.
public class BattleSession
{
	public BattleState State { get; }
	public CharacterData Character => State.Player;
	// Запоминаем тип узла, чтобы при ended+victory сервер мог отличить
	// стартовый бой (Tutorial) от обычного и сбросить IsNewCharacter.
	public MapNodeType NodeType { get; }

	public BattleSession(CharacterData player, List<EnemyData> enemies, List<string> deck,
		int seed, MapNodeType nodeType)
	{
		var (state, _) = CombatEngine.StartBattle(player, enemies, deck, seed);
		State = state;
		NodeType = nodeType;
	}

	public List<BattleEvent> ApplyAction(BattleAction action)
		=> CombatEngine.Apply(State, action);
}

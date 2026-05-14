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

	// Счётчик атакующих маг.заклинаний, сыгранных в ТЕКУЩЕМ ходу игрока.
	// Используется пассивом посоха (WeaponPassive.MagicChain) — каждое
	// следующее даёт +Magnitude% урона и +Magnitude2% маны. Reset в
	// CombatEngine.BeginPlayerTurn.
	public int SpellsCastThisTurn;

	// Per-battle seed. Сервер выдаёт его в BattleStarted; клиент использует
	// для своего Rng. RngState и сам объект Rng отдельно — для удобства
	// сериализации (если когда-нибудь будем восстанавливать бой после
	// reconnect, сериализуем Seed + counter).
	public int Seed;
	public RandomSource Rng;

	// Снэпшот уровней ДО начала боя — чтобы при resolve в BattleEnded
	// корректно эмитить CharacterLevelUp / WeaponLevelUp только по новым
	// уровням. Заполняется в CombatEngine.StartBattle.
	public int PreBattleCharacterLevel;
	public Dictionary<string, int> PreBattleWeaponLevels = new();

	// Активные эффекты подземелья. Скопированы из RunMap.ActiveEffects на старте
	// боя и применяются движком (bleed_all_per_turn, all_dmg_pct, weapon_dmg_pct).
	// Стандарт: bleed добавляется в BeginPlayerTurn; damage-pct применяется
	// в исходящем уроне обеих сторон.
	public List<RunEffect> RunEffects = new();

	// Артефакты забега. В отличие от RunEffects действуют только на игрока и
	// всегда положительны. Скопированы из RunMap.ActiveArtifacts на старте боя.
	// Применение — см. CombatEngine.ApplyArtifact* и BeginPlayerTurn.
	public List<Artifact> Artifacts = new();
}

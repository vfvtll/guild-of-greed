namespace GuildOfGreed.Shared.Combat;

// Действия игрока, которые перетекут в bull-wire-сообщения протокола.
// Клиент создаёт BattleAction → передаёт в CombatEngine.Apply → получает
// список BattleEvent → визуализирует. Параллельно шлёт ту же BattleAction на
// сервер, сервер запускает тот же Apply и сверяет полученные events.
public enum BattleActionType
{
	PlayCard,
	UsePotion,
	EndTurn,
	Flee,
}

public class BattleAction
{
	public BattleActionType Type;

	// PlayCard:
	public int HandIndex = -1;          // позиция карты в руке.
	public int TargetEnemyIndex = -1;   // -1 если карта не таргетная или AOE.

	// UsePotion:
	public string PotionId;
}

using GuildOfGreed.Shared.Combat;

namespace GuildOfGreed.Server;

// Серверная обёртка над BattleSnapshotDto: превращает живую BattleSession
// в JSON (для записи в active_battles SQLite) и обратно. Сам DTO живёт в
// shared — тот же тип десериализуется на клиенте при resume.
public static class BattleSnapshot
{
	public static string Serialize(BattleSession session)
	{
		var dto = new BattleSnapshotDto
		{
			State = session.State,
			NodeType = session.NodeType,
			LocationIndex = session.LocationIndex,
			RngCalls = session.State.Rng?.Calls ?? 0,
		};
		dto.CaptureFromPlayer();
		return BattleSnapshotDto.ToJson(dto);
	}

	public static BattleSession Deserialize(string json)
	{
		var dto = BattleSnapshotDto.FromJson(json);
		if (dto?.State == null) return null;
		dto.RestoreRng();
		dto.RestoreToPlayer();
		return BattleSession.FromSnapshot(dto.State, dto.NodeType, dto.LocationIndex);
	}
}

using System;
using System.Text.Json;
using Godot;

// Сохранённая сессия для auto-login. Хранится в user://auth.json.
// Token не шифруем (любой с доступом к user-данным может его прочитать),
// но и сейв персонажа не шифровали — на прототипе единая модель доверия
// к локальной машине игрока.
public static class AuthPrefs
{
	private const string Path = "user://auth.json";

	public class Data
	{
		public string Token { get; set; } = "";
		public string AccountId { get; set; } = "";
		public string Login { get; set; } = "";
	}

	public static bool HasSession() => FileAccess.FileExists(Path) && !string.IsNullOrEmpty(Load().Token);

	public static Data Load()
	{
		try
		{
			if (!FileAccess.FileExists(Path)) return new Data();
			using var file = FileAccess.Open(Path, FileAccess.ModeFlags.Read);
			if (file == null) return new Data();
			return JsonSerializer.Deserialize<Data>(file.GetAsText()) ?? new Data();
		}
		catch
		{
			return new Data();
		}
	}

	public static void Save(string token, Guid accountId, string login)
	{
		try
		{
			var data = new Data
			{
				Token = token,
				AccountId = accountId.ToString(),
				Login = login,
			};
			using var file = FileAccess.Open(Path, FileAccess.ModeFlags.Write);
			file?.StoreString(JsonSerializer.Serialize(data));
		}
		catch (Exception ex)
		{
			GD.PrintErr($"AuthPrefs.Save: {ex.Message}");
		}
	}

	public static void Clear()
	{
		if (!FileAccess.FileExists(Path)) return;
		try { DirAccess.RemoveAbsolute(Path); }
		catch (Exception ex) { GD.PrintErr($"AuthPrefs.Clear: {ex.Message}"); }
	}
}

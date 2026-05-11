using System;
using System.Text.Json;
using Godot;

// Сетевые настройки клиента: куда подключаться. Сохраняются в user://net.json,
// чтобы dev-машина могла указать кастомный хост/порт без пересборки.
public static class NetPrefs
{
	public const string DefaultHost = "127.0.0.1";
	public const int DefaultPort = 5870;
	private const string Path = "user://net.json";

	private class Data
	{
		public string Host { get; set; } = DefaultHost;
		public int Port { get; set; } = DefaultPort;
	}

	public static string Host => Load().Host;
	public static int Port => Load().Port;

	public static void Save(string host, int port)
	{
		try
		{
			var json = JsonSerializer.Serialize(new Data { Host = host, Port = port });
			using var file = FileAccess.Open(Path, FileAccess.ModeFlags.Write);
			file?.StoreString(json);
		}
		catch (Exception ex)
		{
			GD.PrintErr($"NetPrefs.Save: {ex.Message}");
		}
	}

	private static Data Load()
	{
		try
		{
			if (!FileAccess.FileExists(Path)) return new Data();
			using var file = FileAccess.Open(Path, FileAccess.ModeFlags.Read);
			if (file == null) return new Data();
			var json = file.GetAsText();
			return JsonSerializer.Deserialize<Data>(json) ?? new Data();
		}
		catch
		{
			return new Data();
		}
	}
}

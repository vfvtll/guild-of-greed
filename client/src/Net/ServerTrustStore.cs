using System.Collections.Generic;
using System.Text.Json;
using Godot;

// TOFU (Trust-On-First-Use) pinning сертификата сервера.
//
// Поведение клиента:
//   1. CA-signed cert (chain valid) — доверяем без записи в стор.
//   2. Self-signed cert и нет записи для host:port — TOFU: сохраняем thumbprint
//      и пропускаем. Подразумевается, что первое подключение происходит в
//      доверенной среде (dev-машина / installer).
//   3. Self-signed cert, есть запись и thumbprint совпадает — пропускаем.
//   4. Self-signed cert, thumbprint не совпадает — отказ. Пользователь должен
//      явно сбросить доверие (ConnectingView.ResetTrustButton).
//
// Файл: user://server_trust.json
// Формат: { "host:port": "SHA-1 thumbprint без пробелов и в верхнем регистре" }
public static class ServerTrustStore
{
	private const string Path = "user://server_trust.json";

	public static string Get(string host, int port)
	{
		var all = LoadAll();
		return all.TryGetValue(Key(host, port), out var t) ? t : null;
	}

	public static void Set(string host, int port, string thumbprint)
	{
		var all = LoadAll();
		all[Key(host, port)] = Normalise(thumbprint);
		Save(all);
	}

	public static void Clear(string host, int port)
	{
		var all = LoadAll();
		if (all.Remove(Key(host, port))) Save(all);
	}

	public static bool Matches(string host, int port, string thumbprint)
	{
		var pinned = Get(host, port);
		return pinned != null && string.Equals(pinned, Normalise(thumbprint),
			System.StringComparison.OrdinalIgnoreCase);
	}

	private static string Key(string host, int port) => $"{host}:{port}";

	private static string Normalise(string t) => (t ?? "").Replace(" ", "").Replace(":", "").ToUpperInvariant();

	private static Dictionary<string, string> LoadAll()
	{
		try
		{
			if (!FileAccess.FileExists(Path)) return new();
			using var file = FileAccess.Open(Path, FileAccess.ModeFlags.Read);
			if (file == null) return new();
			var json = file.GetAsText();
			return JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new();
		}
		catch
		{
			return new();
		}
	}

	private static void Save(Dictionary<string, string> all)
	{
		try
		{
			using var file = FileAccess.Open(Path, FileAccess.ModeFlags.Write);
			file?.StoreString(JsonSerializer.Serialize(all));
		}
		catch (System.Exception ex)
		{
			GD.PrintErr($"ServerTrustStore.Save: {ex.Message}");
		}
	}
}

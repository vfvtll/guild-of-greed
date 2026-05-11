using Godot;
using System;
using System.Text.Json;
using GuildOfGreed.Shared.Domain;
using GuildOfGreed.Shared.Data;

// Сохранение и загрузка персонажа.
// Сейчас сохраняем в Godot user:// (локальная папка приложения) как JSON.
// На сервере: те же CharacterData будут лежать в БД, передача — через сеть в JSON.
//
// Что сохраняется:
//   - Имя, статы (STR/INT/CON/WIT/MEN), Level/Grade/Exp
// Что НЕ сохраняется (помечено [JsonIgnore] в CharacterData):
//   - Текущая экипировка (восстанавливается ApplyLoadout)
//   - Боевое состояние (HP/MP/Block/Effects) — это рантайм
public static class SaveGame
{
	private const string SavePath = "user://character.json";

	private static readonly JsonSerializerOptions Options = new()
	{
		WriteIndented = true,
		IncludeFields = true,
	};

	public static bool HasSave() => FileAccess.FileExists(SavePath);

	public static void Save(CharacterData character)
	{
		if (character == null) return;
		try
		{
			var json = JsonSerializer.Serialize(character, Options);
			using var file = FileAccess.Open(SavePath, FileAccess.ModeFlags.Write);
			if (file == null)
			{
				GD.PrintErr($"SaveGame.Save: не удалось открыть {SavePath} ({FileAccess.GetOpenError()})");
				return;
			}
			file.StoreString(json);
		}
		catch (Exception ex)
		{
			GD.PrintErr($"SaveGame.Save: {ex.Message}");
		}
	}

	public static CharacterData Load()
	{
		if (!HasSave()) return null;
		try
		{
			using var file = FileAccess.Open(SavePath, FileAccess.ModeFlags.Read);
			if (file == null) return null;
			var json = file.GetAsText();
			var ch = JsonSerializer.Deserialize<CharacterData>(json, Options);
			if (ch != null) Migrate(ch);
			return ch;
		}
		catch (Exception ex)
		{
			GD.PrintErr($"SaveGame.Load: {ex.Message}");
			return null;
		}
	}

	// Миграции для сейвов старых версий. Запускается на каждой Load.
	private static void Migrate(CharacterData ch)
	{
		// v1 → v2: добавлен DEX. Старый сейв => Dex == 0 => катаем случайно.
		if (ch.Dex == 0)
		{
			ch.Dex = Rng.Range(35, 46);
			GD.Print($"SaveGame: миграция — добавлен DEX={ch.Dex}");
		}
		// v2 → v3: добавлен Id. Старый сейв => Guid.Empty => генерим новый.
		if (ch.Id == System.Guid.Empty)
		{
			ch.Id = System.Guid.NewGuid();
			GD.Print($"SaveGame: миграция — присвоен Id={ch.Id}");
		}
	}

	public static void Delete()
	{
		if (!HasSave()) return;
		try
		{
			DirAccess.RemoveAbsolute(SavePath);
		}
		catch (Exception ex)
		{
			GD.PrintErr($"SaveGame.Delete: {ex.Message}");
		}
	}
}

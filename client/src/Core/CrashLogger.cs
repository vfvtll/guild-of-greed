using System;
using System.Threading.Tasks;
using Godot;

// Глобальный отлов исключений на клиенте. Подключается из Main._EnterTree
// один раз за процесс. Цели:
//   1. Записывать stack trace в user://crashes/crash-{ts}.log
//      (Godot user:// = ~/.local/share/.../guild-of-greed на Linux/Mac,
//       %APPDATA% на Windows).
//   2. При следующем старте находить эти файлы и показывать игроку диалог
//      «обнаружен краш в прошлый раз, отправить?». Отправка по сети —
//      отдельная задача; пока только локально.
//
// Логирует:
//   - AppDomain.UnhandledException — синхронные кидающие исключения, которые
//     не были пойманы (валит процесс)
//   - TaskScheduler.UnobservedTaskException — async-исключения, которые
//     никто не дождался await'ом (тихо съедаются Task'ом).
public static class CrashLogger
{
	private const string CrashDir = "user://crashes";
	private static bool _installed;

	public static void Install()
	{
		if (_installed) return;
		_installed = true;

		EnsureCrashDir();

		AppDomain.CurrentDomain.UnhandledException += (s, e) =>
		{
			var ex = e.ExceptionObject as Exception;
			Write("unhandled", ex);
		};

		TaskScheduler.UnobservedTaskException += (s, e) =>
		{
			Write("unobserved_task", e.Exception);
			e.SetObserved();
		};

		GD.Print("CrashLogger: installed");
	}

	// Список файлов из прошлых сессий — UI вызывает после _Ready, чтобы
	// показать диалог если что-то есть.
	public static string[] CollectPendingCrashes()
	{
		using var dir = DirAccess.Open(CrashDir);
		if (dir == null) return Array.Empty<string>();
		var files = dir.GetFiles();
		if (files == null) return Array.Empty<string>();
		Array.Sort(files);
		return files;
	}

	public static string ReadCrash(string fileName)
	{
		using var f = FileAccess.Open($"{CrashDir}/{fileName}", FileAccess.ModeFlags.Read);
		return f?.GetAsText() ?? "";
	}

	public static void DeleteCrash(string fileName)
	{
		using var dir = DirAccess.Open(CrashDir);
		dir?.Remove(fileName);
	}

	public static void DeleteAllCrashes()
	{
		using var dir = DirAccess.Open(CrashDir);
		if (dir == null) return;
		foreach (var f in dir.GetFiles()) dir.Remove(f);
	}

	private static void EnsureCrashDir()
	{
		if (!DirAccess.DirExistsAbsolute(CrashDir))
			DirAccess.MakeDirRecursiveAbsolute(CrashDir);
	}

	private static void Write(string kind, Exception ex)
	{
		try
		{
			EnsureCrashDir();
			string ts = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
			string path = $"{CrashDir}/crash-{ts}-{kind}.log";
			using var f = FileAccess.Open(path, FileAccess.ModeFlags.Write);
			if (f == null) return;
			f.StoreLine($"# Guild of Greed client crash");
			f.StoreLine($"# Kind:      {kind}");
			f.StoreLine($"# UTC:       {DateTime.UtcNow:O}");
			f.StoreLine($"# Platform:  {OS.GetName()} {OS.GetVersion()}");
			f.StoreLine($"# Protocol:  {GuildOfGreed.Shared.Net.ProtocolVersion.Current}");
			f.StoreLine("");
			f.StoreLine(ex?.ToString() ?? "(null exception)");
			GD.PrintErr($"CrashLogger: wrote {path}");
		}
		catch (Exception writeEx)
		{
			// Если даже логирование падает — выводим в Godot console,
			// дальше делать нечего, процесс всё равно умирает.
			GD.PrintErr($"CrashLogger: failed to write crash: {writeEx.Message}");
		}
	}
}

using System;
using System.IO;

namespace GuildOfGreed.Server;

// Простой logger с UTC timestamp. Выводит в console + append'ит в файл
// {dataDir}/logs/server-YYYYMMDD.log. Ротация суточная — новый файл
// открывается при смене даты UTC.
//
// Достаточно для первого теста с реальными игроками: можно вечером
// открыть логи за день и увидеть auth-failures, rate-limit hits, crashes.
// При росте сервера подменим на Serilog/Microsoft.Extensions.Logging.
public static class Logger
{
	private static readonly object _lock = new();
	private static string _logDir;
	private static string _openDate;     // "yyyy-MM-dd" текущего открытого файла
	private static StreamWriter _file;

	// Вызывается из Program.cs один раз на старте после определения dataDir.
	// Если не вызвано — Logger пишет только в console (как раньше).
	public static void ConfigureFileSink(string dataDir)
	{
		lock (_lock)
		{
			_logDir = Path.Combine(dataDir, "logs");
			Directory.CreateDirectory(_logDir);
			RollFileIfNeeded(force: true);
		}
	}

	public static void Info(string message)  => Write("INFO ", message);
	public static void Warn(string message)  => Write("WARN ", message);
	public static void Error(string message) => Write("ERROR", message);

	public static void Error(string message, Exception ex)
		=> Write("ERROR", $"{message}: {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");

	private static void Write(string level, string message)
	{
		string line = $"{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss.fff} {level} {message}";
		lock (_lock)
		{
			Console.WriteLine(line);
			if (_logDir != null)
			{
				RollFileIfNeeded(force: false);
				_file?.WriteLine(line);
				_file?.Flush();
			}
		}
	}

	// Должен вызываться под _lock.
	private static void RollFileIfNeeded(bool force)
	{
		string today = DateTime.UtcNow.ToString("yyyy-MM-dd");
		if (!force && today == _openDate) return;
		try
		{
			_file?.Dispose();
			_openDate = today;
			string path = Path.Combine(_logDir, $"server-{today}.log");
			_file = new StreamWriter(File.Open(path, FileMode.Append, FileAccess.Write, FileShare.Read))
			{
				AutoFlush = false,
			};
		}
		catch (Exception ex)
		{
			Console.WriteLine($"{DateTime.UtcNow:HH:mm:ss.fff} ERROR Logger: failed to open log file: {ex.Message}");
			_file = null;
		}
	}
}

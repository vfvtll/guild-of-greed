using System;

namespace GuildOfGreed.Server;

// Простой console-логгер с UTC timestamp. Достаточно для прототипа;
// при росте сервера подменим на Serilog/Microsoft.Extensions.Logging.
public static class Logger
{
	private static readonly object _lock = new();

	public static void Info(string message)  => Write("INFO ", message);
	public static void Warn(string message)  => Write("WARN ", message);
	public static void Error(string message) => Write("ERROR", message);

	public static void Error(string message, Exception ex)
		=> Write("ERROR", $"{message}: {ex.GetType().Name}: {ex.Message}");

	private static void Write(string level, string message)
	{
		string line = $"{DateTime.UtcNow:HH:mm:ss.fff} {level} {message}";
		lock (_lock) Console.WriteLine(line);
	}
}

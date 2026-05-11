using System;
using System.IO;

namespace GuildOfGreed.Server;

// Конфиг сервера. Все значения переопределяемы через переменные окружения,
// чтобы переменная ENV в production не требовала пересборки.
public static class NetworkConfig
{
	// TCP порт для входящих TLS-соединений.
	// Незанят системными сервисами на типичной Windows-машине.
	public const int DefaultPort = 5870;

	// Сервер слушает только локально в dev. В production — IPAddress.Any.
	public const string DefaultHost = "127.0.0.1";

	// Базовая папка для рантайм-данных (БД, сертификат). Создаётся при старте.
	private const string DataDirName = "data";

	public static int Port =>
		int.TryParse(Environment.GetEnvironmentVariable("GOG_PORT"), out var p) ? p : DefaultPort;

	public static string Host =>
		Environment.GetEnvironmentVariable("GOG_HOST") ?? DefaultHost;

	public static string DataDir
	{
		get
		{
			var dir = Environment.GetEnvironmentVariable("GOG_DATA_DIR")
				?? Path.Combine(AppContext.BaseDirectory, DataDirName);
			Directory.CreateDirectory(dir);
			return dir;
		}
	}

	public static string DatabasePath => Path.Combine(DataDir, "accounts.db");
	public static string CertificatePath => Path.Combine(DataDir, "server.pfx");
}

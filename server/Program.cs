using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using GuildOfGreed.Server;
using GuildOfGreed.Shared.Net;

// Точка входа сервера. Поднимает SQLite, загружает/создаёт TLS-сертификат
// и запускает TLS-листенер. Ctrl+C / SIGTERM — корректный shutdown.

Logger.Info($"GuildOfGreed.Server starting (protocol v{ProtocolVersion.Current})");

var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
	e.Cancel = true;       // не позволяем процессу прибиться сразу
	Logger.Info("shutdown requested");
	cts.Cancel();
};

try
{
	using var store = new AccountStore(NetworkConfig.DatabasePath);
	Logger.Info($"sqlite ready at {NetworkConfig.DatabasePath}");

	var cert = TlsCertificate.LoadOrCreate(NetworkConfig.CertificatePath);
	Logger.Info($"tls cert {cert.Subject} (thumbprint {cert.Thumbprint})");

	var host = IPAddress.Parse(NetworkConfig.Host);
	var listener = new Listener(host, NetworkConfig.Port, cert, store, cts.Token);
	await listener.RunAsync();
}
catch (Exception ex)
{
	Logger.Error("fatal", ex);
	return 1;
}

Logger.Info("server stopped cleanly");
return 0;

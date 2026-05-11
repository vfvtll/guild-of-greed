using System;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;

namespace GuildOfGreed.Server;

// TCP-листенер с TLS-апгрейдом каждого входящего соединения.
// Per connection — отдельный Task с собственной Session.
public class Listener
{
	private readonly TcpListener _tcp;
	private readonly X509Certificate2 _cert;
	private readonly AccountStore _store;
	private readonly CancellationToken _shutdown;

	public Listener(IPAddress host, int port, X509Certificate2 cert, AccountStore store, CancellationToken shutdown)
	{
		_tcp = new TcpListener(host, port);
		_cert = cert;
		_store = store;
		_shutdown = shutdown;
	}

	public async Task RunAsync()
	{
		_tcp.Start();
		Logger.Info($"listening on {_tcp.LocalEndpoint}");
		try
		{
			while (!_shutdown.IsCancellationRequested)
			{
				TcpClient client = await _tcp.AcceptTcpClientAsync(_shutdown).ConfigureAwait(false);
				_ = HandleClientAsync(client);   // fire-and-forget; ошибки ловит сама Session.
			}
		}
		catch (OperationCanceledException)
		{
			// Нормальный shutdown.
		}
		finally
		{
			_tcp.Stop();
			Logger.Info("listener stopped");
		}
	}

	private async Task HandleClientAsync(TcpClient client)
	{
		var peer = client.Client.RemoteEndPoint?.ToString() ?? "unknown";
		Logger.Info($"[{peer}] connected");

		SslStream ssl = null;
		try
		{
			ssl = new SslStream(client.GetStream(), leaveInnerStreamOpen: false);
			var options = new SslServerAuthenticationOptions
			{
				ServerCertificate = _cert,
				ClientCertificateRequired = false,
				EnabledSslProtocols = System.Security.Authentication.SslProtocols.Tls12
				                    | System.Security.Authentication.SslProtocols.Tls13,
			};
			await ssl.AuthenticateAsServerAsync(options, _shutdown).ConfigureAwait(false);

			var session = new Session(client, ssl, _store, peer, _shutdown);
			await session.RunAsync().ConfigureAwait(false);
		}
		catch (Exception ex)
		{
			Logger.Error($"[{peer}] handshake failed", ex);
			try { ssl?.Dispose(); } catch { }
			try { client.Close(); } catch { }
		}
	}
}

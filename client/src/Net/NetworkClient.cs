using System;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using Godot;
using GuildOfGreed.Shared.Net;

// Тонкий request/response клиент к серверу. Сейчас сервер не шлёт
// unsolicited-сообщений, поэтому каждый метод = одна пара send/receive.
// Когда добавятся server-push (matchmaking, game state) — заменим на
// receive-loop с CorrelationId.
//
// Поток: ConnectAsync → HandshakeAsync → (Register|Login|Resume)Async →
// после auth — Character* методы. Любая ошибка ниже подкидывается из метода
// исключением; UI ловит и показывает.
public class NetworkClient : IDisposable
{
	public string ClientBuild { get; set; } = "client-dev";

	private TcpClient _tcp;
	private SslStream _ssl;
	private string _host;
	private int _port;
	private bool _pinMismatch;

	public bool IsConnected => _tcp?.Connected == true;

	public async Task ConnectAsync(string host, int port, CancellationToken ct = default)
	{
		Dispose();
		_host = host;
		_port = port;
		_pinMismatch = false;
		_tcp = new TcpClient();
		await _tcp.ConnectAsync(host, port, ct).ConfigureAwait(false);

		_ssl = new SslStream(_tcp.GetStream(), leaveInnerStreamOpen: false, ValidateRemote);
		var options = new SslClientAuthenticationOptions
		{
			TargetHost = host,
			EnabledSslProtocols = System.Security.Authentication.SslProtocols.Tls12
			                    | System.Security.Authentication.SslProtocols.Tls13,
			RemoteCertificateValidationCallback = ValidateRemote,
		};
		try
		{
			await _ssl.AuthenticateAsClientAsync(options, ct).ConfigureAwait(false);
		}
		catch (Exception) when (_pinMismatch)
		{
			throw new TlsPinMismatchException(_host, _port);
		}
	}

	// TLS validation:
	//   1. errors == None → cert валиден по системному CA chain. Доверяем без pin.
	//   2. Иначе (self-signed / unknown CA): сверяемся со ServerTrustStore.
	//      - Нет записи → TOFU: сохраняем thumbprint, доверяем (первое подключение
	//        предполагается в безопасной среде — dev-машина / setup).
	//      - Совпадает → доверяем.
	//      - Не совпадает → отказ. Возможный MITM; пользователь должен явно
	//        сбросить доверие через ConnectingView.
	private bool ValidateRemote(object sender, System.Security.Cryptography.X509Certificates.X509Certificate cert,
		X509Chain chain, SslPolicyErrors errors)
	{
		if (errors == SslPolicyErrors.None) return true;
		if (cert is not X509Certificate2 cert2) return false;

		string thumbprint = cert2.Thumbprint;
		if (ServerTrustStore.Matches(_host, _port, thumbprint)) return true;

		var pinned = ServerTrustStore.Get(_host, _port);
		if (pinned == null)
		{
			GD.Print($"NetworkClient: TOFU-pinning self-signed cert for {_host}:{_port} thumbprint={thumbprint}");
			ServerTrustStore.Set(_host, _port, thumbprint);
			return true;
		}

		GD.PrintErr($"NetworkClient: TLS pin mismatch for {_host}:{_port} — expected {pinned}, got {thumbprint}");
		_pinMismatch = true;
		return false;
	}

	public async Task<ServerWelcome> HandshakeAsync(CancellationToken ct = default)
	{
		var welcome = await ExchangeAsync<ServerWelcome>(new ClientHello
		{
			ProtocolVersion = ProtocolVersion.Current,
			ClientBuild = ClientBuild,
		}, ct).ConfigureAwait(false);
		return welcome;
	}

	public Task<RegisterResponse> RegisterAsync(string login, string email, string password, CancellationToken ct = default)
		=> ExchangeAsync<RegisterResponse>(new RegisterRequest
		{
			Login = login, Email = email, Password = password,
		}, ct);

	public Task<LoginResponse> LoginAsync(string login, string password, CancellationToken ct = default)
		=> ExchangeAsync<LoginResponse>(new LoginRequest { Login = login, Password = password }, ct);

	public Task<ResumeSessionResponse> ResumeAsync(string token, CancellationToken ct = default)
		=> ExchangeAsync<ResumeSessionResponse>(new ResumeSessionRequest { Token = token }, ct);

	public Task<LogoutResponse> LogoutAsync(CancellationToken ct = default)
		=> ExchangeAsync<LogoutResponse>(new LogoutRequest(), ct);

	public Task<ListCharactersResponse> ListCharactersAsync(CancellationToken ct = default)
		=> ExchangeAsync<ListCharactersResponse>(new ListCharactersRequest(), ct);

	public Task<CreateCharacterResponse> CreateCharacterAsync(string name,
		int str, int @int, int con, int wit, int men, int dex, CancellationToken ct = default)
		=> ExchangeAsync<CreateCharacterResponse>(new CreateCharacterRequest
		{
			CharacterName = name,
			Str = str, Int = @int, Con = con, Wit = wit, Men = men, Dex = dex,
		}, ct);

	public Task<SelectCharacterResponse> SelectCharacterAsync(Guid characterId, CancellationToken ct = default)
		=> ExchangeAsync<SelectCharacterResponse>(new SelectCharacterRequest { CharacterId = characterId }, ct);

	public Task<DeleteCharacterResponse> DeleteCharacterAsync(Guid characterId, CancellationToken ct = default)
		=> ExchangeAsync<DeleteCharacterResponse>(new DeleteCharacterRequest { CharacterId = characterId }, ct);

	private async Task<TResponse> ExchangeAsync<TResponse>(ClientMessage request, CancellationToken ct)
		where TResponse : ServerMessage
	{
		if (_ssl == null) throw new InvalidOperationException("not connected");
		await Codec.SendAsync(_ssl, request, ct).ConfigureAwait(false);
		var reply = await Codec.ReceiveAsync<ServerMessage>(_ssl, ct).ConfigureAwait(false);
		if (reply is ServerError err)
		{
			throw new ServerException(err.Code, err.Message);
		}
		if (reply is not TResponse typed)
		{
			throw new InvalidOperationException(
				$"unexpected reply: got {reply?.GetType().Name}, expected {typeof(TResponse).Name}");
		}
		return typed;
	}

	public void Dispose()
	{
		try { _ssl?.Dispose(); } catch { }
		try { _tcp?.Dispose(); } catch { }
		_ssl = null;
		_tcp = null;
	}
}

// Server явно ответил ServerError на запрос — оборачиваем в exception
// чтобы вызывающий код мог отличить "сервер сказал нет" от транспортной ошибки.
public class ServerException : Exception
{
	public string Code { get; }
	public ServerException(string code, string message) : base(message)
	{
		Code = code;
	}
}

// TLS pin mismatch — у сервера thumbprint cert не совпадает с тем, что
// сохранён локально. Возможна MITM-атака или сервер перевыпустил сертификат.
// UI должен предложить кнопку "сбросить доверие", после чего следующий
// connect снова TOFU-pinнет.
public class TlsPinMismatchException : Exception
{
	public string Host { get; }
	public int Port { get; }
	public TlsPinMismatchException(string host, int port)
		: base($"TLS pin mismatch for {host}:{port}")
	{
		Host = host;
		Port = port;
	}
}

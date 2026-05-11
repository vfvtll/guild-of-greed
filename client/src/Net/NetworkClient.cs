using System;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
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

	public bool IsConnected => _tcp?.Connected == true;

	public async Task ConnectAsync(string host, int port, CancellationToken ct = default)
	{
		Dispose();
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
		await _ssl.AuthenticateAsClientAsync(options, ct).ConfigureAwait(false);
	}

	// На dev принимаем self-signed сертификат сервера. В production эта функция
	// должна вернуть chain.Build() && подтверждённый CN/SAN.
	private static bool ValidateRemote(object sender, X509Certificate cert,
		X509Chain chain, SslPolicyErrors errors) => true;

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

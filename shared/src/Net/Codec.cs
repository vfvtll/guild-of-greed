using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MessagePack;

namespace GuildOfGreed.Shared.Net;

// Тонкая обёртка над MessagePack: сериализация ↔ байты + асинхронные
// чтение/запись через MessageFraming. Используется и клиентом, и сервером —
// единственный источник правды о wire-формате.
//
// Resolver и компрессия не настраиваются — стандарт MessagePack 3.x
// (SerializerOptions.Standard, без LZ4) подходит для прототипа.
public static class Codec
{
	private static readonly MessagePackSerializerOptions Options =
		MessagePackSerializerOptions.Standard;

	public static byte[] Encode<T>(T message)
		=> MessagePackSerializer.Serialize(message, Options);

	public static T Decode<T>(byte[] payload)
		=> MessagePackSerializer.Deserialize<T>(payload, Options);

	public static async Task SendAsync<T>(Stream stream, T message, CancellationToken ct = default)
	{
		byte[] payload = Encode(message);
		await MessageFraming.WriteAsync(stream, payload, ct).ConfigureAwait(false);
	}

	public static async Task<T> ReceiveAsync<T>(Stream stream, CancellationToken ct = default)
	{
		byte[] payload = await MessageFraming.ReadAsync(stream, ct).ConfigureAwait(false);
		return Decode<T>(payload);
	}
}

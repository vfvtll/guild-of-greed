using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace GuildOfGreed.Shared.Net;

// TCP не сохраняет границы сообщений: один Send может склеиться с другим,
// одно сообщение может прийти в двух Receive. Мы добавляем явный фрейминг:
//   [4 байта длина BE] [N байт payload]
//
// 4 байта BigEndian (network byte order). Лимит размера сообщения — MaxFrame
// (защита от злого клиента, который пришлёт длину 2GB и переполнит буфер).
public static class MessageFraming
{
	public const int MaxFrame = 1 << 20;     // 1 MiB на сообщение — щедро для прототипа.
	public const int LengthPrefixSize = 4;

	public static async Task WriteAsync(Stream stream, byte[] payload, CancellationToken ct = default)
	{
		if (payload == null) throw new ArgumentNullException(nameof(payload));
		if (payload.Length > MaxFrame)
			throw new InvalidOperationException($"frame too large: {payload.Length} > {MaxFrame}");

		var prefix = new byte[LengthPrefixSize];
		WriteBigEndian(prefix, 0, payload.Length);
		await stream.WriteAsync(prefix.AsMemory(0, LengthPrefixSize), ct).ConfigureAwait(false);
		await stream.WriteAsync(payload.AsMemory(0, payload.Length), ct).ConfigureAwait(false);
		await stream.FlushAsync(ct).ConfigureAwait(false);
	}

	// Возвращает payload одного сообщения. Бросает EndOfStreamException если
	// соединение закрылось посреди фрейма.
	public static async Task<byte[]> ReadAsync(Stream stream, CancellationToken ct = default)
	{
		var prefix = new byte[LengthPrefixSize];
		await ReadExactAsync(stream, prefix, ct).ConfigureAwait(false);
		int length = ReadBigEndian(prefix, 0);
		if (length < 0 || length > MaxFrame)
			throw new InvalidOperationException($"bad frame length: {length}");

		var payload = new byte[length];
		if (length > 0) await ReadExactAsync(stream, payload, ct).ConfigureAwait(false);
		return payload;
	}

	private static async Task ReadExactAsync(Stream stream, byte[] buffer, CancellationToken ct)
	{
		int offset = 0;
		while (offset < buffer.Length)
		{
			int read = await stream.ReadAsync(buffer.AsMemory(offset, buffer.Length - offset), ct)
				.ConfigureAwait(false);
			if (read == 0) throw new EndOfStreamException("connection closed mid-frame");
			offset += read;
		}
	}

	private static void WriteBigEndian(byte[] buf, int offset, int value)
	{
		buf[offset    ] = (byte)((value >> 24) & 0xFF);
		buf[offset + 1] = (byte)((value >> 16) & 0xFF);
		buf[offset + 2] = (byte)((value >> 8)  & 0xFF);
		buf[offset + 3] = (byte)( value        & 0xFF);
	}

	private static int ReadBigEndian(byte[] buf, int offset)
	{
		return (buf[offset    ] << 24)
		     | (buf[offset + 1] << 16)
		     | (buf[offset + 2] << 8)
		     |  buf[offset + 3];
	}
}

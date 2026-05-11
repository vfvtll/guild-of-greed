using System;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace GuildOfGreed.Server;

// Загрузка/генерация TLS-сертификата для серверного SslStream.
//
// Self-signed dev-режим:
//   - При первом запуске генерируется RSA-2048 cert с SAN (127.0.0.1, localhost,
//     имя машины), сохраняется в data/server.pfx.
//   - Клиент доверяет cert через TOFU (см. client/Net/ServerTrustStore): при
//     первом подключении thumbprint запоминается, дальше проверяется на совпадение.
//
// Production:
//   - Оператор кладёт реальный cert от CA в путь NetworkConfig.CertificatePath
//     (PFX, зашифрованный тем же password или с переменной окружения).
//   - Клиент валидирует chain штатным образом без pinning'а.
//
// SAN обязателен в современных TLS-стэках: без него клиент не сможет верифицировать
// что cert именно для этого хоста (.NET SslStream строго проверяет hostname match
// через SAN). Старое поле CN больше для UX, чем для проверки.
public static class TlsCertificate
{
	private const string PfxPassword = "dev-only";  // Защита PFX-файла локально, не секрет.
	private const string SubjectName = "CN=GuildOfGreedDev";

	public static X509Certificate2 LoadOrCreate(string path)
	{
		if (File.Exists(path))
		{
			try
			{
				return new X509Certificate2(path, PfxPassword,
					X509KeyStorageFlags.Exportable | X509KeyStorageFlags.UserKeySet);
			}
			catch (CryptographicException)
			{
				// Файл повреждён или пароль не подходит — пересоздаём.
			}
		}
		return Generate(path);
	}

	private static X509Certificate2 Generate(string path)
	{
		using var rsa = RSA.Create(2048);
		var req = new CertificateRequest(SubjectName, rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

		req.CertificateExtensions.Add(new X509KeyUsageExtension(
			X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment, false));
		req.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(
			new OidCollection { new Oid("1.3.6.1.5.5.7.3.1") }, false));   // serverAuth EKU.
		req.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, false));

		// SAN: на что cert "годен". Без этого клиент с TargetHost="127.0.0.1"
		// получит SslPolicyErrors.RemoteCertificateNameMismatch.
		var san = new SubjectAlternativeNameBuilder();
		san.AddIpAddress(IPAddress.Loopback);                // 127.0.0.1
		san.AddIpAddress(IPAddress.IPv6Loopback);            // ::1
		san.AddDnsName("localhost");
		try { san.AddDnsName(Environment.MachineName); } catch { /* пустое имя — пропускаем */ }
		req.CertificateExtensions.Add(san.Build());

		var notBefore = DateTimeOffset.UtcNow.AddMinutes(-5);
		var notAfter  = DateTimeOffset.UtcNow.AddYears(5);
		using var cert = req.CreateSelfSigned(notBefore, notAfter);

		var pfxBytes = cert.Export(X509ContentType.Pfx, PfxPassword);
		File.WriteAllBytes(path, pfxBytes);

		return new X509Certificate2(pfxBytes, PfxPassword,
			X509KeyStorageFlags.Exportable | X509KeyStorageFlags.UserKeySet);
	}
}

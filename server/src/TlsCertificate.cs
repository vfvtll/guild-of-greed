using System;
using System.IO;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace GuildOfGreed.Server;

// Загрузка/генерация TLS-сертификата для серверного SslStream.
//
// На dev: при первом запуске генерируется самоподписанный RSA-2048 сертификат
// и сохраняется в data/server.pfx. Клиент в dev-режиме доверяет любому сертификату
// (см. NetworkClient.ValidateRemote). Это нормально для прототипа.
//
// На production: подменим самоподпись на полноценный сертификат от CA, файл будет
// положен оператором, метод просто загрузит его.
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
					X509KeyStorageFlags.Exportable | X509KeyStorageFlags.EphemeralKeySet);
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

		var notBefore = DateTimeOffset.UtcNow.AddMinutes(-5);
		var notAfter  = DateTimeOffset.UtcNow.AddYears(5);
		using var cert = req.CreateSelfSigned(notBefore, notAfter);

		var pfxBytes = cert.Export(X509ContentType.Pfx, PfxPassword);
		File.WriteAllBytes(path, pfxBytes);

		return new X509Certificate2(pfxBytes, PfxPassword,
			X509KeyStorageFlags.Exportable | X509KeyStorageFlags.EphemeralKeySet);
	}
}

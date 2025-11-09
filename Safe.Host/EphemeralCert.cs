using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

static class EphemeralCert
{
    public static X509Certificate2 Create()
    {
        using var rsa = RSA.Create(2048);
        var req = new CertificateRequest(
            "CN=safe-host",
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);

        var san = new SubjectAlternativeNameBuilder();
        san.AddDnsName("safe-host");
        san.AddDnsName("localhost");
        san.AddIpAddress(IPAddress.Loopback);
        req.CertificateExtensions.Add(san.Build());
        req.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, false));
        req.CertificateExtensions.Add(new X509KeyUsageExtension(
            X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment, false));

        var now = DateTimeOffset.UtcNow.AddMinutes(-5);
        using var selfSigned = req.CreateSelfSigned(now, now.AddYears(5));
        var pfxBytes = selfSigned.Export(X509ContentType.Pfx);

        // Load via X509CertificateLoader to avoid persisted state and suppress platform warnings
        return X509CertificateLoader.LoadPkcs12(pfxBytes, ReadOnlySpan<char>.Empty);
    }
}

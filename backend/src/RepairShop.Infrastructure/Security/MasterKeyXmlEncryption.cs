using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
using Microsoft.AspNetCore.DataProtection.XmlEncryption;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace RepairShop.Infrastructure.Security;

/// <summary>
/// Encrypts the DataProtection key ring (stored in the database) with AES-256-GCM using
/// DataProtection:MasterKey from the environment. With it, a database dump alone can't decrypt the shops'
/// integration secrets (Mercado Pago tokens, ARCA certificates, unlock codes).
/// </summary>
public static class MasterKey
{
    public const string ConfigKey = "DataProtection:MasterKey";

    public static byte[]? From(IConfiguration config)
    {
        var raw = config[ConfigKey];
        if (string.IsNullOrWhiteSpace(raw)) return null;
        try
        {
            var bytes = Convert.FromBase64String(raw.Trim());
            if (bytes.Length == 32) return bytes;
        }
        catch (FormatException)
        {
            // Not base64: derive the key from the passphrase.
        }
        return SHA256.HashData(Encoding.UTF8.GetBytes(raw.Trim()));
    }
}

public sealed class MasterKeyXmlEncryptor : IXmlEncryptor
{
    private readonly byte[] _key;

    public MasterKeyXmlEncryptor(byte[] key) => _key = key;

    public EncryptedXmlInfo Encrypt(XElement plaintextElement)
    {
        var plaintext = Encoding.UTF8.GetBytes(plaintextElement.ToString(SaveOptions.DisableFormatting));
        var nonce = RandomNumberGenerator.GetBytes(12);
        var cipher = new byte[plaintext.Length];
        var tag = new byte[16];
        using (var aes = new AesGcm(_key, tag.Length))
        {
            aes.Encrypt(nonce, plaintext, cipher, tag);
        }

        var element = new XElement("encryptedKey",
            new XComment(" AES-256-GCM con DataProtection:MasterKey "),
            new XElement("nonce", Convert.ToBase64String(nonce)),
            new XElement("tag", Convert.ToBase64String(tag)),
            new XElement("value", Convert.ToBase64String(cipher)));
        return new EncryptedXmlInfo(element, typeof(MasterKeyXmlDecryptor));
    }
}

public sealed class MasterKeyXmlDecryptor : IXmlDecryptor
{
    private readonly byte[] _key;

    // Created by the DataProtection activator from the type name stored in the key XML.
    public MasterKeyXmlDecryptor(IServiceProvider services)
    {
        _key = MasterKey.From(services.GetRequiredService<IConfiguration>())
               ?? throw new InvalidOperationException($"{MasterKey.ConfigKey} is required to read the encrypted key ring.");
    }

    public XElement Decrypt(XElement encryptedElement)
    {
        var nonce = Convert.FromBase64String((string)encryptedElement.Element("nonce")!);
        var tag = Convert.FromBase64String((string)encryptedElement.Element("tag")!);
        var cipher = Convert.FromBase64String((string)encryptedElement.Element("value")!);
        var plaintext = new byte[cipher.Length];
        using (var aes = new AesGcm(_key, tag.Length))
        {
            aes.Decrypt(nonce, cipher, tag, plaintext);
        }
        return XElement.Parse(Encoding.UTF8.GetString(plaintext));
    }
}

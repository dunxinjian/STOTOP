using System.Security.Cryptography;
using System.Text;

namespace STOTOP.Module.System.Services;

/// <summary>
/// 统一的对称加密工具：用于保护 db-connections.json / dingtalk-config.json 中的口令、AppSecret 等敏感串。
///
/// 加密格式（新，V2）：Base64( [0x01 版本标记][16字节随机IV][AES-CBC 密文] )。
/// 每条记录使用独立随机 IV，避免"相同明文得到相同密文"的信息泄漏。
///
/// 解密兼容旧格式（确定性 IV = 密钥前16字节、无版本标记的裸密文），
/// 因此历史已加密数据仍可读取；下次保存时会自动升级为新格式。
///
/// 注意：密钥仍来自调用方（默认回退到内置 key 仅为向后兼容）。要获得真正的机密性，
/// 应通过 Security:EncryptionKey（或环境变量 Security__EncryptionKey）提供非源码内置的强密钥。
/// </summary>
internal static class SecretProtector
{
    private const byte FormatV2 = 0x01;
    private const int IvSize = 16;

    /// <summary>使用随机 IV 加密，输出新格式（V2）。</summary>
    public static string Encrypt(string plainText, byte[] key)
    {
        using var aes = Aes.Create();
        aes.Key = key;
        aes.GenerateIV(); // 每条记录随机 IV
        var iv = aes.IV;

        using var encryptor = aes.CreateEncryptor();
        var plainBytes = Encoding.UTF8.GetBytes(plainText);
        var cipher = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);

        var output = new byte[1 + IvSize + cipher.Length];
        output[0] = FormatV2;
        Buffer.BlockCopy(iv, 0, output, 1, IvSize);
        Buffer.BlockCopy(cipher, 0, output, 1 + IvSize, cipher.Length);
        return Convert.ToBase64String(output);
    }

    /// <summary>解密。自动识别新/旧格式；新格式总长 ≡ 1 (mod 16)，旧格式（裸密文）总长 ≡ 0 (mod 16)，因此可无歧义区分。</summary>
    public static string Decrypt(string cipherText, byte[] key)
    {
        var data = Convert.FromBase64String(cipherText);

        byte[] iv;
        byte[] cipher;
        if (data.Length % 16 == 1 && data[0] == FormatV2)
        {
            // 新格式：随机 IV
            iv = new byte[IvSize];
            Buffer.BlockCopy(data, 1, iv, 0, IvSize);
            cipher = new byte[data.Length - 1 - IvSize];
            Buffer.BlockCopy(data, 1 + IvSize, cipher, 0, cipher.Length);
        }
        else
        {
            // 旧格式：确定性 IV = 密钥前 16 字节（向后兼容）
            iv = key[..IvSize];
            cipher = data;
        }

        using var aes = Aes.Create();
        aes.Key = key;
        aes.IV = iv;
        using var decryptor = aes.CreateDecryptor();
        var plainBytes = decryptor.TransformFinalBlock(cipher, 0, cipher.Length);
        return Encoding.UTF8.GetString(plainBytes);
    }
}

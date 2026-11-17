using System.Security.Cryptography;
using banditoth.MAUI.PreferencesExtension;
using Bonfire.Core;

<<<<<<< TODO: необъединенное изменение из проекта "Bonfire (net10.0-maccatalyst)", До:
=======
using Bonfire.Services;
using Bonfire.Services;
using Bonfire.Services.Storage;
>>>>>>> После

namespace Bonfire.Services;

public record DeviceKeys(Bytes PublicKey, Bytes PrivateKey)
{
    public static DeviceKeys Generate()
    {
        var publicKey = new byte[32];
        var privateKey = new byte[32];
        RandomNumberGenerator.Fill(publicKey);
        RandomNumberGenerator.Fill(privateKey);
        return new DeviceKeys(publicKey, privateKey);
    }
}

public static class ThisDevice
{
    private const string StorageKey = "DeviceKeys";
    private static readonly DeviceKeys Keys;
    public static Bytes PublicKey => Keys.PublicKey;
    public static Bytes PrivateKey => Keys.PrivateKey;
    static ThisDevice() => Keys = TryLoad() ?? GenerateAndSave();
    static DeviceKeys? TryLoad()
    {
        try { return PreferencesExtension.GetObject<DeviceKeys>(StorageKey, default!); }
        catch { return null; }
    }
    static DeviceKeys GenerateAndSave()
    {
        var keys = DeviceKeys.Generate();
        try { PreferencesExtension.SetObject(StorageKey, keys); } catch { }
        return keys;
    }
}
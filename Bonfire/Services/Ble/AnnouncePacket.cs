using System.Security.Cryptography;
using Bonfire.Core;

namespace Bonfire.Services.Ble;

public static class AnnouncePacket
{
    public const int 
        PublicKeySize = 32, 
        HashSize = 8, 
        HashCountSize = 1, 
        MaxPacketSize = 255, 
        AdOverhead = 8, 
        MaxHashes = (MaxPacketSize - AdOverhead - PublicKeySize - HashCountSize) / HashSize;

    public static byte[] Build(Bytes publicKey, IReadOnlyList<Bytes> recipientKeys)
    {
        var hashes = recipientKeys
            .Take(MaxHashes)
            .Select(k => Hash(k))
            .ToArray();

        var buffer = new byte[PublicKeySize + 1 + hashes.Length * HashSize];
        var span = buffer.AsSpan();

        publicKey.Span.CopyTo(span[..PublicKeySize]);
        span[PublicKeySize] = (byte)hashes.Length;

        for (int i = 0; i < hashes.Length; i++) 
            hashes[i].Span.CopyTo(span[(PublicKeySize + 1 + i * HashSize)..]);

        return buffer;
    }

    public static (Bytes PublicKey, List<Bytes> Hashes) Parse(byte[] data)
    {
        if (data.Length < PublicKeySize + 1) return (default, new());

        var publicKey = new Bytes(data[..PublicKeySize]);
        var count = data[PublicKeySize];
        var hashes = new List<Bytes>(count);

        for (int i = 0; i < count; i++)
        {
            var offset = PublicKeySize + 1 + i * HashSize;
            if (offset + HashSize > data.Length) break;
            hashes.Add(new Bytes(data[offset..(offset + HashSize)]));
        }

        return (publicKey, hashes);
    }

    public static Bytes Hash(Bytes key)
    {
        var hash = SHA256.HashData(key.Span);
        return new Bytes(hash[..HashSize]);
    }
}
using Bonfire.Core;
using Bonfire.Services.Ble;
using Bonfire.Services.Storage;
using System.Diagnostics.CodeAnalysis;

namespace Bonfire.Models;

public class Peer
{
    public readonly record struct Wire(Bytes PublicKey, DateTime LastSeen);

    public readonly record struct UI(Bytes PublicKey, string DisplayName, string StatusText);

    public class Data
    {
        public Bytes PublicKey { get; init; }
        public DateTime LastSeen { get; set; } = DateTime.UtcNow;
        public Bytes Hash { get; init; }
        public Data(Bytes publicKey)
        {
            PublicKey = publicKey;
            Hash = AnnouncePacket.Hash(publicKey);
        }
        public Wire ToWire() => new(PublicKey, LastSeen);
        public UI ToUI() => new(PublicKey, AliasTable.GetPeerName(PublicKey), UIFormatLastSeen(LastSeen));
        private static string UIFormatLastSeen(DateTime lastSeen)
        {
            var age = DateTime.UtcNow - lastSeen;
            if (age.TotalSeconds < 30) return "недавно";
            if (age.TotalMinutes < 60) return $"{(int)age.TotalMinutes} мин назад";
            return $"{(int)age.TotalHours} ч назад";
        }
    }

    
}


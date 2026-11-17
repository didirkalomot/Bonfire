using System.Text.Json;
using System.Text.Json.Serialization;

namespace Bonfire.Core;

[JsonConverter(typeof(Converter))]
public readonly struct Bytes : IEquatable<Bytes>
{
    private readonly byte[] bytes;
    public ReadOnlySpan<byte> Span => bytes;
    public bool Equals(Bytes other) => Span.SequenceEqual(other.Span);
    public override bool Equals(object? obj) => obj is Bytes other && Equals(other);
    public static bool operator ==(Bytes a, Bytes b) => a.Equals(b);
    public static bool operator !=(Bytes a, Bytes b) => !a.Equals(b);
    public static implicit operator Bytes(byte[] bytes) => new(bytes);
    public Bytes(byte[] bytes)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        this.bytes = bytes;
    }
    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var b in Span) hash.Add(b);
        return hash.ToHashCode();
    }
    public sealed class Converter : JsonConverter<Bytes>
    {
        public override Bytes Read(ref Utf8JsonReader reader, Type _, JsonSerializerOptions __)
            => Convert.FromBase64String(reader.GetString() ?? "");
        public override void Write(Utf8JsonWriter writer, Bytes value, JsonSerializerOptions _)
            => writer.WriteStringValue(Convert.ToBase64String(value.Span));
        public override Bytes ReadAsPropertyName(ref Utf8JsonReader reader, Type _, JsonSerializerOptions __)
            => Convert.FromBase64String(reader.GetString() ?? "");
        public override void WriteAsPropertyName(Utf8JsonWriter writer, Bytes value, JsonSerializerOptions _)
            => writer.WritePropertyName(Convert.ToBase64String(value.Span));
    }
}
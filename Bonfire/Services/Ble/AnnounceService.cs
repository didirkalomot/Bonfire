using Bluetooth.Abstractions.Scanning;
using Bluetooth.Abstractions.Scanning.EventArgs;
using Bluetooth.Abstractions.Scanning.Options;
using Bonfire.Core;
using Bonfire.Models;
using Bonfire.Services.Storage;
using Microsoft.Maui.ApplicationModel;
using System.Diagnostics;
using System.Text.Json;

namespace Bonfire.Services.Ble;

public static class AnnounceService
{
    static readonly object Lock = new();
    static IBluetoothScanner? Scanner;
    static List<Bytes> _Recipients = new();
    static List<Bytes> Recipients
    {
        get { lock (Lock) return [.._Recipients]; }
        set
        {
            lock (Lock)
            {
                _Recipients.Clear();
                _Recipients.AddRange(value);
            }
        }
    }

#if ANDROID
    static Platforms.Android.ExtendedAdvertiser? Advertiser;
#endif

    public static async Task StartAsync()
    {
        await StartScanningAsync();
        await RefreshAdvertiseAsync();
    }

    public static async Task StopAsync()
    {
        if (Scanner is not null)
        {
            Scanner.AdvertisementReceived -= OnAdvertisementReceived;
            await Scanner.StopScanningAsync();
            Scanner = null;
        }
        StopAdvertise();
    }

    static void StartAdvertise()
    {
#if ANDROID
        Advertiser ??= new Platforms.Android.ExtendedAdvertiser();
        if (!Advertiser.IsSupported) return;
        var snapshot = Recipients;
        var packet = AnnouncePacket.Build(ThisDevice.PublicKey, snapshot);
        if (packet.Length > Advertiser.MaxDataLength)
        {
            var maxHashes = Math.Max(0, (Advertiser.MaxDataLength - AnnouncePacket.PublicKeySize - 1) / AnnouncePacket.HashSize);
            packet = AnnouncePacket.Build(ThisDevice.PublicKey, snapshot.Take(maxHashes).ToList());
        }
        Advertiser.Start(packet);
#endif
    }

    static void StopAdvertise()
    {
#if ANDROID
        Advertiser?.Stop();
#endif
    }

    public static async Task RefreshAdvertiseAsync()
    {
        StopAdvertise();
        Recipients = await MessageStore.GetQueuedRecipientsAsync();
        StartAdvertise();
    }

    static async Task StartScanningAsync()
    {
        var services = IPlatformApplication.Current?.Services;
        Scanner = services?.GetService<IBluetoothScanner>();
        if (Scanner is null) { Debug.WriteLine($"Service {services} not have scanner"); return; }

        Scanner.AdvertisementFilter = ad => ad.ManufacturerId == Protocol.CompanyId;
        Scanner.AdvertisementReceived += OnAdvertisementReceived;
        await Scanner.StartScanningAsync(Protocol.ActiveDeviceScanOptions);
    }

    static async void OnAdvertisementReceived(object? sender, AdvertisementReceivedEventArgs e)
    {
        var ad = e.Advertisement;
        if (ad?.ManufacturerData is not { Length: > 0 } mfg) return;

        var (publicKey, hashes) = AnnouncePacket.Parse(mfg.ToArray());
        if (publicKey.Span.Length == 0) return;

        PeerTable.UpdateFromBeacon(publicKey);
        await CheckHashesAsync(publicKey, ad.BluetoothAddress, hashes);
    }

    static async Task CheckHashesAsync(Bytes senderPublicKey, string bluetoothAddress, List<Bytes> incomingHashes)
    {
        if (incomingHashes.Count == 0) return;

        var hashSet = incomingHashes.ToHashSet();
        var candidates = PeerTable.GetAll()
            .Select(p => (PublicKey: p.PublicKey, Hash: p.Hash))
            .Append((PublicKey: ThisDevice.PublicKey, Hash: AnnouncePacket.Hash(ThisDevice.PublicKey)));

        var toRequest = candidates
            .Where(c => hashSet.Contains(c.Hash))
            .Select(c => c.PublicKey)
            .ToHashSet();

        if (toRequest.Count == 0) return;
        await ConnectAndFetchAsync(bluetoothAddress, toRequest.ToList());
    }

    static async Task ConnectAndFetchAsync(string bluetoothAddress, List<Bytes> peersToRequest)
    {
        if (Scanner is null) return;
        var device = Scanner.GetDeviceOrDefault(bluetoothAddress);
        if (device is null) { Debug.WriteLine($"Device {bluetoothAddress} not found"); return; }

        try
        {
            if (!device.IsConnected) await device.ConnectAsync();
            await device.ExploreServicesAsync(ServiceExplorationOptions.WithCharacteristics);

            var service = device.GetService(Protocol.MessageServiceUuid);
            if (service is null) return;

            var characteristic = service.GetCharacteristic(Protocol.MessageCharacteristicUuid);
            if (characteristic is null) return;
            var tcs = new TaskCompletionSource<byte[]>(); // Подписка на Indicate
            void OnValueUpdated(object? s, EventArgs args) { tcs.TrySetResult(characteristic.Value.ToArray()); }
            characteristic.ValueUpdated += OnValueUpdated;
            await characteristic.StartListeningAsync();
            await characteristic.WriteValueAsync(SerializeKeys(peersToRequest)); // Отправка запроса
            var data = await tcs.Task.WaitAsync(TimeSpan.FromSeconds(5)); // Ожидание ответа (с таймаутом)
            characteristic.ValueUpdated -= OnValueUpdated; // Отписка
            await characteristic.StopListeningAsync();

            var json = System.Text.Encoding.UTF8.GetString(data);
            var messages = JsonSerializer.Deserialize<List<Message.Wire>>(json);

            if (messages is { Count: > 0 })
            {
                await MessageStore.SaveAsync(messages);
                Debug.WriteLine($"Received {messages.Count} message(s)");
                await RefreshAdvertiseAsync();
            }

            await device.DisconnectAsync();
        }
        catch (Exception ex) { Debug.WriteLine($"GATT error: {ex.Message}"); }
    }

    static byte[] SerializeKeys(List<Bytes> keys)
    {
        var buffer = new byte[keys.Count * 32];
        for (int i = 0; i < keys.Count; i++) keys[i].Span.CopyTo(buffer.AsSpan(i * 32, 32));
        return buffer;
    }

   
}
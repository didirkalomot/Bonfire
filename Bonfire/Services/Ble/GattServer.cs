using Bonfire.Core;
using Bluetooth.Abstractions.Broadcasting;
using Bluetooth.Abstractions.Broadcasting.EventArgs;
using Bonfire.Models;
using Bonfire.Services.Storage;
using System.Diagnostics;
using System.Text.Json;

namespace Bonfire.Services.Ble;

public static class GattServer
{
    static IBluetoothLocalService? Service;
    static IBluetoothLocalCharacteristic? Characteristic;

    public static async Task StartAsync(IBluetoothBroadcaster broadcaster)
    {
        Service = await broadcaster.CreateServiceAsync(
            Protocol.MessageServiceUuid,
            isPrimary: true);

        Characteristic = await Service.CreateCharacteristicAsync(
            Protocol.MessageCharacteristicUuid,
            Protocol.MessageCharacteristicProperties,
            Protocol.MessageCharacteristicPermissions);

        Characteristic.WriteRequested += OnWriteRequested;
    }

    public static async Task StopAsync()
    {
        if (Characteristic is not null)
            Characteristic.WriteRequested -= OnWriteRequested;
        Characteristic = null;
        Service = null;
    }

    static async void OnWriteRequested(object? sender, CharacteristicWriteRequestEventArgs e)
    {
        // B написал: N × 32 байт (ключи получателей)
        var keys = DecodeKeys(e.Value.ToArray());
        if (keys.Count == 0) return;

        // Ищем сообщения для этих получателей (они сразу же пометятся как отправленные)
        var messages = await MessageStore.TakeQueuedForRecipientsAsync(keys);
        if (messages.Count == 0) return;

        // Отправляем через Indicate
        var json = JsonSerializer.SerializeToUtf8Bytes(messages);
        await Characteristic!.UpdateValueAsync(json, notifyClients: true);
    }

    static List<Bytes> DecodeKeys(byte[] data)
    {
        var list = new List<Bytes>();
        for (int i = 0; i + 32 <= data.Length; i += 32)
            list.Add(new Bytes(data[i..(i + 32)]));
        return list;
    }
}
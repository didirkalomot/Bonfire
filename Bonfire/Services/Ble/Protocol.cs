using Bluetooth.Abstractions.Broadcasting.Enums;
using Bluetooth.Abstractions.Scanning.Options;

namespace Bonfire.Services.Ble;

public static partial class Protocol
{
    public static readonly Guid MessageServiceUuid =
        Guid.Parse("0000B0F1-0000-1000-8000-00805F9B34FB");

    public static readonly Guid MessageCharacteristicUuid =
        Guid.Parse("0000B0F2-0000-1000-8000-00805F9B34FB");

    public const ushort CompanyId = 0xFFFF;

    // Клиент: Read + Write. Сервер: Read + Write + Indicate.
    public const BluetoothCharacteristicProperties MessageCharacteristicProperties =
        BluetoothCharacteristicProperties.Read |
        BluetoothCharacteristicProperties.Write |
        BluetoothCharacteristicProperties.Indicate;

    public const BluetoothCharacteristicPermissions MessageCharacteristicPermissions =
        BluetoothCharacteristicPermissions.Read |
        BluetoothCharacteristicPermissions.Write;

    public static readonly ScanningOptions ActiveDeviceScanOptions = new()
    {
        ServiceUuids = new[] { Protocol.MessageServiceUuid },  // только Bonfire
        ScanMode = BluetoothScanMode.LowLatency,               // активный поиск
        IgnoreDuplicateAdvertisements = false,                 // нужны все обновления (хеши)
        EnableExtendedAdvertising = true,                      // BLE 5, длинные пакеты
        RssiThreshold = -85,                                   // отсечь совсем далёкие
        IgnoreNamelessAdvertisements = false,                  // имена не обязательны
    };

    public static readonly ScanningOptions NotActiveDeviceScanOptions = new()
    {
        ServiceUuids = new[] { Protocol.MessageServiceUuid },  // только Bonfire
        ScanMode = BluetoothScanMode.LowPower,                 // активный поиск
        IgnoreDuplicateAdvertisements = false,                 // нужны все обновления (хеши)
        EnableExtendedAdvertising = true,                      // BLE 5, длинные пакеты
        RssiThreshold = -85,                                   // отсечь совсем далёкие
        IgnoreNamelessAdvertisements = false,                  // имена не обязательны
    };
}
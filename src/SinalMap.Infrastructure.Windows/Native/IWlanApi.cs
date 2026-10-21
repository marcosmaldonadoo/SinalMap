namespace SinalMap.Infrastructure.Windows.Native;

/// <summary>A fronteira ABI pode ser substituída para que os testes não carreguem wlanapi.dll.</summary>
internal interface IWlanApi
{
    uint OpenHandle(out uint negotiatedVersion, out nint handle);
    uint CloseHandle(nint handle);
    void FreeMemory(nint pointer);
    uint EnumerateInterfaces(SafeWlanHandle handle, out nint pointer);
    uint QueryInterface(SafeWlanHandle handle, Guid id, WlanOpcode opcode, out uint size, out nint pointer);
    uint Scan(SafeWlanHandle handle, Guid id);
    uint RegisterNotification(SafeWlanHandle handle, uint source, WlanNotificationCallback? callback);
    uint GetBssList(SafeWlanHandle handle, Guid id, ref Dot11Ssid ssid,
        Dot11BssType bssType, bool securityEnabled, out nint pointer);
}

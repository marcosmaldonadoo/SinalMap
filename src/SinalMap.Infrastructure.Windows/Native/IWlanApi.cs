namespace SinalMap.Infrastructure.Windows.Native;

/// <summary>The thin ABI boundary is replaceable so tests never load wlanapi.dll.</summary>
internal interface IWlanApi
{
    uint OpenHandle(out uint negotiatedVersion, out nint handle);
    uint CloseHandle(nint handle);
    void FreeMemory(nint pointer);
    uint EnumerateInterfaces(SafeWlanHandle handle, out nint pointer);
    uint QueryInterface(SafeWlanHandle handle, Guid id, WlanOpcode opcode, out uint size, out nint pointer);
    uint GetBssList(SafeWlanHandle handle, Guid id, ref Dot11Ssid ssid,
        Dot11BssType bssType, bool securityEnabled, out nint pointer);
}

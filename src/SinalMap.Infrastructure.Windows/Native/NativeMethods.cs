using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace SinalMap.Infrastructure.Windows.Native;

[SupportedOSPlatform("windows")]
internal static class NativeMethods
{
    // These functions return their error codes directly. GetLastWin32Error would be incorrect.
    [DllImport("wlanapi.dll", ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    internal static extern uint WlanOpenHandle(uint clientVersion, nint reserved, out uint negotiatedVersion, out nint handle);

    [DllImport("wlanapi.dll", ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    internal static extern uint WlanCloseHandle(nint handle, nint reserved);

    [DllImport("wlanapi.dll", ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    internal static extern void WlanFreeMemory(nint pointer);

    [DllImport("wlanapi.dll", ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    internal static extern uint WlanEnumInterfaces(SafeWlanHandle handle, nint reserved, out nint pointer);

    [DllImport("wlanapi.dll", ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    internal static extern uint WlanQueryInterface(SafeWlanHandle handle, in Guid id, WlanOpcode opcode,
        nint reserved, out uint size, out nint pointer, out uint opcodeValueType);

    [DllImport("wlanapi.dll", ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    internal static extern uint WlanGetNetworkBssList(SafeWlanHandle handle, in Guid id, ref Dot11Ssid ssid,
        Dot11BssType bssType, [MarshalAs(UnmanagedType.Bool)] bool securityEnabled, nint reserved, out nint pointer);
}

[SupportedOSPlatform("windows")]
internal sealed class WlanApi : IWlanApi
{
    public uint OpenHandle(out uint version, out nint handle) => NativeMethods.WlanOpenHandle(2, 0, out version, out handle);
    public uint CloseHandle(nint handle) => NativeMethods.WlanCloseHandle(handle, 0);
    public void FreeMemory(nint pointer) => NativeMethods.WlanFreeMemory(pointer);
    public uint EnumerateInterfaces(SafeWlanHandle handle, out nint pointer) => NativeMethods.WlanEnumInterfaces(handle, 0, out pointer);
    public uint QueryInterface(SafeWlanHandle handle, Guid id, WlanOpcode opcode, out uint size, out nint pointer) =>
        NativeMethods.WlanQueryInterface(handle, in id, opcode, 0, out size, out pointer, out _);
    public uint GetBssList(SafeWlanHandle handle, Guid id, ref Dot11Ssid ssid,
        Dot11BssType bssType, bool securityEnabled, out nint pointer) =>
        NativeMethods.WlanGetNetworkBssList(handle, in id, ref ssid, bssType, securityEnabled, 0, out pointer);
}

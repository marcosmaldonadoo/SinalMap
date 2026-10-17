using Microsoft.Win32.SafeHandles;

namespace SinalMap.Infrastructure.Windows.Native;

internal sealed class SafeWlanHandle : SafeHandleZeroOrMinusOneIsInvalid
{
    private readonly Func<nint, uint> close;

    internal SafeWlanHandle(nint value, Func<nint, uint> close) : base(ownsHandle: true)
    {
        this.close = close;
        SetHandle(value);
    }

    protected override bool ReleaseHandle() => close(handle) == 0;
}

internal sealed class SafeWlanMemory : SafeHandleZeroOrMinusOneIsInvalid
{
    private readonly Action<nint> free;

    internal SafeWlanMemory(nint value, Action<nint> free) : base(ownsHandle: true)
    {
        this.free = free;
        SetHandle(value);
    }

    protected override bool ReleaseHandle()
    {
        free(handle);
        return true;
    }
}

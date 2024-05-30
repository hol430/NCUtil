using System.Runtime.InteropServices;

namespace NCUtil.Core.Interop.Marshalling;

// From https://stackoverflow.com/questions/6300093/why-cant-i-return-a-char-string-from-c-to-c-sharp-in-a-release-build
class ConstCharPtrMarshaler : ICustomMarshaler
{
    private static readonly ConstCharPtrMarshaler instance = new ConstCharPtrMarshaler();

    public object MarshalNativeToManaged(IntPtr pNativeData)
    {
        // fixme
        return Marshal.PtrToStringAnsi(pNativeData) ?? string.Empty;
    }

    public IntPtr MarshalManagedToNative(object ManagedObj)
    {
        return IntPtr.Zero;
    }

    public void CleanUpNativeData(IntPtr pNativeData){ }

    public void CleanUpManagedData(object ManagedObj) { }

    public int GetNativeDataSize() => IntPtr.Size;

    public static ICustomMarshaler GetInstance(string cookie) => instance;
}

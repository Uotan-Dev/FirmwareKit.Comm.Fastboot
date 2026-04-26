using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

namespace FirmwareKit.Comm.Fastboot;

internal static class DataHelper
{
#if NET5_0_OR_GREATER
    public static T Bytes2Struct<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.NonPublicConstructors)] T>(byte[] data, int length) where T : struct
#else
    public static T Bytes2Struct<T>(byte[] data, int length) where T : struct
#endif
    {
        IntPtr ptr = Marshal.AllocHGlobal(length);
        try
        {
            Marshal.Copy(data, 0, ptr, length);
            return Marshal.PtrToStructure<T>(ptr);
        }
        finally
        {
            Marshal.FreeHGlobal(ptr);
        }
    }

    public static byte[] Struct2Bytes<T>(in T str) where T : struct
    {
        int length = Marshal.SizeOf<T>();
        byte[] data = new byte[length];
        IntPtr ptr = Marshal.AllocHGlobal(length);
        try
        {
            Marshal.StructureToPtr(str, ptr, true);
            Marshal.Copy(ptr, data, 0, length);
        }
        finally
        {
            Marshal.FreeHGlobal(ptr);
        }
        return data;
    }

#if NET5_0_OR_GREATER
    public static T Deserialize<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.NonPublicConstructors)] T>(Stream stream) where T : struct
#else
    public static T Deserialize<T>(Stream stream) where T : struct
#endif
    {
        int size = Marshal.SizeOf<T>();
        byte[] buffer = new byte[size];
        ReadStreamFully(stream, buffer, size);
        return Bytes2Struct<T>(buffer, size);
    }

    public static void Serialize<T>(Stream stream, in T str) where T : struct
    {
        byte[] buffer = Struct2Bytes(str);
        stream.Write(buffer, 0, buffer.Length);
    }

    internal static void ReadStreamFully(Stream stream, byte[] buffer, int count)
    {
        int offset = 0;
        while (offset < count)
        {
            int read = stream.Read(buffer, offset, count - offset);
            if (read == 0) throw new EndOfStreamException();
            offset += read;
        }
    }
}

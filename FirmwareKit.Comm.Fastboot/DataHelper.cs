using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

namespace FirmwareKit.Comm.Fastboot;

public class DataHelper
{
#if NET5_0_OR_GREATER
    public static T Bytes2Struct<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.NonPublicConstructors)] T>(byte[] data, int length) where T : struct
#else
    public static T Bytes2Struct<T>(byte[] data, int length) where T : struct
#endif
    {
        T str;
        IntPtr ptr = Marshal.AllocHGlobal(length);
        Marshal.Copy(data, 0, ptr, length);
        str = Marshal.PtrToStructure<T>(ptr);
        Marshal.FreeHGlobal(ptr);
        return str;
    }

    public static byte[] Struct2Bytes<T>(T str) where T : struct
    {
        int length = Marshal.SizeOf<T>();
        byte[] data = new byte[length];
        IntPtr ptr = Marshal.AllocHGlobal(length);
        Marshal.StructureToPtr(str, ptr, true);
        Marshal.Copy(ptr, data, 0, length);
        Marshal.FreeHGlobal(ptr);
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

    public static void Serialize<T>(Stream stream, T str) where T : struct
    {
        byte[] buffer = Struct2Bytes<T>(str);
        stream.Write(buffer, 0, buffer.Length);
    }

    private static void ReadStreamFully(Stream stream, byte[] buffer, int count)
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
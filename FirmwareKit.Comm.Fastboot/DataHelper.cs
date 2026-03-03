using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

namespace FirmwareKit.Comm.Fastboot
{
    public class DataHelper
    {
        public static T Bytes2Struct<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.NonPublicConstructors)] T>(byte[] data, int length) where T : struct
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

        public static T Deserialize<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.NonPublicConstructors)] T>(Stream stream) where T : struct
        {
            int size = Marshal.SizeOf<T>();
            byte[] buffer = new byte[size];
            stream.ReadExactly(buffer, 0, size);
            return Bytes2Struct<T>(buffer, size);
        }

        public static void Serialize<T>(Stream stream, T str) where T : struct
        {
            byte[] buffer = Struct2Bytes<T>(str);
            stream.Write(buffer, 0, buffer.Length);
        }
    }
}

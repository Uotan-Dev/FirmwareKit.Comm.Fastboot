using System.Runtime.InteropServices;
using System.Text;

namespace FirmwareKit.Comm.Fastboot.Tests
{
    public class BootImageTests
    {
        [Fact]
        public void BootImageHeaderV0_Create_HasCorrectMagic()
        {
            var header = BootImageHeaderV0.Create();
            string magic = Encoding.ASCII.GetString(header.Magic);
            Assert.Equal("ANDROID!", magic);
        }

        [Fact]
        public void BootImageHeaderV0_Size_IsExpected()
        {
            int size = Marshal.SizeOf<BootImageHeaderV0>();
            Assert.Equal(1632, size);
        }
    }
}

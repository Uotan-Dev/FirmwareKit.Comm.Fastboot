namespace FirmwareKit.Comm.Fastboot
{
    public interface IFastbootTransport : IDisposable
    {
        byte[] Read(int length);
        long Write(byte[] data, int length);
    }
}

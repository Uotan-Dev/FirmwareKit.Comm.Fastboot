namespace FirmwareKit.Comm.Fastboot.Backend.Network;

public interface IFastbootTransport : IDisposable
{
    byte[] Read(int length);
    long Write(byte[] data, int length);


}

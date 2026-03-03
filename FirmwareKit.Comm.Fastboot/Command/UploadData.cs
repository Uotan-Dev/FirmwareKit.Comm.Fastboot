using System;
using System.IO;
using FirmwareKit.Comm.Fastboot.DataModel;

namespace FirmwareKit.Comm.Fastboot
{
    public partial class FastbootUtil
    {
        /// <summary>
        /// Uploads data from device (corresponding to the upload in the protocol)
        /// </summary>
        public FastbootResponse UploadData(string command, Stream output)
        {
            FastbootResponse response = RawCommand(command);
            if (response.Result != FastbootState.Data)
                throw new Exception("Unexpected response for upload: " + response.Result);

            long size = response.DataSize;
            long bytesDownloaded = 0;
            while (bytesDownloaded < size)
            {
                int toRead = (int)Math.Min(OnceSendDataSize, size - bytesDownloaded);
                byte[] data = Transport.Read(toRead);
                if (data == null || data.Length == 0) throw new Exception("Unexpected EOF from USB.");
                output.Write(data, 0, data.Length);
                bytesDownloaded += data.Length;
                NotifyProgress(bytesDownloaded, size);
            }

            return HandleResponse();
        }
    }
}

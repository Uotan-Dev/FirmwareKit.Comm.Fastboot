using System;
using System.Security.Cryptography;
using System.Text;
using FirmwareKit.Comm.Fastboot.DataModel;

namespace FirmwareKit.Comm.Fastboot
{
    public partial class FastbootUtil
    {
        /// <summary>
        /// Downloads data
        /// </summary>
        public FastbootResponse DownloadData(byte[] data)
        {
            FastbootResponse response = RawCommand("download:" + data.Length.ToString("x8"));
            if (response.Result != FastbootState.Data)
                return response;

            long written = Transport.Write(data, data.Length);
            if (written != data.Length)
            {
                return new FastbootResponse { Result = FastbootState.Fail, Response = $"Short write: {written}/{data.Length}" };
            }

            return HandleResponse();
        }
    }
}

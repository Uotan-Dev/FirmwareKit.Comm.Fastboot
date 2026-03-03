using System;
using System.IO;
using System.Security.Cryptography;
using FirmwareKit.Comm.Fastboot.DataModel;

namespace FirmwareKit.Comm.Fastboot
{
    public partial class FastbootUtil
    {
        /// <summary>
        /// Downloads data
        /// </summary>
        public FastbootResponse DownloadData(Stream stream, long length, bool onEvent = true)
        {
            FastbootResponse response = RawCommand("download:" + length.ToString("x8"));
            if (response.Result == FastbootState.Fail)
                return response;

            using var sha256 = SHA256.Create();
            byte[] buffer = new byte[OnceSendDataSize];
            long bytesRead = 0;
            while (bytesRead < length)
            {
                int toRead = (int)Math.Min(OnceSendDataSize, length - bytesRead);
                int readSize = stream.Read(buffer, 0, toRead);
                if (readSize <= 0) break;

                sha256.TransformBlock(buffer, 0, readSize, null, 0);
                Transport.Write(buffer, readSize);
                bytesRead += readSize;
                if (onEvent)
                    NotifyProgress(bytesRead, length);
            }
            sha256.TransformFinalBlock(Array.Empty<byte>(), 0, 0);

            var res = HandleResponse();
            if (res.Result == FastbootState.Success && sha256.Hash != null)
            {
                res.Hash = BitConverter.ToString(sha256.Hash).Replace("-", "").ToLower();
            }
            return res;
        }
    }
}

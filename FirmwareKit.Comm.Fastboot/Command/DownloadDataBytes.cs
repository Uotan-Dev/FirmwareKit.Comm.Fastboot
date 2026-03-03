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
            if (response.Result == FastbootState.Fail)
                return response;
            Transport.Write(data, data.Length);
            var res = HandleResponse();
            if (res.Result == FastbootState.Success)
            {
                using var sha256 = SHA256.Create();
                byte[] hash = sha256.ComputeHash(data);
                res.Hash = BitConverter.ToString(hash).Replace("-", "").ToLower();
            }
            return res;
        }
    }
}

namespace FirmwareKit.Comm.Fastboot.DataModel
{
    public class FastbootResponse
    {
        public FastbootState Result { get; set; }
        public string Response { get; set; } = "";
        public byte[]? Data { get; set; }
        public long DataSize { get; set; }
        public List<string> Info { get; set; } = new List<string>();
        public string Text { get; set; } = "";
        public string? Hash { get; set; }

        public FastbootResponse ThrowIfError()
        {
            if (Result == FastbootState.Fail)
                throw new Exception("Command failed");
            return this;
        }
    }

    public enum FastbootState
    {
        Success,
        Fail,
        Text,
        Data,
        Info,
        Unknown,
        Timeout
    }
}

namespace FirmwareKit.Comm.Fastboot;

public class FastbootCommandEventArgs : EventArgs
{
    public string Command { get; }
    public FastbootResponse Response { get; }
    public bool Quiet { get; }

    public FastbootCommandEventArgs(string command, FastbootResponse response, bool quiet)
    {
        Command = command;
        Response = response;
        Quiet = quiet;
    }
}

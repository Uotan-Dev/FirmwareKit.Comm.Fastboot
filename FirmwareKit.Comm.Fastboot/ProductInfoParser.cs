using FirmwareKit.Comm.Fastboot.DataModel;

namespace FirmwareKit.Comm.Fastboot;

public class ProductInfoParser(FastbootUtil fastboot)
{
    private FastbootUtil _fastboot = fastboot;
    private Dictionary<string, string> _varCache = [];

    public bool Validate(string content, out string? error)
    {
        error = null;
        string[] lines = content.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        foreach (string rawLine in lines)
        {
            string line = rawLine.Trim();
            if (string.IsNullOrEmpty(line) || line.StartsWith("#")) continue;

            if (line.StartsWith("require "))
            {
                string contentPart = line.Substring(8).Trim();
                string[] requirements = contentPart.Split([' '], StringSplitOptions.RemoveEmptyEntries);
                foreach (var req in requirements)
                {
                    if (!ProcessRequire(req, out error)) return false;
                }
            }
            else if (line.StartsWith("reject "))
            {
                string contentPart = line.Substring(7).Trim();
                string[] rejections = contentPart.Split([' '], StringSplitOptions.RemoveEmptyEntries);
                foreach (var rej in rejections)
                {
                    if (!ProcessReject(rej, out error)) return false;
                }
            }
            else if (line.StartsWith("require-for-product:"))
            {
                int colonIdx = line.IndexOf(':');
                int spaceIdx = line.IndexOf(' ', colonIdx);
                if (colonIdx > 0 && spaceIdx > colonIdx)
                {
                    string prod = line.Substring(colonIdx + 1, spaceIdx - colonIdx - 1).Trim();
                    string deviceProd = GetVariable("product");
                    if (deviceProd == prod)
                    {
                        if (!ProcessRequire(line.Substring(spaceIdx + 1).Trim(), out error)) return false;
                    }
                }
            }
            else if (line.StartsWith("require-for-variant:"))
            {
                int colonIdx = line.IndexOf(':');
                int spaceIdx = line.IndexOf(' ', colonIdx);
                if (colonIdx > 0 && spaceIdx > colonIdx)
                {
                    string variant = line.Substring(colonIdx + 1, spaceIdx - colonIdx - 1).Trim();
                    string deviceVariant = GetVariable("variant");
                    if (deviceVariant == variant)
                    {
                        if (!ProcessRequire(line.Substring(spaceIdx + 1).Trim(), out error)) return false;
                    }
                }
            }
        }
        return true;
    }

    private bool ProcessRequire(string requirement, out string? error)
    {
        error = null;
        string sep = requirement.Contains('=') ? "=" : " ";
        string[] parts = requirement.Split(new[] { sep }, 2, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2) return true;

        string key = parts[0].Trim();
        string expectedValue = parts[1].Trim();

        // Handle partition-exists special key
        if (key == "partition-exists")
        {
            if (_fastboot.PartitionExists(expectedValue)) return true;
            error = $"Requirement failed: partition {expectedValue} does not exist on device";
            return false;
        }

        string[] allowedValues = expectedValue.Split(new[] { '|', ',' }, StringSplitOptions.RemoveEmptyEntries);
        string deviceValue = GetVariable(key);

        foreach (string val in allowedValues)
        {
            string trimmedVal = val.Trim();
            if (trimmedVal.EndsWith("*"))
            {
                if (deviceValue.StartsWith(trimmedVal.Substring(0, trimmedVal.Length - 1))) return true;
            }
            else if (trimmedVal == deviceValue) return true;
        }

        error = $"Requirement failed: {key} (device: {deviceValue}, expected: {expectedValue})";
        return false;
    }

    private bool ProcessReject(string rejection, out string? error)
    {
        error = null;
        string sep = rejection.Contains('=') ? "=" : " ";
        string[] parts = rejection.Split(new[] { sep }, 2, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2) return true;

        string key = parts[0].Trim();
        string[] rejectedValues = parts[1].Split(new[] { '|', ',' }, StringSplitOptions.RemoveEmptyEntries);
        string deviceValue = GetVariable(key);

        foreach (string val in rejectedValues)
        {
            string trimmedVal = val.Trim();
            bool isMatch = false;
            if (trimmedVal.EndsWith("*"))
            {
                isMatch = deviceValue.StartsWith(trimmedVal.Substring(0, trimmedVal.Length - 1));
            }
            else
            {
                isMatch = trimmedVal == deviceValue;
            }

            if (isMatch)
            {
                error = $"Rejection failed: {key} is {deviceValue}";
                return false;
            }
        }
        return true;
    }

    private string GetVariable(string key)
    {
        if (_varCache.ContainsKey(key)) return _varCache[key];

        string queryKey = key == "board" ? "product" : key;
        var resp = _fastboot.RawCommand("getvar:" + queryKey);
        string val = resp.Result == FastbootState.Success ? resp.Response.Trim() : "";
        _varCache[key] = val;
        return val;
    }


}
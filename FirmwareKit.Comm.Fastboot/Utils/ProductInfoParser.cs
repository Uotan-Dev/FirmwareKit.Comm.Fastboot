namespace FirmwareKit.Comm.Fastboot;

internal class ProductInfoParser(FastbootDriver fastboot)
{
    private readonly FastbootDriver _fastboot = fastboot;
    private readonly Dictionary<string, string> _varCache = [];

    public bool Validate(string content, out string? error)
    {
        error = null;
        string[] lines = content.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (string rawLine in lines)
        {
            string line = rawLine.Trim();
            if (string.IsNullOrEmpty(line) || line.StartsWith("#")) continue;

            if (line.StartsWith("require "))
            {
                string contentPart = line.Substring(8).Trim();
                foreach (var req in contentPart.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    if (!ProcessRequire(req, out error)) return false;
                }
            }
            else if (line.StartsWith("reject "))
            {
                string contentPart = line.Substring(7).Trim();
                foreach (var rej in contentPart.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    if (!ProcessReject(rej, out error)) return false;
                }
            }
            else if (line.StartsWith("require-for-product:"))
            {
                if (TryExtractScopedRequirement(line, out string scopeValue, out string requirement))
                {
                    string deviceProd = GetVariable("product");
                    if (deviceProd == scopeValue && !ProcessRequire(requirement, out error)) return false;
                }
            }
            else if (line.StartsWith("require-for-variant:"))
            {
                if (TryExtractScopedRequirement(line, out string scopeValue, out string requirement))
                {
                    string deviceVariant = GetVariable("variant");
                    if (deviceVariant == scopeValue && !ProcessRequire(requirement, out error)) return false;
                }
            }
        }
        return true;
    }

    private static bool TryExtractScopedRequirement(string line, out string scopeValue, out string requirement)
    {
        scopeValue = "";
        requirement = "";
        int colonIdx = line.IndexOf(':');
        int spaceIdx = line.IndexOf(' ', colonIdx);
        if (colonIdx <= 0 || spaceIdx <= colonIdx) return false;

        scopeValue = line.Substring(colonIdx + 1, spaceIdx - colonIdx - 1).Trim();
        requirement = line.Substring(spaceIdx + 1).Trim();
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
            bool isMatch = trimmedVal.EndsWith("*")
                ? deviceValue.StartsWith(trimmedVal.Substring(0, trimmedVal.Length - 1))
                : trimmedVal == deviceValue;

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
        if (_varCache.TryGetValue(key, out string? cached)) return cached;

        string queryKey = key == "board" ? "product" : key;
        var resp = _fastboot.RawCommand("getvar:" + queryKey);
        string val = resp.Result == FastbootState.Success ? resp.Response.Trim() : "";
        _varCache[key] = val;
        return val;
    }
}

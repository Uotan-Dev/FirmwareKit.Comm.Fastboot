namespace FirmwareKit.Comm.Fastboot;

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
internal sealed class ExternalToolDependencyAttribute : Attribute
{
    public ExternalToolDependencyAttribute(string toolName)
    {
        ToolName = toolName;
    }

    public string ToolName { get; }
}

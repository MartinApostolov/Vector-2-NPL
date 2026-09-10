namespace Vector.VisualStudio.Diagnostics;

using System.Diagnostics;
using System.Runtime.InteropServices;

internal static class DotNetChildProcessEnvironment
{
    private static readonly string[] RuntimeRootVariables =
    [
        "DOTNET_ROOT",
        "DOTNET_ROOT_X64",
        "DOTNET_ROOT_X86",
        "DOTNET_ROOT_ARM64",
        "DOTNET_ROOT(x86)",
    ];

    public static string UseMachineWideRuntime(ProcessStartInfo startInfo)
    {
        ArgumentNullException.ThrowIfNull(startInfo);

        var inherited = new List<string>();
        foreach (string variable in RuntimeRootVariables)
        {
            if (startInfo.Environment.TryGetValue(variable, out string? value))
            {
                inherited.Add($"{variable}='{value}'");
                startInfo.Environment.Remove(variable);
            }
        }

        string runtimeRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            "dotnet");
        string sharedRuntimeDirectory = Path.Combine(runtimeRoot, "shared", "Microsoft.NETCore.App");
        bool hasNet8Runtime;
        try
        {
            hasNet8Runtime = Directory.Exists(sharedRuntimeDirectory)
                && Directory.EnumerateDirectories(sharedRuntimeDirectory, "8.*", SearchOption.TopDirectoryOnly).Any();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            hasNet8Runtime = false;
        }

        if (hasNet8Runtime)
        {
            startInfo.Environment["DOTNET_ROOT"] = runtimeRoot;
            startInfo.Environment[GetArchitectureSpecificRootVariable()] = runtimeRoot;
        }

        string inheritedDescription = inherited.Count == 0 ? "<none>" : string.Join(", ", inherited);
        return $"inheritedRuntimeOverrides={inheritedDescription}; " +
            $"machineWideRuntimeRoot='{runtimeRoot}'; net8Available={hasNet8Runtime}";
    }

    private static string GetArchitectureSpecificRootVariable() => RuntimeInformation.ProcessArchitecture switch
    {
        Architecture.X86 => "DOTNET_ROOT_X86",
        Architecture.Arm64 => "DOTNET_ROOT_ARM64",
        _ => "DOTNET_ROOT_X64",
    };
}

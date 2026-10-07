using System.ComponentModel;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
using Suisharp;

return await SuisharpCommand.RunAsync(args);

internal static class SuisharpCommand
{
    public static async Task<int> RunAsync(string[] arguments)
    {
        if (arguments.Length < 2 || arguments[0] != "run")
        {
            PrintUsage();
            return 2;
        }

        var sourceArgument = arguments[1];
        var separator = Array.IndexOf(arguments, "--", 2);
        if (separator < 0 && arguments.Length > 2)
        {
            Console.Error.WriteLine("アプリ引数は -- の後に指定してください。");
            PrintUsage();
            return 2;
        }

        if (!string.Equals(Path.GetExtension(sourceArgument), ".cs", StringComparison.OrdinalIgnoreCase))
        {
            Console.Error.WriteLine("実行するファイルは .cs にしてください。");
            return 2;
        }

        var sourcePath = Path.GetFullPath(sourceArgument);
        if (!File.Exists(sourcePath))
        {
            Console.Error.WriteLine($"ファイルが見つかりません: {sourceArgument}");
            return 2;
        }

        var appArguments = separator < 0 ? [] : arguments[(separator + 1)..];
        var tempDirectory = GetTempDirectory(sourcePath);
        Directory.CreateDirectory(tempDirectory);
        var projectPath = Path.Combine(tempDirectory, "SuisharpSingleFileApp.csproj");
        WriteProjectIfChanged(projectPath, sourcePath);

        var dotnet = FindDotnetHost();

        var build = new ProcessStartInfo(dotnet)
        {
            UseShellExecute = false,
            WorkingDirectory = tempDirectory
        };
        build.ArgumentList.Add("build");
        build.ArgumentList.Add(projectPath);
        build.ArgumentList.Add("--nologo");
        build.ArgumentList.Add("--verbosity");
        build.ArgumentList.Add("minimal");
        var buildExitCode = await RunProcessAsync(build);
        if (buildExitCode != 0) return buildExitCode;

        var appPath = Path.Combine(tempDirectory, "bin", "Debug", "net10.0", "SuisharpSingleFileApp.dll");
        var run = new ProcessStartInfo(dotnet)
        {
            UseShellExecute = false,
            WorkingDirectory = Environment.CurrentDirectory
        };
        run.ArgumentList.Add(appPath);
        foreach (var appArgument in appArguments) run.ArgumentList.Add(appArgument);
        return await RunProcessAsync(run);
    }

    private static string GetTempDirectory(string sourcePath)
    {
        var identity = Encoding.UTF8.GetBytes(sourcePath);
        var hash = Convert.ToHexString(SHA256.HashData(identity));
        return Path.Combine(Path.GetTempPath(), "suisharp", hash);
    }

    private static string FindDotnetHost()
    {
        var configuredHost = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH");
        if (!string.IsNullOrWhiteSpace(configuredHost) && File.Exists(configuredHost)) return configuredHost;

        var currentProcess = Environment.ProcessPath;
        if (!string.IsNullOrWhiteSpace(currentProcess) &&
            string.Equals(Path.GetFileNameWithoutExtension(currentProcess), "dotnet", StringComparison.OrdinalIgnoreCase))
            return currentProcess;

        foreach (var rootName in new[] { "DOTNET_ROOT_X64", "DOTNET_ROOT", "DOTNET_ROOT_X86" })
        {
            var root = Environment.GetEnvironmentVariable(rootName);
            if (string.IsNullOrWhiteSpace(root)) continue;
            var host = Path.Combine(root, OperatingSystem.IsWindows() ? "dotnet.exe" : "dotnet");
            if (File.Exists(host)) return host;
        }

        return "dotnet";
    }

    private static void WriteProjectIfChanged(string projectPath, string sourcePath)
    {
        var project = new XDocument(
            new XElement("Project",
                new XAttribute("Sdk", "Microsoft.NET.Sdk"),
                new XElement("PropertyGroup",
                    new XElement("OutputType", "Exe"),
                    new XElement("TargetFramework", "net10.0"),
                    new XElement("AssemblyName", "SuisharpSingleFileApp"),
                    new XElement("EnableDefaultCompileItems", "false"),
                    new XElement("ImplicitUsings", "enable"),
                    new XElement("Nullable", "enable")),
                new XElement("ItemGroup",
                    new XElement("FrameworkReference", new XAttribute("Include", "Microsoft.AspNetCore.App")),
                    AssemblyReference("Suisharp.Web", typeof(SuisharpApp).Assembly.Location),
                    AssemblyReference("Suisharp.Core", typeof(Suisharp.Component).Assembly.Location),
                    new XElement("Compile", new XAttribute("Include", sourcePath)))));

        using var writer = new StringWriter();
        project.Save(writer, SaveOptions.None);
        var content = writer.ToString();
        if (!File.Exists(projectPath) || File.ReadAllText(projectPath) != content)
            File.WriteAllText(projectPath, content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    private static XElement AssemblyReference(string name, string path) =>
        new("Reference",
            new XAttribute("Include", name),
            new XElement("HintPath", path),
            new XElement("Private", "true"));

    private static async Task<int> RunProcessAsync(ProcessStartInfo startInfo)
    {
        using var process = new Process { StartInfo = startInfo };
        try
        {
            if (!process.Start()) throw new InvalidOperationException($"プロセスを開始できません: {startInfo.FileName}");
        }
        catch (Win32Exception)
        {
            Console.Error.WriteLine(".NET 10 SDKが見つかりません。dotnetコマンドが実行できる環境で再試行してください。");
            return 127;
        }
        await process.WaitForExitAsync();
        return process.ExitCode;
    }

    private static void PrintUsage() =>
        Console.Error.WriteLine("使い方: suisharp run <file.cs> [-- <app args...>]");
}

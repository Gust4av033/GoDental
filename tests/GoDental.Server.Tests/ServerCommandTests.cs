using System.Diagnostics;
using System.Runtime.InteropServices;

namespace GoDental.Server.Tests;

public sealed class ServerCommandTests
{
    [Theory]
    [InlineData("certificate")]
    [InlineData("http")]
    [InlineData("alternate-listener")]
    [InlineData("extra-argument")]
    public async Task DeploymentCommandFailsClosedWithoutLeakingConfigurationOrStartingAListener(string invalidCase)
    {
        var host = Path.GetFullPath(Path.Combine(RuntimeEnvironment.GetRuntimeDirectory(), "../../..",
            OperatingSystem.IsWindows() ? "dotnet.exe" : "dotnet"));
        using var process = new Process { StartInfo = new(host)
        {
            UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true
        } };
        process.StartInfo.ArgumentList.Add(typeof(Program).Assembly.Location);
        process.StartInfo.ArgumentList.Add("--check-server");
        if (invalidCase == "extra-argument") process.StartInfo.ArgumentList.Add("SYNTHETIC_PRIVATE_CANARY");
        process.StartInfo.Environment["ASPNETCORE_ENVIRONMENT"] = "Production";
        process.StartInfo.Environment["DOTNET_ENVIRONMENT"] = "Production";
        process.StartInfo.Environment["Server__SqlServer"] = "SYNTHETIC_PRIVATE_CANARY";
        process.StartInfo.Environment["Server__Database"] = "GoDental_Development";
        process.StartInfo.Environment["Server__ServiceSid"] = "SYNTHETIC_PRIVATE_CANARY";
        process.StartInfo.Environment["Server__HttpsCertificateThumbprint"] = "invalid-thumbprint";
        process.StartInfo.Environment["Server__Url"] = invalidCase == "http" ? "http://localhost:7443" : "https://localhost:7443";
        if (invalidCase == "alternate-listener")
            process.StartInfo.Environment["Kestrel__Endpoints__Unexpected__Url"] = "http://localhost:7444";
        process.Start();
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        try { await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(30)); }
        catch { if (!process.HasExited) process.Kill(entireProcessTree: true); throw; }
        var output = (await stdout) + (await stderr);
        Assert.Equal(1, process.ExitCode);
        Assert.DoesNotContain("SYNTHETIC_PRIVATE_CANARY", output);
        Assert.DoesNotContain("Now listening", output);
        Assert.DoesNotContain("SqlException", output);
    }
}

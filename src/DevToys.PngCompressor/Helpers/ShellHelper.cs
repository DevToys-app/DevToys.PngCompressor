﻿using System.Diagnostics;
using CommunityToolkit.Diagnostics;
using Microsoft.Extensions.Logging;

namespace DevToys.PngCompressor.Helpers;

internal static class ShellHelper
{
    internal static void OpenFileInShell(string fileOrUrl, string? arguments = null)
    {
        Guard.IsNotNullOrWhiteSpace(fileOrUrl);

        try
        {
            var startInfo = new ProcessStartInfo(fileOrUrl, arguments!);
            startInfo.UseShellExecute = true;
            Process.Start(startInfo);
        }
        catch
        {
            // hack because of this: https://github.com/dotnet/corefx/issues/10361
            if (OperatingSystem.IsWindows())
            {
                fileOrUrl = fileOrUrl.Replace("&", "^&");
                Process.Start(new ProcessStartInfo(fileOrUrl, arguments!) { UseShellExecute = true });
            }
            else if (OperatingSystem.IsLinux() || OperatingSystem.IsFreeBSD())
            {
                Process.Start("xdg-open", new[] { fileOrUrl, arguments! });
            }
            else if (OperatingSystem.IsMacOS() || OperatingSystem.IsMacCatalyst())
            {
                Process.Start("open", new[] { fileOrUrl, arguments! });
            }
            else
            {
                throw;
            }
        }
    }

    internal static async Task<(int exitCode, string error)> RunCommandLineAppAsync(string programPath, string arguments, ILogger logger, CancellationToken cancellationToken)
    {
        if (OperatingSystem.IsMacOS() || OperatingSystem.IsMacCatalyst())
        {
            await MitigateMacOSFileAccessRestrictionsAsync(programPath, logger, cancellationToken);
        }

        Guard.IsTrue(File.Exists(programPath), "executable not found.");

        var processStartInfo
            = new ProcessStartInfo
            {
                FileName = programPath,
                Arguments = arguments,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

        Process process = Process.Start(processStartInfo) ?? throw new InvalidOperationException("Failed to start process.");

        string error = await process.StandardError.ReadToEndAsync(cancellationToken);

        await process.WaitForExitAsync(cancellationToken);

        return new(process.ExitCode, error);
    }

    /// <summary>
    /// On macOS, files downloaded from the internet manually (through the web browser) are automatically put into quarantine, blocking command-line apps like PngQuant and OxiPng to be executed.
    /// This method is a hack to bypass this restriction by adding execute permission and removing the specied file from quarantine.
    /// </summary>
    private static async Task MitigateMacOSFileAccessRestrictionsAsync(string filePath, ILogger logger, CancellationToken cancellationToken)
    {
        Guard.IsTrue(OperatingSystem.IsMacOS() || OperatingSystem.IsMacCatalyst(), "This method is only supported on macOS.");
        Guard.IsTrue(File.Exists(filePath), "file not found.");

        try
        {
            var bashProcessStartInfo = new ProcessStartInfo
            {
                FileName = "/bin/bash",
                Arguments = $"-c \"chmod +x '{filePath}'; xattr -d com.apple.quarantine '{filePath}'\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            var bashProcess = new Process { StartInfo = bashProcessStartInfo };
            bashProcess.Start();
            await bashProcess.WaitForExitAsync(cancellationToken);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to mitigate macOS file access restrictions.");
        }
    }
}

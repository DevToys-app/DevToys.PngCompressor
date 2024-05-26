using System.Runtime.InteropServices;
using CommunityToolkit.Diagnostics;
using Microsoft.Extensions.Logging;

namespace DevToys.PngCompressor.Helpers;

internal static class CompressHelper
{
    internal static async Task CompressAsync(FileInfo imageFile, bool lossless, ILogger logger, CancellationToken cancellationToken)
    {
        Guard.IsTrue(imageFile.Exists, "File does not exist.");
        Guard.IsTrue(string.Equals(imageFile.Extension, ".png", StringComparison.CurrentCultureIgnoreCase), "File is not a PNG file.");

        if (lossless)
        {
            await CompressLosslessAsync(imageFile, logger, cancellationToken);
        }
        else
        {
            await CompressLossyAsync(imageFile, logger, cancellationToken);
        }
    }

    private static async Task CompressLosslessAsync(FileInfo imageFile, ILogger logger, CancellationToken cancellationToken)
    {
        string pngQuantPath = Path.GetDirectoryName(typeof(CompressHelper).Assembly.Location)!;
        if (OperatingSystem.IsWindows())
        {
            pngQuantPath = Path.Combine(pngQuantPath, "runtimes", "win", "native", "oxipng", "oxipng.exe");
        }
        else if (OperatingSystem.IsMacOS() || OperatingSystem.IsMacCatalyst())
        {
            bool isArm64 = Environment.Is64BitOperatingSystem && RuntimeInformation.ProcessArchitecture == Architecture.Arm64;
            if (isArm64)
            {
                pngQuantPath = Path.Combine(pngQuantPath, "runtimes", "osx-arm64", "native", "oxipng", "oxipng");
            }
            else
            {
                pngQuantPath = Path.Combine(pngQuantPath, "runtimes", "osx-x64", "native", "oxipng", "oxipng");
            }
        }
        else if (OperatingSystem.IsLinux())
        {
            bool isArm64 = Environment.Is64BitOperatingSystem && RuntimeInformation.ProcessArchitecture == Architecture.Arm64;
            if (isArm64)
            {
                pngQuantPath = Path.Combine(pngQuantPath, "runtimes", "linux-arm64", "native", "oxipng", "oxipng");
            }
            else
            {
                pngQuantPath = Path.Combine(pngQuantPath, "runtimes", "linux-x64", "native", "oxipng", "oxipng");
            }
        }

        Guard.IsTrue(File.Exists(pngQuantPath), "oxipng executable not found.");

        string arguments = $"--opt max --strip safe --interlace 0 --quiet --fast \"{imageFile.FullName}\"";
        (int exitCode, string error) = await ShellHelper.RunCommandLineAppAsync(pngQuantPath, arguments, logger, cancellationToken);

        if (exitCode != 0)
        {
            throw new InvalidOperationException($"Failed to compress PNG file using OXIPNG. Exit code: {exitCode}. Error: {error}");
        }
    }

    private static async Task CompressLossyAsync(FileInfo imageFile, ILogger logger, CancellationToken cancellationToken)
    {
        string pngQuantPath = Path.GetDirectoryName(typeof(CompressHelper).Assembly.Location)!;
        if (OperatingSystem.IsWindows())
        {
            pngQuantPath = Path.Combine(pngQuantPath, "runtimes", "win", "native", "pngquant", "pngquant.exe");
        }
        else if (OperatingSystem.IsMacOS() || OperatingSystem.IsMacCatalyst())
        {
            pngQuantPath = Path.Combine(pngQuantPath, "runtimes", "osx", "native", "pngquant", "pngquant");
        }
        else if (OperatingSystem.IsLinux())
        {
            pngQuantPath = Path.Combine(pngQuantPath, "runtimes", "linux", "native", "pngquant", "pngquant");
        }

        Guard.IsTrue(File.Exists(pngQuantPath), "pngquant executable not found.");

        string arguments = $"--force --speed=1 --quality=85-99 --skip-if-larger --strip --output \"{imageFile.FullName}\" \"{imageFile.FullName}\"";
        (int exitCode, string error) = await ShellHelper.RunCommandLineAppAsync(pngQuantPath, arguments, logger, cancellationToken);

        if (exitCode != 0 && exitCode != 99 && exitCode != 98)
        {
            throw new InvalidOperationException($"Failed to compress PNG file using PNGQUANT. Exit code: {exitCode}. Error: {error}");
        }
    }
}

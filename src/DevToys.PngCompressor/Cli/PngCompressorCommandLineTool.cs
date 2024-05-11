using System.ComponentModel.Composition;
using DevToys.Api;
using DevToys.PngCompressor.Gui;
using DevToys.PngCompressor.Helpers;
using Microsoft.Extensions.Logging;
using OneOf;

namespace DevToys.PngCompressor.Cli;

[Export(typeof(ICommandLineTool))]
[Name("PngCompressor")]
[CommandName(
    Name = "pngcompressor",
    Alias = "pngc",
    ResourceManagerBaseName = "DevToys.PngCompressor.Strings.PngCompressor",
    DescriptionResourceName = nameof(Strings.PngCompressor.Description))]
internal sealed class PngCompressorCommandLineTool : ICommandLineTool
{
    [CommandLineOption(
        Name = "input",
        Alias = "i",
        IsRequired = true,
        DescriptionResourceName = nameof(Strings.PngCompressor.InputOptionDescription))]
    internal OneOf<DirectoryInfo, FileInfo>[] Input { get; set; } = null!;

    [CommandLineOption(
        Name = "mode",
        Alias = "m",
        IsRequired = true,
        DescriptionResourceName = nameof(Strings.PngCompressor.ModeOptionDescription))]
    internal CompressionMode Mode { get; set; }

    [CommandLineOption(
        Name = "output",
        Alias = "o",
        DescriptionResourceName = nameof(Strings.PngCompressor.OutputDirectoryOptionDescription))]
    internal OneOf<DirectoryInfo, FileInfo>? Output { get; set; }

    [CommandLineOption(
        Name = "silent",
        Alias = "s",
        DescriptionResourceName = nameof(Strings.PngCompressor.SilentOptionDescription))]
    internal bool Silent { get; set; } = false;

    public async ValueTask<int> InvokeAsync(ILogger logger, CancellationToken cancellationToken)
    {
        try
        {
            if (Input is null || Input.Length == 0)
            {
                return -1;
            }

            if (Input.Length >= 1)
            {
                if (Output.HasValue && Output.Value.IsT1)
                {
                    Console.Error.WriteLine("Output must be a directory when passing more than one file as input.");
                    return -1;
                }
            }

            for (int i = 0; i < Input.Length; i++)
            {
                int exitCode = await TreatDirectoryOrFileAsync(Input[i], enforcePng: true, logger, cancellationToken);
                if (exitCode != 0)
                {
                    return exitCode;
                }
            }

            return 0;
        }
        catch
        {
            return -1;
        }
    }

    private async Task<int> TreatDirectoryOrFileAsync(OneOf<DirectoryInfo, FileInfo> directoryOrFile, bool enforcePng, ILogger logger, CancellationToken cancellationToken)
    {
        if (directoryOrFile.IsT0)
        {
            foreach (FileInfo fileInDirectory in directoryOrFile.AsT0.EnumerateFiles())
            {
                int exitCode = await TreatDirectoryOrFileAsync(fileInDirectory, enforcePng: false, logger, cancellationToken);
                if (exitCode != 0)
                {
                    return exitCode;
                }
            }
        }
        else if (directoryOrFile.IsT1)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (enforcePng && !string.Equals(directoryOrFile.AsT1.Extension, ".png", StringComparison.OrdinalIgnoreCase))
            {
                Console.Error.WriteLine($"File '{directoryOrFile.AsT1.FullName}' is not a PNG file.");
                return -1;
            }

            return await CompressAsync(directoryOrFile.AsT1, logger, cancellationToken);
        }

        return 0;
    }

    private async Task<int> CompressAsync(FileInfo imageFile, ILogger logger, CancellationToken cancellationToken)
    {
        FileInfo destinationFile;

        if (!Output.HasValue)
        {
            destinationFile = imageFile;
        }
        else if (Output.Value.IsT0)
        {
            destinationFile = new FileInfo(Path.Combine(Output.Value.AsT0.FullName, imageFile.Name));
        }
        else
        {
            destinationFile = Output.Value.AsT1;
        }

        if (!imageFile.Exists)
        {
            Console.Error.WriteLine($"File '{imageFile.FullName}' does not exist.");
            return -1;
        }

        if (imageFile != destinationFile)
        {
            imageFile.CopyTo(destinationFile.FullName, overwrite: true);
        }

        try
        {
            long originalSize = imageFile.Length;

            await CompressHelper.CompressAsync(destinationFile, Mode == CompressionMode.Lossless, logger, cancellationToken);

            if (!Silent)
            {
                destinationFile.Refresh();
                long newSize = destinationFile.Length;
                string compressedFileSize = HumanizeFileSize(newSize, Strings.PngCompressor.FileSizeDisplay);
                string originalFileSize = HumanizeFileSize(originalSize, Strings.PngCompressor.FileSizeDisplay);
                string compressedPercentage = string.Format(Strings.PngCompressor.CompressionRatio, (int)((originalSize - newSize) / (double)originalSize * 100));

                Console.WriteLine($"'{imageFile.Name}' : {originalFileSize} -> {compressedFileSize} ({compressedPercentage})");
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Failed to compress file '{imageFile.FullName}'. Error: {ex.Message}");
            return -1;
        }

        return 0;
    }

    private static string HumanizeFileSize(double fileSize, string fileSizeDisplay)
    {
        int order = 0;
        while (fileSize >= 1024 && order < PngCompressorTaskItemGui.SizesStrings.Length - 1)
        {
            order++;
            fileSize /= 1024;
        }

        string fileSizeString = string.Format(fileSizeDisplay, fileSize, PngCompressorTaskItemGui.SizesStrings[order]);
        return fileSizeString;
    }
}

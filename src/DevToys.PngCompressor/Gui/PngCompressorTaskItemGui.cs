using CommunityToolkit.Diagnostics;
using DevToys.Api;
using DevToys.PngCompressor.Helpers;
using Microsoft.Extensions.Logging;
using static DevToys.Api.GUI;

namespace DevToys.PngCompressor.Gui;

internal sealed class PngCompressorTaskItemGui : IUIListItem
{
    internal static readonly string[] SizesStrings
        = {
            Strings.PngCompressor.Bytes,
            Strings.PngCompressor.Kilobytes,
            Strings.PngCompressor.Megabytes,
            Strings.PngCompressor.Gigabytes,
            Strings.PngCompressor.Terabytes
        };

    private readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
    private readonly CancellationToken _cancellationToken;
    private readonly IFileStorage _fileStorage;
    private readonly Lazy<IUIElement> _ui;
    private readonly IUISetting _setting = Setting();
    private readonly IUIStack _interactiveElementStack = Stack();
    private readonly PngCompressorGuiTool _pngCompressorGuiTool;
    private readonly ILogger _logger;
    private readonly bool _lossless;
    private readonly SandboxedFileReader _inputFile;

    private string? _errorMessage;
    private string? _compressedFileSize;
    private string? _compressedPercentage;
    private FileInfo? _compressedFile;

    internal PngCompressorTaskItemGui(
        PngCompressorGuiTool pngCompressorGuiTool,
        ILogger logger,
        IFileStorage fileStorage,
        SandboxedFileReader inputFile,
        bool lossless)
    {
        Guard.IsNotNull(pngCompressorGuiTool);
        Guard.IsNotNull(logger);
        Guard.IsNotNull(inputFile);
        Guard.IsNotNull(fileStorage);

        _pngCompressorGuiTool = pngCompressorGuiTool;
        _logger = logger;
        _fileStorage = fileStorage;
        _lossless = lossless;
        Value = inputFile;
        _inputFile = inputFile;
        _cancellationToken = _cancellationTokenSource.Token;

        UpdateUICompressionInProgress();

        ComputePropertiesAsync().ForgetSafely();

        _ui
            = new Lazy<IUIElement>(
                _setting
                    .Icon("FluentSystemIcons", '\uF488')
                    .Title(inputFile.FileName)

                    .InteractiveElement(
                        _interactiveElementStack
                            .Horizontal()
                            .LargeSpacing()));

        CompressAsync().ForgetSafely();
    }

    public IUIElement UIElement => _ui.Value;

    public object? Value { get; }

    internal void SaveInFolder(string folderPath)
    {
        if (_compressedFile is not null)
        {
            // _compressedFile can be null if the compression failed, and it's OK here.
            // This method is called from the Save All button, and the button doesn't know whether a file got correctly compressed or not.
            // Just opt-out if the file is not compressed.
            string filePath = Path.Combine(folderPath, _inputFile.FileName);
            _compressedFile.CopyTo(filePath, overwrite: true);
        }
    }

    private async ValueTask OnSaveAsButtonClickAsync()
    {
        Guard.IsNotNull(_compressedFile);

        // Ask the user to pick up a file.
        using Stream? outputFileStream = await _fileStorage.PickSaveFileAsync(".png");
        if (outputFileStream is not null)
        {
            using Stream compressedFileStream = _compressedFile.OpenRead();
            await compressedFileStream.CopyToAsync(outputFileStream);
        }
    }

    private ValueTask OnDeleteButtonClickAsync()
    {
        _pngCompressorGuiTool.ItemList.Items.Remove(this);
        return ValueTask.CompletedTask;
    }

    private void OnPreviewButtonClick()
    {
        ShellHelper.OpenFileInShell(_compressedFile!.FullName);
    }

    private void OnCancel()
    {
        _cancellationTokenSource.Cancel();
        _cancellationTokenSource.Dispose();

        UpdateUICompressionCompleted();
    }

    private async ValueTask OnShowErrorAsync()
    {
        await _pngCompressorGuiTool.View
            .OpenDialogAsync(
                Label().Text(_errorMessage),
                isDismissible: true);
    }

    private async Task CompressAsync()
    {
        try
        {
            await TaskSchedulerAwaiter.SwitchOffMainThreadAsync(_cancellationToken);

            // Create a temporary file
            FileInfo tempFile = _fileStorage.CreateSelfDestroyingTempFile(desiredFileExtension: "png");

            // Copy the input file into the temporary file.
            using (Stream tempFileStream = tempFile.OpenWrite())
            {
                await _inputFile.CopyFileContentToAsync(tempFileStream, _cancellationToken);
            }

            tempFile.Refresh();
            long originalSize = tempFile.Length;

            // Compress the temporary file.
            await CompressHelper.CompressAsync(tempFile, _lossless, _logger, _cancellationToken);

            tempFile.Refresh();
            long newSize = tempFile.Length;
            _compressedFileSize = HumanizeFileSize(newSize, Strings.PngCompressor.FileSizeDisplay);
            _compressedPercentage = string.Format(Strings.PngCompressor.CompressionRatio, (int)((originalSize - newSize) / (double)originalSize * 100));
            _compressedFile = tempFile;
        }
        catch (OperationCanceledException)
        {
            // Ignore
        }
        catch (Exception ex)
        {
            _errorMessage = ex.Message;
            _logger.LogError(ex, "Failed to compress the file '{FileName}'.", _inputFile.FileName);
        }
        finally
        {
            UpdateUICompressionCompleted();
        }
    }

    private void UpdateUICompressionInProgress()
    {
        _interactiveElementStack
            .WithChildren(
                ProgressRing()
                    .StartIndeterminateProgress(),
                Button()
                    .Icon("FluentSystemIcons", '\uF75A')
                    .OnClick(OnCancel));
    }

    private void UpdateUICompressionCompleted()
    {
        if (_cancellationToken.IsCancellationRequested)
        {
            // Compression got canceled.
            _interactiveElementStack
                .WithChildren(
                    Button()
                        .Icon("FluentSystemIcons", '\uF34C')
                        .OnClick(OnDeleteButtonClickAsync));
        }
        else
        {
            if (!string.IsNullOrEmpty(_errorMessage))
            {
                // Compression failed.
                _interactiveElementStack
                    .SmallSpacing()
                    .WithChildren(
                        Button()
                            .Icon("FluentSystemIcons", '\uF869')
                            .OnClick(OnShowErrorAsync),
                        Button()
                            .Icon("FluentSystemIcons", '\uF34C')
                            .OnClick(OnDeleteButtonClickAsync));
            }
            else
            {
                // Compression succeeded.
                _interactiveElementStack
                    .WithChildren(
                        Stack()
                            .Vertical()
                            .NoSpacing()

                            .WithChildren(
                                Label()
                                    .Text(_compressedPercentage)
                                    .AlignHorizontally(UIHorizontalAlignment.Right),
                                Label()
                                    .Style(UILabelStyle.Caption)
                                    .Text(_compressedFileSize)),

                        Label()
                            .Text(_lossless ? Strings.PngCompressor.LosslessIndication : Strings.PngCompressor.LossyIndication)
                            .Style(UILabelStyle.BodyStrong),

                        Stack()
                            .Horizontal()
                            .SmallSpacing()

                            .WithChildren(
                                Button()
                                    .Icon("FluentSystemIcons", '\uE5F2')
                                    .OnClick(OnPreviewButtonClick),
                                Button()
                                    .Icon("FluentSystemIcons", '\uF67F')
                                    .OnClick(OnSaveAsButtonClickAsync),
                                Button()
                                    .Icon("FluentSystemIcons", '\uF34C')
                                    .OnClick(OnDeleteButtonClickAsync)));
            }
        }
    }

    private async Task ComputePropertiesAsync()
    {
        await TaskSchedulerAwaiter.SwitchOffMainThreadAsync(_cancellationToken);

        using Stream fileStream = await _inputFile.GetNewAccessToFileContentAsync(_cancellationToken);

        long storageFileSize = fileStream.Length;
        string? fileSize = HumanizeFileSize(storageFileSize, Strings.PngCompressor.FileSizeDisplay);

        _setting.Description(fileSize);
    }

    private static string HumanizeFileSize(double fileSize, string fileSizeDisplay)
    {
        int order = 0;
        while (fileSize >= 1024 && order < SizesStrings.Length - 1)
        {
            order++;
            fileSize /= 1024;
        }

        string fileSizeString = string.Format(fileSizeDisplay, fileSize, SizesStrings[order]);
        return fileSizeString;
    }
}

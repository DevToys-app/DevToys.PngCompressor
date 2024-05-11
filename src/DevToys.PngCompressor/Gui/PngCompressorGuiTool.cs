using System.Collections.Specialized;
using System.ComponentModel.Composition;
using DevToys.Api;
using Microsoft.Extensions.Logging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using static DevToys.Api.GUI;

namespace DevToys.PngCompressor.Gui;

[Export(typeof(IGuiTool))]
[Name("PNG Compressor")]
[ToolDisplayInformation(
    IconFontName = "FluentSystemIcons",
    IconGlyph = '\uF488',
    GroupName = PredefinedCommonToolGroupNames.Graphic,
    ResourceManagerAssemblyIdentifier = nameof(PngCompressorResourceAssemblyIdentifier),
    ResourceManagerBaseName = "DevToys.PngCompressor.Strings.PngCompressor",
    ShortDisplayTitleResourceName = nameof(Strings.PngCompressor.ShortDisplayTitle),
    LongDisplayTitleResourceName = nameof(Strings.PngCompressor.LongDisplayTitle),
    DescriptionResourceName = nameof(Strings.PngCompressor.Description),
    AccessibleNameResourceName = nameof(Strings.PngCompressor.AccessibleName))]
[AcceptedDataTypeName(PredefinedCommonDataTypeNames.Image)]
[AcceptedDataTypeName("PngImageFile")]
[AcceptedDataTypeName("PngImageFiles")]
internal sealed class PngCompressorGuiTool : IGuiTool, IDisposable
{
    /// <summary>
    /// Whether the compression should be lossless or lossy.
    /// </summary>
    private static readonly SettingDefinition<bool> lossless
        = new(
            name: $"{nameof(PngCompressorGuiTool)}.{nameof(lossless)}",
            defaultValue: false);

    private readonly ILogger _logger;
    private readonly ISettingsProvider _settingsProvider;
    private readonly IFileStorage _fileStorage;
    private readonly IUIList _itemList = List("png-compressor-task-list");
    private readonly IUISwitch _compressionModeSwitch = Switch("png-compressor-compression-mode-switch");
    private readonly IUIButton _saveAllButton = Button("png-compressor-save-all-button");
    private readonly IUIButton _deleteAllButton = Button("png-compressor-delete-all-button");

    private UIToolView? _view;

    [ImportingConstructor]
    public PngCompressorGuiTool(ISettingsProvider settingsProvider, IFileStorage fileStorage)
    {
        _logger = this.Log();
        _settingsProvider = settingsProvider;
        _fileStorage = fileStorage;

        _itemList.Items.CollectionChanged += OnItemListItemsChanged;
        if (_settingsProvider.GetSetting(lossless))
        {
            _compressionModeSwitch.On();
        }
        else
        {
            _compressionModeSwitch.Off();
        }
    }

    internal IUIList ItemList => _itemList;

    public UIToolView View
    {
        get
        {
            _view ??= new(
                isScrollable: true,
                Stack()
                    .Vertical()
                    .LargeSpacing()

                    .WithChildren(
                        Stack()
                            .Vertical()
                            .SmallSpacing()

                            .WithChildren(
                                Label()
                                    .Text(Strings.PngCompressor.ConfigurationTitle),

                                Setting("png-compressor-compression-mode-setting")
                                    .Icon("FluentSystemIcons", '\uF18D')
                                    .Title(Strings.PngCompressor.CompressionModeTitle)
                                    .Description(Strings.PngCompressor.CompressionModeDescription)
                                    .InteractiveElement(
                                        _compressionModeSwitch
                                            .OnText(Strings.PngCompressor.CompressionModeLossless)
                                            .OffText(Strings.PngCompressor.CompressionModeLossy)
                                            .OnToggle(OnCompressionModeChanged))),

                        FileSelector()
                            .CanSelectManyFiles()
                            .LimitFileTypesTo(".png")
                            .OnFilesSelected(OnFilesSelected),

                        Stack()
                            .Horizontal()
                            .AlignHorizontally(UIHorizontalAlignment.Right)
                            .MediumSpacing()

                            .WithChildren(
                                _saveAllButton
                                    .Icon("FluentSystemIcons", '\uF67F')
                                    .Text(Strings.PngCompressor.SaveAll)
                                    .AccentAppearance()
                                    .Disable()
                                    .OnClick(OnSaveAllAsync),

                                _deleteAllButton
                                    .Icon("FluentSystemIcons", '\uF34C')
                                    .Text(Strings.PngCompressor.DeleteAll)
                                    .Disable()
                                    .OnClick(OnDeleteAllAsync)),

                        _itemList
                            .ForbidSelectItem()));

            return _view;
        }
    }

    public async void OnDataReceived(string dataTypeName, object? parsedData)
    {
        if (dataTypeName == PredefinedCommonDataTypeNames.Image && parsedData is Image<Rgba32> image)
        {
            FileInfo temporaryFile = _fileStorage.CreateSelfDestroyingTempFile("png");

            using (image)
            {
                using Stream fileStream = _fileStorage.OpenWriteFile(temporaryFile.FullName, replaceIfExist: true);
                await image.SaveAsPngAsync(fileStream);
            }

            _itemList.Items.Insert(0, CreateTaskItemGui(SandboxedFileReader.FromFileInfo(temporaryFile)));
        }
        else if (dataTypeName == "PngImageFile" && parsedData is FileInfo file)
        {
            _itemList.Items.Insert(0, CreateTaskItemGui(SandboxedFileReader.FromFileInfo(file)));
        }
        else if (dataTypeName == "PngImageFiles" && parsedData is FileInfo[] files)
        {
            for (int i = 0; i < files.Length; i++)
            {
                _itemList.Items.Insert(0, CreateTaskItemGui(SandboxedFileReader.FromFileInfo(files[i])));
            }
        }
    }

    public void Dispose()
    {
        for (int i = 0; i < _itemList.Items.Count; i++)
        {
            if (_itemList.Items[i] is IDisposable disposableItem)
                disposableItem.Dispose();
        }

        _itemList.Items.CollectionChanged -= OnItemListItemsChanged;
    }

    private void OnCompressionModeChanged(bool isLossless)
    {
        _settingsProvider.SetSetting(PngCompressorGuiTool.lossless, isLossless);
    }

    private void OnFilesSelected(SandboxedFileReader[] files)
    {
        for (int i = 0; i < files.Length; i++)
        {
            _itemList.Items.Insert(
                0,
                CreateTaskItemGui(files[i]));
        }
    }

    private async ValueTask OnSaveAllAsync()
    {
        string? folderPath = await _fileStorage.PickFolderAsync();

        if (!string.IsNullOrEmpty(folderPath))
        {
            await TaskSchedulerAwaiter.SwitchOffMainThreadAsync(CancellationToken.None);

            for (int i = 0; i < _itemList.Items.Count; i++)
            {
                if (_itemList.Items[i] is PngCompressorTaskItemGui item)
                {
                    item.SaveInFolder(folderPath);
                }
            }
        }
    }

    private ValueTask OnDeleteAllAsync()
    {
        for (int i = 0; i < _itemList.Items.Count; i++)
        {
            if (_itemList.Items[i] is IDisposable disposableItem)
                disposableItem.Dispose();
        }

        _itemList.Items.Clear();

        return ValueTask.CompletedTask;
    }

    private void OnItemListItemsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        UpdateButtonsState();
    }

    private void UpdateButtonsState()
    {
        bool hasItems = _itemList.Items.Count > 0;
        if (hasItems)
        {
            _saveAllButton.Enable();
            _deleteAllButton.Enable();
        }
        else
        {
            _saveAllButton.Disable();
            _deleteAllButton.Disable();
        }
    }

    private PngCompressorTaskItemGui CreateTaskItemGui(SandboxedFileReader file)
    {
        return new PngCompressorTaskItemGui(
            this,
            _logger,
            _fileStorage,
            file,
            _settingsProvider.GetSetting(lossless));
    }
}

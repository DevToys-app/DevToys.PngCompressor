using System.ComponentModel.Composition;
using DevToys.Api;

namespace DevToys.PngCompressor.SmartDetection;

[Export(typeof(IDataTypeDetector))]
[DataTypeName("PngImageFiles", baseName: PredefinedCommonDataTypeNames.Files)]
internal sealed class PngFilesDataTypeDetector : IDataTypeDetector
{
    public ValueTask<DataDetectionResult> TryDetectDataAsync(object data, DataDetectionResult? resultFromBaseDetector, CancellationToken cancellationToken)
    {
        if (resultFromBaseDetector is not null
            && resultFromBaseDetector.Data is FileInfo[] dataFiles)
        {
            var files = new List<FileInfo>();
            for (int i = 0; i < dataFiles.Length; i++)
            {
                if (string.Equals(dataFiles[i].Extension, ".png", StringComparison.OrdinalIgnoreCase))
                    files.Add(dataFiles[i]);
            }

            if (files.Count > 0)
                return ValueTask.FromResult(new DataDetectionResult(Success: true, Data: files.ToArray()));
        }

        return ValueTask.FromResult(DataDetectionResult.Unsuccessful);
    }
}

using System.ComponentModel.Composition;
using DevToys.Api;

namespace DevToys.PngCompressor.SmartDetection;

[Export(typeof(IDataTypeDetector))]
[DataTypeName("PngImageFile", baseName: PredefinedCommonDataTypeNames.File)]
internal sealed class PngFileDataTypeDetector : IDataTypeDetector
{
    public ValueTask<DataDetectionResult> TryDetectDataAsync(object data, DataDetectionResult? resultFromBaseDetector, CancellationToken cancellationToken)
    {
        if (resultFromBaseDetector is not null
            && resultFromBaseDetector.Data is FileInfo dataFile)
        {
            if (string.Equals(dataFile.Extension, ".png", StringComparison.OrdinalIgnoreCase))
                return ValueTask.FromResult(new DataDetectionResult(Success: true, Data: dataFile));
        }

        return ValueTask.FromResult(DataDetectionResult.Unsuccessful);
    }
}

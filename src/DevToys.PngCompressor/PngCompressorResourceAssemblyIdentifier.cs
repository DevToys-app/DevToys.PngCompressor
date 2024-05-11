using System.ComponentModel.Composition;
using DevToys.Api;

namespace DevToys.PngCompressor;

[Export(typeof(IResourceAssemblyIdentifier))]
[Name(nameof(PngCompressorResourceAssemblyIdentifier))]
internal sealed class PngCompressorResourceAssemblyIdentifier : IResourceAssemblyIdentifier
{
    public ValueTask<FontDefinition[]> GetFontDefinitionsAsync()
    {
        throw new NotImplementedException();
    }
}

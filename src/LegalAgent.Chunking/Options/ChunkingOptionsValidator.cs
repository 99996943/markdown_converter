using Microsoft.Extensions.Options;

namespace LegalAgent.Chunking;

/// <summary>Validates <see cref="ChunkingOptions"/>: the length limit is at least <see cref="MinChunkLength"/> characters.</summary>
public sealed class ChunkingOptionsValidator : IValidateOptions<ChunkingOptions>
{
    /// <summary>Smallest allowed <see cref="ChunkingOptions.MaxChunkLength"/> (spec 004, FR-221).</summary>
    public const int MinChunkLength = 200;

    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, ChunkingOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return options.MaxChunkLength < MinChunkLength
            ? ValidateOptionsResult.Fail($"MaxChunkLength musi wynosić co najmniej {MinChunkLength} znaków (jest {options.MaxChunkLength}).")
            : ValidateOptionsResult.Success;
    }
}

namespace LegalAgent.Downloads;

/// <summary>One planned download: position, address and target file name.</summary>
/// <param name="Index">1-based position in the list.</param>
/// <param name="Address">Address given by the user.</param>
/// <param name="FileName">Target file name in the output directory.</param>
public sealed record PlannedDownload(int Index, Uri Address, string FileName);

/// <summary>Derives deterministic, file-system-safe file names from addresses (FR-321, research R7).</summary>
public static class FileNamePlanner
{
    /// <summary>Plans file names for the addresses; the result depends only on the list and its order.</summary>
    /// <param name="addresses">Addresses in order.</param>
    /// <returns>One planned download per address, in order.</returns>
    public static IReadOnlyList<PlannedDownload> Plan(IReadOnlyList<Uri> addresses) =>
        throw new NotImplementedException();
}

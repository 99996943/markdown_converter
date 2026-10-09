using LegalAgent.Corpus.Content;

namespace LegalAgent.Corpus.Planning;

/// <summary>Plans the base documents of a corpus run.</summary>
public static class CorpusPlanner
{
    /// <summary>Plans the base documents.</summary>
    /// <param name="content">The loaded content.</param>
    /// <param name="parameters">The run parameters.</param>
    /// <returns>The plan.</returns>
    /// <exception cref="CorpusGenerationException">The content cannot satisfy the parameters.</exception>
    public static CorpusPlan Plan(ContentLibrary content, RunParameters parameters)
        => throw new NotImplementedException();
}

namespace LegalAgent.Faq.Model;

/// <summary>Step of the FAQ generation.</summary>
public enum FaqStep
{
    /// <summary>Candidate questions and answers for one document.</summary>
    Candidates,

    /// <summary>Selection of the final items from all candidates.</summary>
    Selection,
}

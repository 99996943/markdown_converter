namespace LegalAgent.Faq;

/// <summary>JSON schemas of the model responses (contracts/model-exchange.md), for structured outputs in strict mode.</summary>
public static class FaqSchemas
{
    /// <summary>Schema of the candidate step: <c>{"candidates":[{"question","answer","unit"}]}</c>.</summary>
    public const string Candidates =
        """
        {
          "type": "object",
          "additionalProperties": false,
          "required": ["candidates"],
          "properties": {
            "candidates": {
              "type": "array",
              "items": {
                "type": "object",
                "additionalProperties": false,
                "required": ["question", "answer", "unit"],
                "properties": {
                  "question": { "type": "string" },
                  "answer": { "type": "string" },
                  "unit": { "type": "string" }
                }
              }
            }
          }
        }
        """;

    /// <summary>Schema of the selection step: <c>{"items":[{"question","answer","basedOn":[…],"sources":[…]}]}</c>.</summary>
    public const string Selection =
        """
        {
          "type": "object",
          "additionalProperties": false,
          "required": ["items"],
          "properties": {
            "items": {
              "type": "array",
              "items": {
                "type": "object",
                "additionalProperties": false,
                "required": ["question", "answer", "basedOn", "sources"],
                "properties": {
                  "question": { "type": "string" },
                  "answer": { "type": "string" },
                  "basedOn": { "type": "array", "items": { "type": "string" } },
                  "sources": {
                    "type": "array",
                    "items": {
                      "type": "object",
                      "additionalProperties": false,
                      "required": ["documentId", "unit"],
                      "properties": {
                        "documentId": { "type": "string" },
                        "unit": { "type": "string" }
                      }
                    }
                  }
                }
              }
            }
          }
        }
        """;
}

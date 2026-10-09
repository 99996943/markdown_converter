using System.Security.Cryptography;
using System.Text;

namespace LegalAgent.Corpus.Pdf;

/// <summary>Makes the trailer /ID of a PDF deterministic (research R4).</summary>
internal static class PdfIdNormalizer
{
    private const int HexLength = 32;
    private static readonly byte[] IdMarker = Encoding.ASCII.GetBytes("/ID");

    /// <summary>
    /// Replaces both hex strings of the trailer <c>/ID</c> array in place (same length, so xref offsets stay valid)
    /// with a SHA-256 digest of the file computed with the ID digits zeroed. Returns the input unchanged when no /ID exists.
    /// </summary>
    public static byte[] Normalize(byte[] pdf)
    {
        ArgumentNullException.ThrowIfNull(pdf);

        int first = -1;
        int second = -1;
        int marker = pdf.AsSpan().LastIndexOf(IdMarker);
        if (marker >= 0)
        {
            int open1 = IndexOfByte(pdf, (byte)'<', marker + IdMarker.Length);
            if (open1 >= 0 && IsHex(pdf, open1 + 1) && pdf[open1 + 1 + HexLength] == (byte)'>')
            {
                int open2 = open1 + 2 + HexLength;
                if (open2 < pdf.Length && pdf[open2] == (byte)'<' && IsHex(pdf, open2 + 1) && pdf[open2 + 1 + HexLength] == (byte)'>')
                {
                    first = open1 + 1;
                    second = open2 + 1;
                }
            }
        }

        if (first < 0)
        {
            return pdf;
        }

        byte[] result = (byte[])pdf.Clone();
        Array.Fill(result, (byte)'0', first, HexLength);
        Array.Fill(result, (byte)'0', second, HexLength);
        byte[] hash = SHA256.HashData(result);
        string hex1 = Convert.ToHexString(hash.AsSpan(0, 16));
        string hex2 = Convert.ToHexString(hash.AsSpan(16, 16));
        Encoding.ASCII.GetBytes(hex1, 0, HexLength, result, first);
        Encoding.ASCII.GetBytes(hex2, 0, HexLength, result, second);
        return result;
    }

    private static int IndexOfByte(byte[] data, byte value, int start)
    {
        // Only a short gap ("[ ") is expected between the marker and the first string.
        int end = Math.Min(data.Length, start + 8);
        for (int i = start; i < end; i++)
        {
            if (data[i] == value)
            {
                return i;
            }
        }

        return -1;
    }

    private static bool IsHex(byte[] data, int start)
    {
        if (start + HexLength >= data.Length)
        {
            return false;
        }

        for (int i = start; i < start + HexLength; i++)
        {
            byte b = data[i];
            bool ok = b is >= (byte)'0' and <= (byte)'9' or >= (byte)'A' and <= (byte)'F' or >= (byte)'a' and <= (byte)'f';
            if (!ok)
            {
                return false;
            }
        }

        return true;
    }
}

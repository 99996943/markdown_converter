# Error fixtures

Small committed binaries used by `PdfDocumentOpenerTests`. Python is NOT a build or test
dependency: the files are produced once and committed.

| File | Purpose | How it was produced |
|------|---------|---------------------|
| `not-a-pdf.txt` | Input without a `%PDF-` header | Hand-written one-line text file |
| `truncated.pdf` | Damaged structure (no xref/trailer) | Synthetic 3-page PDF built by `SyntheticPdfBuilder`, cut to its first half (`bytes[..(length / 2)]`) |
| `encrypted.pdf` | User password required (`user-secret`) | Synthetic PDF encrypted with pypdf (AES-256, owner password `owner-secret`) |
| `permissions-only.pdf` | Empty user password, owner password set, only printing allowed: MUST open and convert normally | Same source encrypted with pypdf (AES-256, `user_password=""`) |
| `broken-page.pdf` | Valid 3-page PDF whose page 2 cannot be read (PdfPig throws on `GetPage(2)`); pages 1 and 3 read fine | Synthetic PDF from `SyntheticPdfBuilder`, page 2 `/Contents` reference patched to a missing object (see "broken-page.pdf" below) |

## Reproduction

qpdf is not required. Tools used: Python 3.12.4, `pypdf` 6.19.0, `cryptography` 50.0.2.

1. Build the base PDF with `SyntheticPdfBuilder` (three pages, one line each):
   `Art. 1. Przykladowy tekst dokumentu.`, `Art. 2. Drugi akapit.`, `Art. 3. Trzeci akapit.`
   and write `Build()` bytes to `plain.pdf` (for example from a throw-away test).
   `truncated.pdf` is the first half of those bytes.
2. Create a virtual environment outside the repository and install the tools:

   ```text
   python -m venv <tmp>/pdfvenv
   <tmp>/pdfvenv/Scripts/python -m pip install pypdf==6.19.0 cryptography==50.0.2
   ```

3. Run a script (kept outside the repository) equivalent to:

   ```python
   from pypdf import PdfReader, PdfWriter
   from pypdf.constants import UserAccessPermissions

   w = PdfWriter(clone_from=PdfReader("plain.pdf"))
   w.encrypt(user_password="user-secret", owner_password="owner-secret", algorithm="AES-256")
   w.write(open("encrypted.pdf", "wb"))

   w = PdfWriter(clone_from=PdfReader("plain.pdf"))
   w.encrypt(user_password="", owner_password="owner-secret",
             permissions_flag=UserAccessPermissions.PRINT, algorithm="AES-256")
   w.write(open("permissions-only.pdf", "wb"))
   ```

Encryption uses random salts, so regenerating produces different bytes; the committed files are the
reference.

## broken-page.pdf

Produced in C# only (no Python), from a throw-away test kept outside the repository. Base PDF:
`SyntheticPdfBuilder` with three pages, text at (50, 80): `Art. 1. Przykladowy tekst dokumentu.`,
`Art. 2. Drugi akapit.`, `Art. 3. Trzeci akapit.`. In the output page 2 is object `7 0 obj` and its
content stream is object 10. The patch keeps the file length (so the xref table stays valid) and
points the page's `/Contents` at a non-existent object 99:

```csharp
var b = builder.Build();
var s = Encoding.Latin1.GetString(b);
int page2 = s.IndexOf("7 0 obj", StringComparison.Ordinal);
int ct = s.IndexOf("/Contents 10 0 R", page2, StringComparison.Ordinal);
b[ct + 10] = (byte)'9';   // "/Contents 10 0 R" -> "/Contents 99 0 R"
b[ct + 11] = (byte)'9';
File.WriteAllBytes("broken-page.pdf", b);
```

Verified with PdfPig 0.1.16 and the library's options (`UseLenientParsing = true`,
`SkipMissingFonts = false`): the document opens with 3 pages; the letters of pages 1 and 3 read as
`Art. 1. Przykladowy tekst dokumentu.` and `Art. 3. Trzeci akapit.`; `GetPage(2)` throws
`System.InvalidOperationException: Failed to parse the content for the page: 2` (no inner exception).

Defects that lenient parsing silently repairs and therefore do NOT work for this fixture: corrupting
the bytes of page 2's FlateDecode content stream (with or without a valid `78 9C` zlib header) makes
the page read as empty (zero letters) instead of throwing.

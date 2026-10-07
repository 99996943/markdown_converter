# Error fixtures

Small committed binaries used by `PdfDocumentOpenerTests`. Python is NOT a build or test
dependency: the files are produced once and committed.

| File | Purpose | How it was produced |
|------|---------|---------------------|
| `not-a-pdf.txt` | Input without a `%PDF-` header | Hand-written one-line text file |
| `truncated.pdf` | Damaged structure (no xref/trailer) | Synthetic 3-page PDF built by `SyntheticPdfBuilder`, cut to its first half (`bytes[..(length / 2)]`) |
| `encrypted.pdf` | User password required (`user-secret`) | Synthetic PDF encrypted with pypdf (AES-256, owner password `owner-secret`) |
| `permissions-only.pdf` | Empty user password, owner password set, only printing allowed: MUST open and convert normally | Same source encrypted with pypdf (AES-256, `user_password=""`) |

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

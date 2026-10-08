namespace NICEDA.ApiService.Legal;

/// <summary>
/// The "Legal" SharePoint's "Before" shape: a folder path is the *only*
/// organizing principle. No tags, no classification, no customer id field
/// — which customer a document belongs to (and whether it's public or
/// restricted) can only be guessed from the path string itself.
/// </summary>
public sealed record LegalDocument(
    string Id,
    string FolderPath,
    string FileName,
    int SizeKb,
    string ModifiedDateText);

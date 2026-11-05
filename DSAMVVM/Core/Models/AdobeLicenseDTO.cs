namespace DSAMVVM.Core.Models
{
    /// <summary>
    /// Represents evaluated user Adobe licensing entitlements for Acrobat Pro and Creative Cloud.
    /// </summary>
    public record AdobeLicenseStatus(bool HasAcrobatPro, bool HasCreativeCloud);
}
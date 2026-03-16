namespace DSAMVVM.Core.Models
{
    // Represents the evaluation result of a user's Adobe licensing entitlements
    public record AdobeLicenseStatus(bool HasAcrobatPro, bool HasCreativeCloud);
}
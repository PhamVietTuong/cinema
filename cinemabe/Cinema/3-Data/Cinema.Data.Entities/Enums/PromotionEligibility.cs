namespace Cinema.Data.Enums;

/// <summary>Who qualifies for a PromotionProgram. Only Everyone and Members are safe to auto-apply at
/// checkout — the system has no way to verify StudentOrTeacher or AgeLimit online, so those stay
/// informational-only (AutoApply is rejected for them; see PromotionProgramManager validation).</summary>
public enum PromotionEligibility
{
    Everyone = 0,
    Members = 1,
    StudentOrTeacher = 2,
    AgeLimit = 3,
}

namespace Reporting.Application.Clients;

// ===== Client Brand Profile (Client 360 — CPW-R1B) — علاقة 1:0..1 =====
public record ClientBrandProfileDto(
    Guid ClientId,
    string? BusinessOverview,
    string? ProductsAndServices,
    string? TargetAudience,
    string? TargetLocations,
    string? UniqueSellingProposition,
    string? Strengths,
    string? Competitors,
    string? ToneOfVoice,
    string? PreferredMessages,
    string? ProhibitedMessages,
    string? BrandGuidelinesUrl,
    string? Notes,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc);

/// <summary>
/// طلب حفظ (Upsert) لملفّ البراند — سجلّ واحد لكلّ عميل. يُنشأ إن لم يكن موجودًا وإلّا يُحدَّث.
/// </summary>
public record UpsertClientBrandProfileRequest(
    string? BusinessOverview = null,
    string? ProductsAndServices = null,
    string? TargetAudience = null,
    string? TargetLocations = null,
    string? UniqueSellingProposition = null,
    string? Strengths = null,
    string? Competitors = null,
    string? ToneOfVoice = null,
    string? PreferredMessages = null,
    string? ProhibitedMessages = null,
    string? BrandGuidelinesUrl = null,
    string? Notes = null);

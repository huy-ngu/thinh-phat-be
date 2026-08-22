namespace ThinhPhat.Api.Shared.Services.Ghtk;

public sealed class GhtkOptions
{
    public const string SectionName = "Ghtk";

    public string BaseUrl { get; set; } = "https://services.giaohangtietkiem.vn";
    public string ApiToken { get; set; } = string.Empty;
    public string? PartnerCode { get; set; }
}

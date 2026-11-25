namespace FamilyOS.Maui.Services.Api;

public sealed class FamilyApiException : Exception
{
    public int StatusCode { get; }
    public string? Path { get; }

    public FamilyApiException(string message, int statusCode = 0, string? path = null, Exception? inner = null)
        : base(message, inner)
    {
        StatusCode = statusCode;
        Path = path;
    }
}

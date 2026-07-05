namespace Cinema.Business.DTO.Requests;

/// <summary>Result of an image upload: the absolute URL the file is served from.</summary>
public class UploadResultDTO
{
    public string Url { get; set; } = string.Empty;
}

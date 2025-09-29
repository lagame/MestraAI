namespace RPGSessionManager.Dtos;

public class MediaUploadRequest
{
    public IFormFile? FileName { get; set; } 
    public string ContentType { get; set; } = string.Empty;
    public string Base64Content { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
  public int SessionId { get; set; }
  public string? MediaType { get; set; }
}


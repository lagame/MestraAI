namespace RPGSessionManager.Dtos;

public class StopAudioRequest
{
  public int MediaId { get; set; }
  public int SessionId { get; set; }
    public string AudioUrl { get; set; } = string.Empty;
}


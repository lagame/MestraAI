namespace RPGSessionManager.Dtos;

public class PlayAudioRequest
{
    public int SessionId { get; set; }
    public string AudioUrl { get; set; } = string.Empty;
    public float Volume { get; set; } = 1.0f;
    public bool Loop { get; set; } = false;
    public int MediaId { get; set; }
}


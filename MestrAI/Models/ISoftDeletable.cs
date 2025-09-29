namespace RPGSessionManager.Models;

public interface ISoftDeletable
{
  public bool IsDeleted { get; set; }
  public DateTime? DeletedAt { get; set; }
  public string? DeletedByUserId { get; set; }
}

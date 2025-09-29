namespace RPGSessionManager.Dtos;

public class UpdateTokenDto
{
    public int TokenId { get; set; }
    public double X { get; set; }
    public double Y { get; set; }
    public double Scale { get; set; }
    public double Rotation { get; set; }
    public bool IsVisible { get; set; }
    public string? ImageUrl { get; set; }
}


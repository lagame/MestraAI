using System.Security.Claims;

namespace RPGSessionManager.Services;

public class UserResolverService : IUserResolverService
{
  private readonly IHttpContextAccessor _httpContextAccessor;

  public UserResolverService(IHttpContextAccessor httpContextAccessor)
  {
    _httpContextAccessor = httpContextAccessor;
  }

  public string? GetUserId()
  {
    return _httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier);
  }
}

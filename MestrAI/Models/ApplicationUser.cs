using Microsoft.AspNetCore.Identity;

namespace RPGSessionManager.Models;

public class ApplicationUser : IdentityUser
{
    public string DisplayName { get; set; } = string.Empty;
}


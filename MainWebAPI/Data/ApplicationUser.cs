
using Microsoft.AspNetCore.Identity;
using System.Collections.Generic;
namespace MainWebAPI.Data
{

    public class ApplicationUser : IdentityUser
    {
        public string Avatar { get; set; }
        public string Bio { get; set; }
        public UserProfile UserProfile { get; set; }
    }
}

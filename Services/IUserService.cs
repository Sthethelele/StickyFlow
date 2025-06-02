using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace StickyFlow.Services
{
    public interface IUserService
    {
        Task<User> GetUserByEmail(string email);
        Task<string> GenerateJwtToken(User user);
        Task<User> RegisterUser(string email, string password, string fullName, string timeZone);
    }

    public class UserService : IUserService
    {
        private readonly IConfiguration _configuration;
        private readonly List<User> _users = new List<User>(); // Replace with actual database

        public UserService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public async Task<User> GetUserByEmail(string email)
        {
            return await Task.FromResult(_users.FirstOrDefault(u => u.Email == email));
        }

        public async Task<string> GenerateJwtToken(User user)
        {
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: _configuration["Jwt:Issuer"],
                audience: _configuration["Jwt:Audience"],
                claims: new[] { new Claim(ClaimTypes.Name, user.Email) },
                expires: DateTime.Now.AddDays(1),
                signingCredentials: creds);

            return await Task.FromResult(new JwtSecurityTokenHandler().WriteToken(token));
        }

        public async Task<User> RegisterUser(string email, string password, string fullName, string timeZone)
        {
            var user = new User
            {
                Email = email,
                Password = password, // In production, hash this password
                FullName = fullName,
                TimeZone = timeZone
            };
            _users.Add(user); // Replace with database save
            return await Task.FromResult(user);
        }
    }

    public class User
    {
        public string Email { get; set; }
        public string Password { get; set; }
        public string FullName { get; set; }
        public string TimeZone { get; set; }
    }
}

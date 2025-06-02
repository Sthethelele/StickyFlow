using Google.Apis.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using StickyFlow.Data;
using StickyFlow.Dtos;
using StickyFlow.Models;
using StickyFlow.Services;
using System.ComponentModel.DataAnnotations;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using System.Web;

namespace StickyFlow.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IConfiguration _configuration;
        private readonly ApplicationDbContext _context;
        private readonly IEmailService _emailService;
        private readonly ILogger<AuthController> _logger;

        public AuthController(
            UserManager<ApplicationUser> userManager,
            IConfiguration configuration,
            ApplicationDbContext context,
            IEmailService emailService,
            ILogger<AuthController> logger)
        {
            _userManager = userManager;
            _configuration = configuration;
            _context = context;
            _emailService = emailService;
            _logger = logger;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] StickyFlowRegisterRequest request)
        {
            if (!IsValidEmail(request.Email))
                return BadRequest(new { message = "Invalid email format" });

            var userExists = await _userManager.FindByEmailAsync(request.Email);
            if (userExists != null)
                return Conflict(new { message = "User already exists" });

            var user = new ApplicationUser
            {
                FullName = request.FullName,
                Email = request.Email,
                UserName = request.Email,
                SecurityStamp = Guid.NewGuid().ToString()
            };

            var result = await _userManager.CreateAsync(user, request.Password);
            if (!result.Succeeded)
                return BadRequest(new { message = "Registration failed", errors = result.Errors });

            var profile = new UserProfile
            {
                UserId = user.Id,
                FullName = request.FullName,
                TimeZone = request.TimeZone ?? "UTC"
            };
            _context.UserProfiles.Add(profile);
            await _context.SaveChangesAsync();

            if (_configuration.GetValue<bool>("EmailConfirmationRequired"))
            {
                var emailToken = await _userManager.GenerateEmailConfirmationTokenAsync(user);
                await _emailService.SendConfirmationEmailAsync(user.Email, user.Id, emailToken);
                _logger.LogInformation("New user registered: {Email}. Email confirmation sent.", request.Email);
                return Ok(new { Status = "Success", Message = "User created. Please check your email for verification." });
            }

            _logger.LogInformation("New user registered: {Email}", request.Email);
            return Ok(new { Status = "Success", Message = "User created successfully." });
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] StickyFlowLoginRequest request)
        {
            var user = await _userManager.FindByEmailAsync(request.Email);
            if (user == null)
                return BadRequest(new { message = "Invalid email or password" });

            if (_configuration.GetValue<bool>("EmailConfirmationRequired") && !user.EmailConfirmed)
                return BadRequest(new { message = "Email not confirmed. Please check your email for the confirmation link." });

            var result = await _userManager.CheckPasswordAsync(user, request.Password);
            if (!result)
                return BadRequest(new { message = "Invalid email or password" });

            var token = GenerateJwtToken(user);
            _logger.LogInformation("User logged in: {Email}", request.Email);
            return Ok(new { token });
        }

        [HttpGet("confirm-email")]
        public async Task<IActionResult> ConfirmEmail(string userId, string token)
        {
            if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(token))
                return BadRequest(new { message = "Invalid email confirmation link" });

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
                return BadRequest(new { message = "User not found" });

            var result = await _userManager.ConfirmEmailAsync(user, token);
            if (!result.Succeeded)
                return BadRequest(new { message = "Email confirmation failed", errors = result.Errors });

            _logger.LogInformation("Email confirmed for user: {Email}", user.Email);
            return Ok(new { Status = "Success", Message = "Email confirmed successfully. You can now log in." });
        }

        [HttpPost("forgot-password")]
        public async Task<IActionResult> ForgotPassword([FromBody] StickyFlowForgotPasswordRequest request)
        {
            var user = await _userManager.FindByEmailAsync(request.Email);
            if (user == null)
                return Ok();

            var resetToken = await _userManager.GeneratePasswordResetTokenAsync(user);
            await _emailService.SendPasswordResetEmailAsync(user.Email, resetToken);

            _logger.LogInformation("Password reset requested for {Email}", request.Email);
            return Ok(new { Status = "Success", Message = "Password reset link sent" });
        }

        [HttpPost("reset-password")]
        public async Task<IActionResult> ResetPassword([FromBody] StickyFlowResetPasswordRequest request)
        {
            var user = await _userManager.FindByEmailAsync(request.Email);
            if (user == null)
                return BadRequest(new { message = "Invalid email" });

            var result = await _userManager.ResetPasswordAsync(user, request.Token, request.NewPassword);
            if (!result.Succeeded)
                return BadRequest(new { message = "Password reset failed", errors = result.Errors });

            _logger.LogInformation("Password reset successfully for {Email}", request.Email);
            return Ok(new { Status = "Success", Message = "Password reset successfully" });
        }

        [HttpPost("google-login")]
        public async Task<IActionResult> GoogleLogin([FromBody] GoogleLoginRequest request)
        {
            try
            {
                // Validate the Google ID token
                var settings = new GoogleJsonWebSignature.ValidationSettings
                {
                    Audience = new[] { _configuration["Google:204699764852-qneavf12bga9c8i1rtq2q3erhsto3f91.apps.googleusercontent.com"] } // Fetch from configuration
                };
                var payload = await GoogleJsonWebSignature.ValidateAsync(request.IdToken, settings);

                // Check if user exists in Identity
                var user = await _userManager.FindByEmailAsync(payload.Email);
                if (user != null)
                {
                    // User exists, generate a JWT token
                    if (_configuration.GetValue<bool>("EmailConfirmationRequired") && !user.EmailConfirmed)
                    {
                        return BadRequest(new { message = "Email not confirmed. Please check your email for the confirmation link." });
                    }

                    var token = GenerateJwtToken(user);
                    _logger.LogInformation("User logged in with Google: {Email}", payload.Email);
                    return Ok(new { token });
                }
                else
                {
                    // New user, return Google data for registration
                    return Ok(new
                    {
                        newUser = true,
                        googleData = new
                        {
                            email = payload.Email,
                            name = payload.Name
                        }
                    });
                }
            }
            catch (InvalidJwtException)
            {
                return BadRequest("Invalid Google token.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Google Sign-In failed");
                return BadRequest($"Google Sign-In failed: {ex.Message}");
            }
        }

        private bool IsValidEmail(string email)
        {
            try
            {
                var addr = new System.Net.Mail.MailAddress(email);
                return addr.Address == email;
            }
            catch
            {
                return false;
            }
        }

        private string GenerateJwtToken(ApplicationUser user)
        {
            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.Email),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new Claim(ClaimTypes.NameIdentifier, user.Id)
            };

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: _configuration["Jwt:ValidIssuer"],
                audience: _configuration["Jwt:ValidAudience"],
                claims: claims,
                expires: DateTime.Now.AddHours(Convert.ToDouble(_configuration["Jwt:ExpiryHours"])),
                signingCredentials: creds);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }

    public class GoogleLoginRequest
    {
        public string IdToken { get; set; }
    }
}
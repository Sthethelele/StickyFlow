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
            if (!ModelState.IsValid)
                return BadRequest(new { message = "Invalid request data", errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage) });

            var userExists = await _userManager.FindByEmailAsync(request.Email);
            if (userExists != null)
                return Conflict(new { message = "User already exists" });

            var user = new ApplicationUser
            {
                FullName = HttpUtility.HtmlEncode(request.FullName), // Sanitize FullName
                Email = request.Email,
                UserName = request.Email,
                SecurityStamp = Guid.NewGuid().ToString()
            };

            var result = await _userManager.CreateAsync(user, request.Password);
            if (!result.Succeeded)
                return BadRequest(new { message = "Registration failed", errors = result.Errors });

            // Validate TimeZone
            var timeZone = request.TimeZone ?? "UTC";
            if (!TimeZoneInfo.GetSystemTimeZones().Any(tz => tz.Id == timeZone))
                return BadRequest(new { message = "Invalid time zone" });

            var profile = new UserProfile
            {
                UserId = user.Id,
                FullName = HttpUtility.HtmlEncode(request.FullName), // Sanitize again for profile
                TimeZone = timeZone
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

            _logger.LogInformation("New user registered: {UserId}", user.Id);
            return Ok(new { Status = "Success", Message = "User created successfully." });
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] StickyFlowLoginRequest request)
        {
            if (!ModelState.IsValid)
        return BadRequest(new { message = "Invalid request data", errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage) });
            var user = await _userManager.FindByEmailAsync(request.Email);
            if (user == null)
                return BadRequest(new { message = "Invalid email or password" });

            if (_configuration.GetValue<bool>("EmailConfirmationRequired") && !user.EmailConfirmed)
                return BadRequest(new { message = "Email not confirmed. Please check your email for the confirmation link." });

            var result = await _userManager.CheckPasswordAsync(user, request.Password);
            if (!result)
                return BadRequest(new { message = "Invalid email or password" });

            var token = GenerateJwtToken(user);
            _logger.LogInformation("User logged in: {Email}", user.Id);
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
            {
                _logger.LogWarning("Failed email confirmation attempt for userId: {UserId}", userId);
                return BadRequest(new { message = "Email confirmation failed", errors = result.Errors });
            }

            _logger.LogInformation("Email confirmed for user: {Email}", user.Id);
            return Ok(new { Status = "Success", Message = "Email confirmed successfully. You can now log in." });
        }

        [HttpPost("forgot-password")]
        public async Task<IActionResult> ForgotPassword([FromBody] StickyFlowForgotPasswordRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(new { message = "Invalid request data", errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage) });

            var user = await _userManager.FindByEmailAsync(request.Email);
            if (user == null)
                return Ok();

            var resetToken = await _userManager.GeneratePasswordResetTokenAsync(user);
            await _emailService.SendPasswordResetEmailAsync(user.Email, resetToken);

            _logger.LogInformation("Password reset requested for {Email}", user.Id);
            return Ok(new { Status = "Success", Message = "Password reset link sent" });
        }

        [HttpPost("reset-password")]
        public async Task<IActionResult> ResetPassword([FromBody] StickyFlowResetPasswordRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(new { message = "Invalid request data", errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage) });
            var user = await _userManager.FindByEmailAsync(request.Email);
            if (user == null)
                return BadRequest(new { message = "Invalid email" });

            var result = await _userManager.ResetPasswordAsync(user, request.Token, request.NewPassword);
            if (!result.Succeeded)
                return BadRequest(new { message = "Password reset failed", errors = result.Errors });

            _logger.LogInformation("Password reset successfully for {Email}", user.Id);
            return Ok(new { Status = "Success", Message = "Password reset successfully" });
        }

        [HttpPost("google-login")]
        public async Task<IActionResult> GoogleLogin([FromBody] GoogleLoginRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(new { message = "Invalid request data", errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage) });

            try
            {
                // Validate the Google ID token
                var settings = new GoogleJsonWebSignature.ValidationSettings
                {
                    Audience = new[] { _configuration["Google:ClientId"] } // Fetch from configuration
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
                    _logger.LogInformation("User logged in with Google: {UserId}", user.Id);
                    return Ok(new { token });
                }
                else
                {
                    // New user, create account
                    user = new ApplicationUser
                    {
                        Email = payload.Email,
                        UserName = payload.Email,
                        FullName = HttpUtility.HtmlEncode(payload.Name), // Sanitize FullName
                        EmailConfirmed = true, // Google-verified emails can be trusted
                        SecurityStamp = Guid.NewGuid().ToString()
                    };
                    var result = await _userManager.CreateAsync(user);
                    if (!result.Succeeded)
                        return BadRequest(new { message = "Google user registration failed", errors = result.Errors });

                    var profile = new UserProfile
                    {
                        UserId = user.Id,
                        FullName = HttpUtility.HtmlEncode(payload.Name), // Sanitize FullName
                        TimeZone = "UTC" // Default or validate if configurable
                    };
                    _context.UserProfiles.Add(profile);
                    await _context.SaveChangesAsync();

                    var token = GenerateJwtToken(user);
                    _logger.LogInformation("New user registered and logged in with Google: {UserId}", user.Id);
                    return Ok(new { token });
                }
            }
            catch (InvalidJwtException)
            {
                _logger.LogWarning("Invalid Google ID token provided.");
                return BadRequest(new { message = "Invalid Google ID token." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during Google Sign-In for token request.");
                return StatusCode(500, new { message = "An unexpected error occurred during Google Sign-In." });
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
            var jwtKey = _configuration["Jwt:Key"];
            var issuer = _configuration["Jwt:ValidIssuer"];
            var audience = _configuration["Jwt:ValidAudience"];

            if (string.IsNullOrEmpty(jwtKey) || jwtKey.Length < 32)
            {
                _logger.LogError("JWT Key is missing or too short. Minimum 32 bytes required.");
                throw new InvalidOperationException("Invalid JWT Key configuration.");
            }

            if (string.IsNullOrEmpty(issuer) || string.IsNullOrEmpty(audience))
            {
                _logger.LogError("Missing JWT Issuer or Audience configuration.");
                throw new InvalidOperationException("JWT configuration is incomplete.");
            }

            var claims = new[]
            {
        new Claim(JwtRegisteredClaimNames.Sub, user.Email),
        new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        new Claim(ClaimTypes.NameIdentifier, user.Id)
    };

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            double expiryHours;
            var expiryHoursValue = _configuration["Jwt:ExpiryHours"];
            if (!double.TryParse(expiryHoursValue, out expiryHours))
            {
                expiryHours = 24; // Default to 24 hours if parsing fails
                _logger.LogWarning("Failed to parse Jwt:ExpiryHours from configuration. Defaulting to 24 hours.");
            }

            var token = new JwtSecurityToken(
                issuer: issuer,
                audience: audience,
                claims: claims,
                expires: DateTime.Now.AddHours(expiryHours),
                signingCredentials: creds);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }

    public class GoogleLoginRequest
    {
        public string IdToken { get; set; }
    }
}
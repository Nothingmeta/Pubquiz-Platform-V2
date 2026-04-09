using System.Security.Cryptography;
using Pubquiz_Platform.Data;
using Pubquiz_Platform.Data.Entities;

namespace Pubquiz_Platform_V2.Services
{
    public class RefreshTokenService : IRefreshTokenService
    {
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly ILogger<RefreshTokenService> _logger;

        public RefreshTokenService(ApplicationDbContext context, IConfiguration configuration, ILogger<RefreshTokenService> logger)
        {
            _context = context;
            _configuration = configuration;
            _logger = logger;
        }

        /// <summary>
        /// Generates a cryptographically secure random refresh token
        /// </summary>
        public string GenerateRefreshToken()
        {
            var randomNumber = new byte[32];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(randomNumber);
                return Convert.ToBase64String(randomNumber);
            }
        }

        /// <summary>
        /// Validates a refresh token and returns the associated user ID
        /// </summary>
        public (bool IsValid, int UserId) ValidateRefreshToken(string token)
        {
            try
            {
                var user = _context.Users.FirstOrDefault(u => u.RefreshToken == token);
                
                if (user == null)
                {
                    _logger.LogWarning("Refresh token not found in database");
                    return (false, 0);
                }

                if (user.RefreshTokenExpiryTime == null || user.RefreshTokenExpiryTime < DateTime.UtcNow)
                {
                    _logger.LogWarning($"Refresh token expired for user {user.UserId}");
                    return (false, 0);
                }

                return (true, user.UserId);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error validating refresh token: {ex.Message}");
                return (false, 0);
            }
        }

        /// <summary>
        /// Saves a refresh token to the database for a user
        /// </summary>
        public void SaveRefreshToken(int userId, string refreshToken, DateTime expiryTime)
        {
            var user = _context.Users.Find(userId);
            if (user != null)
            {
                user.RefreshToken = refreshToken;
                user.RefreshTokenExpiryTime = expiryTime;
                _context.SaveChanges();
                _logger.LogInformation($"Refresh token saved for user {userId}");
            }
        }

        /// <summary>
        /// Revokes a refresh token for a user (logout)
        /// </summary>
        public void RevokeRefreshToken(int userId)
        {
            var user = _context.Users.Find(userId);
            if (user != null)
            {
                user.RefreshToken = null;
                user.RefreshTokenExpiryTime = null;
                _context.SaveChanges();
                _logger.LogInformation($"Refresh token revoked for user {userId}");
            }
        }
    }
}
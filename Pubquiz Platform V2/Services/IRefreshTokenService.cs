using System.Security.Claims;

namespace Pubquiz_Platform_V2.Services
{
    public interface IRefreshTokenService
    {
        string GenerateRefreshToken();
        (bool IsValid, int UserId) ValidateRefreshToken(string token);
        void SaveRefreshToken(int userId, string refreshToken, DateTime expiryTime);
        void RevokeRefreshToken(int userId);
    }
}
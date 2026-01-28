namespace WebGame.Domain.Interfaces;

public interface IAuthorizationService
{
    (Guid PlayerId, string JwtToken) GenerateJwtToken(string username);
    bool IsTokenValidForAtLeastOneDay(string token);
}
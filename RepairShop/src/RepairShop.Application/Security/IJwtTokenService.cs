using RepairShop.Domain.Users;

namespace RepairShop.Application.Security;

public interface IJwtTokenService
{
    string CreateToken(AppUser user);
}

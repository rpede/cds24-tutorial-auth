using System.Security.Claims;

namespace Service.Draft;

public interface IDraftService
{
    Dto.DraftDetail GetById(long id);
    IEnumerable<Dto.Draft> List();
    Task<long> Create(ClaimsPrincipal principal, Dto.DraftFormData data);
    Task Update(ClaimsPrincipal principal, long id, Dto.DraftFormData data);
    Task Delete(long id);
}

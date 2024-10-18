using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Service;
using Service.Draft;
using Service.Draft.Dto;

namespace Api.Controllers;

[ApiController]
[Route("api/draft")]
[Authorize(Roles = $"{Role.Admin},{Role.Editor}")]
public class DraftController(IDraftService service) : ControllerBase
{
    private readonly IDraftService service = service;

    [HttpGet]
    [Route("")]
    public IEnumerable<Draft> List() => service.List();

    [HttpPost]
    [Route("")]
    [Authorize(Roles = Role.Editor)]
    public async Task<long> Create(DraftFormData data) =>
        await service.Create(HttpContext.User, data);

    [HttpGet]
    [Route("{id}")]
    public DraftDetail Get(long id) => service.GetById(id);

    [HttpPut]
    [Route("{id}")]
    [Authorize(Roles = Role.Editor)]
    public async Task Update(long id, DraftFormData data) =>
        await service.Update(HttpContext.User, id, data);

    [HttpDelete]
    [Route("{id}")]
    public async Task Delete(long id) => await service.Delete(id);
}

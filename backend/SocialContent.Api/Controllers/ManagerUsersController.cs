using Microsoft.AspNetCore.Mvc;
using SocialContent.Api.Dtos;
using SocialContent.Api.Services;

namespace SocialContent.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ManagerUsersController(IManagerUserService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<ManagerUserDto>>> GetAll(CancellationToken ct) =>
        Ok(await service.GetAllAsync(ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ManagerUserDto>> Get(Guid id, CancellationToken ct) =>
        Ok(await service.GetAsync(id, ct));

    [HttpPost]
    public async Task<ActionResult<ManagerUserDto>> Create(ManagerUserWriteDto dto, CancellationToken ct)
    {
        var created = await service.CreateAsync(dto, ct);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ManagerUserDto>> Update(Guid id, ManagerUserWriteDto dto, CancellationToken ct) =>
        Ok(await service.UpdateAsync(id, dto, ct));

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await service.DeleteAsync(id, ct);
        return NoContent();
    }
}

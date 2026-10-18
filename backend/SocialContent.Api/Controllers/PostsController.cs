using Microsoft.AspNetCore.Mvc;
using SocialContent.Api.Domain;
using SocialContent.Api.Dtos;
using SocialContent.Api.Services;

namespace SocialContent.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PostsController(IPostService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<PostDto>>> GetAll([FromQuery] FacebookPostType? type, CancellationToken ct) =>
        Ok(await service.GetAllAsync(type, ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<PostDto>> Get(Guid id, CancellationToken ct) =>
        Ok(await service.GetAsync(id, ct));

    [HttpPost]
    public async Task<ActionResult<PostDto>> Create(PostWriteDto dto, CancellationToken ct)
    {
        var created = await service.CreateAsync(dto, ct);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<PostDto>> Update(Guid id, PostWriteDto dto, CancellationToken ct) =>
        Ok(await service.UpdateAsync(id, dto, ct));

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await service.DeleteAsync(id, ct);
        return NoContent();
    }
}

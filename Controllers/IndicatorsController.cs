using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MetierService.Data;

[Route("api/indicators")]
[ApiController]
public class IndicatorsController : ControllerBase
{
    private readonly AppDbContext _context;
    public IndicatorsController(AppDbContext context) { _context = context; }

    [HttpGet]
    public async Task<IActionResult> GetIndicateurs()
    {
        return Ok(await _context.Indicateurs.ToListAsync());
    }
}

using Microsoft.AspNetCore.Mvc;
using VehicleDebts.Application.Abstractions;
using VehicleDebts.Application.DTOs;
using VehicleDebts.Application.UseCases.GetVehicleDebts;

namespace VehicleDebts.Api.Controllers;

[ApiController]
[Route("api/vehicle")]
public sealed class VehicleDebtsController(
    IQueryHandler<GetVehicleDebtsQuery, VehicleDebtsResponse> handler) : ControllerBase
{
    [HttpGet("{plate}/debts")]
    public async Task<IActionResult> GetDebts(string plate, CancellationToken ct)
    {
        var result = await handler.HandleAsync(new GetVehicleDebtsQuery(plate), ct);
        return Ok(result);
    }
}

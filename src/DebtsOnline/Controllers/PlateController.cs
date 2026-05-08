using System.Globalization;
using System.Xml.Linq;
using DebtsOnline.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace DebtsOnline.Controllers;

[ApiController]
[Route("api/plate")]
public class PlateController(IDebtRepository repository) : ControllerBase
{
    [HttpGet("{plateId}/debts")]
    public ContentResult GetDebts(string plateId)
    {
        var debts = repository.GetByPlate(plateId).ToList();

        var debtsElement = debts.Count > 0
            ? new XElement("debts", debts.Select(d =>
                new XElement("debt",
                    new XElement("category", d.Category.ToString()),
                    new XElement("value", d.Value.ToString("0.00", CultureInfo.InvariantCulture)),
                    new XElement("expiration", d.Expiration.ToString("yyyy-MM-dd"))
                )))
            : new XElement("debts");

        var xml = new XDocument(
            new XElement("response",
                new XElement("plate", plateId),
                debtsElement
            )
        );

        return Content(xml.ToString(), "application/xml");
    }
}
using GrowDiary.Web.Api.Contracts;
using GrowDiary.Web.Api.Mapping;
using GrowDiary.Web.Infrastructure;
using GrowDiary.Web.Models;
using GrowDiary.Web.Services.Knowledge;
using Microsoft.AspNetCore.Mvc;

namespace GrowDiary.Web.Api.Controllers;

[ApiController]
[Route("api/hardware-items")]
[Produces("application/json")]
public sealed class HardwareItemsApiController : ApiControllerBase
{
    private readonly GrowRepository _repository;
    private readonly KnowledgeBaseLoader _knowledgeBase;

    public HardwareItemsApiController(GrowRepository repository, KnowledgeBaseLoader knowledgeBase)
    {
        _repository = repository;
        _knowledgeBase = knowledgeBase;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<HardwareItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    public ActionResult<IReadOnlyList<HardwareItemDto>> List([FromQuery] int? tentId = null, [FromQuery] HardwareItemStatus? status = null)
    {
        if (tentId.HasValue && _repository.GetTent(tentId.Value) is null)
        {
            ModelState.AddModelError(nameof(tentId), $"Zelt mit Id {tentId.Value} existiert nicht.");
            return ValidationError();
        }

        if (status.HasValue && !Enum.IsDefined(status.Value))
        {
            ModelState.AddModelError(nameof(status), "Status ist ungueltig.");
            return ValidationError();
        }

        var items = tentId.HasValue
            ? _repository.GetHardwareItemsByTent(tentId.Value)
            : status.HasValue
                ? _repository.GetHardwareItemsByStatus(status.Value)
                : _repository.GetHardwareItems();

        return Ok(items.Select(item => item.ToDto()).ToList());
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(HardwareItemDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public ActionResult<HardwareItemDto> Detail(int id)
    {
        var item = _repository.GetHardwareItem(id);
        return item is null
            ? NotFoundError("hardware_item_not_found", $"HardwareItem mit Id {id} existiert nicht.")
            : Ok(item.ToDto());
    }

    [HttpPost]
    [ProducesResponseType(typeof(HardwareItemDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    public ActionResult<HardwareItemDto> Create([FromBody] CreateHardwareItemRequest request)
    {
        ApplyWearTemplateDefaults(request);
        Validate(request.Name, request.Category, request.Status, request.Criticality, request.TentId, request.SetupId, request.GrowId, request.TentSensorId, request.InstalledAtUtc, request.RetiredAtUtc);
        if (!ModelState.IsValid)
        {
            return ValidationError();
        }

        var item = _repository.CreateHardwareItem(request.ToModel());
        return CreatedAtAction(nameof(Detail), new { id = item.Id }, item.ToDto());
    }

    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(HardwareItemDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public ActionResult<HardwareItemDto> Update(int id, [FromBody] UpdateHardwareItemRequest request)
    {
        if (!ModelState.IsValid)
        {
            return ValidationError();
        }

        var item = _repository.GetHardwareItem(id);
        if (item is null)
        {
            return NotFoundError("hardware_item_not_found", $"HardwareItem mit Id {id} existiert nicht.");
        }

        Validate(request.Name, request.Category, request.Status, request.Criticality, request.TentId, request.SetupId, request.GrowId, request.TentSensorId, request.InstalledAtUtc, request.RetiredAtUtc);
        if (!ModelState.IsValid)
        {
            return ValidationError();
        }

        request.ApplyTo(item);
        _repository.UpdateHardwareItem(item);
        return Ok(_repository.GetHardwareItem(id)!.ToDto());
    }

    private void ApplyWearTemplateDefaults(CreateHardwareItemRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.WearTemplateId))
        {
            return;
        }

        var template = _knowledgeBase.WearTemplates.FirstOrDefault(template =>
            string.Equals(template.Id, request.WearTemplateId.Trim(), StringComparison.OrdinalIgnoreCase));
        if (template is null)
        {
            return;
        }

        request.WearTemplateId = template.Id;
        request.Name = string.IsNullOrWhiteSpace(request.Name) ? template.Name : request.Name;
        request.Category = string.IsNullOrWhiteSpace(request.Category) ? template.Category : request.Category;
        request.ExpectedLifespanDays ??= template.ExpectedLifespanDays;
        request.InspectionIntervalDays ??= template.InspectionIntervalDays;
    }

    private void Validate(
        string? name,
        string? category,
        HardwareItemStatus status,
        HardwareItemCriticality criticality,
        int? tentId,
        int? setupId,
        int? growId,
        int? tentSensorId,
        DateTime? installedAtUtc,
        DateTime? retiredAtUtc)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            ModelState.AddModelError(nameof(CreateHardwareItemRequest.Name), "Name darf nicht leer sein.");
        }

        if (string.IsNullOrWhiteSpace(category))
        {
            ModelState.AddModelError(nameof(CreateHardwareItemRequest.Category), "Category darf nicht leer sein.");
        }

        if (!Enum.IsDefined(status))
        {
            ModelState.AddModelError(nameof(CreateHardwareItemRequest.Status), "Status ist ungueltig.");
        }

        if (!Enum.IsDefined(criticality))
        {
            ModelState.AddModelError(nameof(CreateHardwareItemRequest.Criticality), "Criticality ist ungueltig.");
        }

        if (tentId.HasValue && _repository.GetTent(tentId.Value) is null)
        {
            ModelState.AddModelError(nameof(CreateHardwareItemRequest.TentId), $"Zelt mit Id {tentId.Value} existiert nicht.");
        }

        if (setupId.HasValue && _repository.GetSetup(setupId.Value) is null)
        {
            ModelState.AddModelError(nameof(CreateHardwareItemRequest.SetupId), $"Setup mit Id {setupId.Value} existiert nicht.");
        }

        if (growId.HasValue && _repository.GetGrow(growId.Value) is null)
        {
            ModelState.AddModelError(nameof(CreateHardwareItemRequest.GrowId), $"Grow mit Id {growId.Value} existiert nicht.");
        }

        if (tentSensorId.HasValue && _repository.GetTentSensor(tentSensorId.Value) is null)
        {
            ModelState.AddModelError(nameof(CreateHardwareItemRequest.TentSensorId), $"TentSensor mit Id {tentSensorId.Value} existiert nicht.");
        }

        if (installedAtUtc.HasValue &&
            retiredAtUtc.HasValue &&
            retiredAtUtc.Value.ToUniversalTime() < installedAtUtc.Value.ToUniversalTime())
        {
            ModelState.AddModelError(nameof(CreateHardwareItemRequest.RetiredAtUtc), "RetiredAtUtc darf nicht vor InstalledAtUtc liegen.");
        }
    }
}

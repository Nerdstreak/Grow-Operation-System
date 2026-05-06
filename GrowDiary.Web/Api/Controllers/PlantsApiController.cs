using GrowDiary.Web.Api.Contracts;
using GrowDiary.Web.Api.Mapping;
using GrowDiary.Web.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace GrowDiary.Web.Api.Controllers;

[ApiController]
[Route("api/plants")]
[Produces("application/json")]
public sealed class PlantsApiController : ApiControllerBase
{
    private readonly GrowRepository _repository;

    public PlantsApiController(GrowRepository repository)
    {
        _repository = repository;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<PlantInstanceDto>), StatusCodes.Status200OK)]
    public ActionResult<IReadOnlyList<PlantInstanceDto>> List([FromQuery] int? setupId = null, [FromQuery] int? growId = null)
    {
        var plants = setupId.HasValue
            ? _repository.GetPlantsBySetup(setupId.Value)
            : growId.HasValue
                ? _repository.GetPlantsByGrow(growId.Value)
                : _repository.GetPlants();

        return Ok(plants.Select(plant => plant.ToDto()).ToList());
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(PlantInstanceDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public ActionResult<PlantInstanceDto> Detail(int id)
    {
        var plant = _repository.GetPlant(id);
        return plant is null
            ? NotFoundError("plant_not_found", $"Pflanze mit Id {id} existiert nicht.")
            : Ok(plant.ToDto());
    }

    [HttpPost]
    [ProducesResponseType(typeof(PlantInstanceDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    public ActionResult<PlantInstanceDto> Create([FromBody] CreatePlantInstanceRequest request)
    {
        if (!ModelState.IsValid)
        {
            return ValidationError();
        }

        ValidatePlant(request.Label, null, request.ParentPlantId, request.StrainId, request.SetupId, request.GrowId, request.StartedAt, request.EndedAt);
        if (!ModelState.IsValid)
        {
            return ValidationError();
        }

        var plant = _repository.CreatePlant(request.ToModel());
        return CreatedAtAction(nameof(Detail), new { id = plant.Id }, plant.ToDto());
    }

    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(PlantInstanceDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public ActionResult<PlantInstanceDto> Update(int id, [FromBody] UpdatePlantInstanceRequest request)
    {
        if (!ModelState.IsValid)
        {
            return ValidationError();
        }

        var plant = _repository.GetPlant(id);
        if (plant is null)
        {
            return NotFoundError("plant_not_found", $"Pflanze mit Id {id} existiert nicht.");
        }

        ValidatePlant(request.Label, id, request.ParentPlantId, request.StrainId, request.SetupId, request.GrowId, request.StartedAt, request.EndedAt);
        if (!ModelState.IsValid)
        {
            return ValidationError();
        }

        request.ApplyTo(plant);
        _repository.UpdatePlant(plant);
        return Ok(_repository.GetPlant(id)!.ToDto());
    }

    private void ValidatePlant(string label, int? plantId, int? parentPlantId, int? strainId, int? setupId, int? growId, DateTime? startedAt, DateTime? endedAt)
    {
        if (string.IsNullOrWhiteSpace(label))
        {
            ModelState.AddModelError(nameof(CreatePlantInstanceRequest.Label), "Label darf nicht leer sein.");
        }

        if (plantId.HasValue && parentPlantId == plantId)
        {
            ModelState.AddModelError(nameof(UpdatePlantInstanceRequest.ParentPlantId), "ParentPlantId darf nicht auf dieselbe Pflanze zeigen.");
        }

        if (parentPlantId.HasValue && _repository.GetPlant(parentPlantId.Value) is null)
        {
            ModelState.AddModelError(nameof(CreatePlantInstanceRequest.ParentPlantId), $"ParentPlantId {parentPlantId.Value} existiert nicht.");
        }

        if (strainId.HasValue && _repository.GetStrain(strainId.Value) is null)
        {
            ModelState.AddModelError(nameof(CreatePlantInstanceRequest.StrainId), $"StrainId {strainId.Value} existiert nicht.");
        }

        if (setupId.HasValue && _repository.GetSetup(setupId.Value) is null)
        {
            ModelState.AddModelError(nameof(CreatePlantInstanceRequest.SetupId), $"SetupId {setupId.Value} existiert nicht.");
        }

        if (growId.HasValue && _repository.GetGrow(growId.Value) is null)
        {
            ModelState.AddModelError(nameof(CreatePlantInstanceRequest.GrowId), $"GrowId {growId.Value} existiert nicht.");
        }

        if (startedAt.HasValue && endedAt.HasValue && endedAt.Value < startedAt.Value)
        {
            ModelState.AddModelError(nameof(CreatePlantInstanceRequest.EndedAt), "EndedAt darf nicht vor StartedAt liegen.");
        }
    }
}

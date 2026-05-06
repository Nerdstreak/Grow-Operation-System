using GrowDiary.Web.Api.Contracts;
using GrowDiary.Web.Api.Mapping;
using GrowDiary.Web.Infrastructure;
using GrowDiary.Web.Models;
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

    [HttpPost("clone-from-mother")]
    [ProducesResponseType(typeof(PlantInstanceDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    public ActionResult<PlantInstanceDto> CloneFromMother([FromBody] CreateCloneFromMotherRequest request)
    {
        if (!ModelState.IsValid)
        {
            return ValidationError();
        }

        var mother = _repository.GetPlant(request.MotherPlantId);
        if (mother is null)
        {
            ModelState.AddModelError(nameof(CreateCloneFromMotherRequest.MotherPlantId), $"Mutterpflanze mit Id {request.MotherPlantId} existiert nicht.");
        }
        else if (mother.PlantRole != PlantRole.Mother)
        {
            ModelState.AddModelError(nameof(CreateCloneFromMotherRequest.MotherPlantId), "Nur Pflanzen mit PlantRole Mother koennen als Clone-Quelle genutzt werden.");
        }

        if (string.IsNullOrWhiteSpace(request.Label))
        {
            ModelState.AddModelError(nameof(CreateCloneFromMotherRequest.Label), "Label darf nicht leer sein.");
        }

        if (request.TargetSetupId.HasValue)
        {
            var targetSetup = _repository.GetSetup(request.TargetSetupId.Value);
            if (targetSetup is null)
            {
                ModelState.AddModelError(nameof(CreateCloneFromMotherRequest.TargetSetupId), $"Ziel-Setup mit Id {request.TargetSetupId.Value} existiert nicht.");
            }
            else if (targetSetup.SetupType != SetupType.Quarantine)
            {
                ModelState.AddModelError(nameof(CreateCloneFromMotherRequest.TargetSetupId), "Clone-Ziel muss ein Quarantine-Setup sein.");
            }
        }

        if (request.StrainId.HasValue && _repository.GetStrain(request.StrainId.Value) is null)
        {
            ModelState.AddModelError(nameof(CreateCloneFromMotherRequest.StrainId), $"StrainId {request.StrainId.Value} existiert nicht.");
        }

        if (!ModelState.IsValid || mother is null)
        {
            return ValidationError();
        }

        var cutAt = request.CutAt ?? DateTime.Now;
        var clone = new PlantInstance
        {
            StrainId = request.StrainId ?? mother.StrainId,
            SetupId = request.TargetSetupId,
            GrowId = null,
            ParentPlantId = mother.Id,
            Label = request.Label.Trim(),
            PlantRole = PlantRole.Clone,
            PlantStatus = PlantStatus.Active,
            PhenoLabel = Normalize(request.PhenoLabel),
            StartedAt = cutAt,
            Notes = Normalize(request.Notes)
        };

        var created = _repository.CreateCloneFromMother(clone, mother.SetupId, cutAt);
        return CreatedAtAction(nameof(Detail), new { id = created.Id }, created.ToDto());
    }

    [HttpPost("decide-quarantine")]
    [ProducesResponseType(typeof(PlantInstanceDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    public ActionResult<PlantInstanceDto> DecideQuarantine([FromBody] DecideQuarantinePlantRequest request)
    {
        if (!ModelState.IsValid)
        {
            return ValidationError();
        }

        var plant = _repository.GetPlant(request.PlantId);
        Setup? quarantineSetup = null;
        if (plant is null)
        {
            ModelState.AddModelError(nameof(DecideQuarantinePlantRequest.PlantId), $"Pflanze mit Id {request.PlantId} existiert nicht.");
        }
        else if (!plant.SetupId.HasValue)
        {
            ModelState.AddModelError(nameof(DecideQuarantinePlantRequest.PlantId), "Pflanze ist keinem Quarantine-Setup zugeordnet.");
        }
        else
        {
            quarantineSetup = _repository.GetSetup(plant.SetupId.Value);
            if (quarantineSetup is null || quarantineSetup.SetupType != SetupType.Quarantine)
            {
                ModelState.AddModelError(nameof(DecideQuarantinePlantRequest.PlantId), "Nur Plants aus einem Quarantine-Setup koennen entschieden werden.");
            }
        }

        var isCleared = string.Equals(request.Decision, "Cleared", StringComparison.Ordinal);
        var isRejected = string.Equals(request.Decision, "Rejected", StringComparison.Ordinal);
        if (!isCleared && !isRejected)
        {
            ModelState.AddModelError(nameof(DecideQuarantinePlantRequest.Decision), "Decision muss Cleared oder Rejected sein.");
        }

        Setup? targetSetup = null;
        GrowRun? targetGrow = null;
        if (isCleared)
        {
            if (request.TargetSetupId.HasValue)
            {
                targetSetup = _repository.GetSetup(request.TargetSetupId.Value);
                if (targetSetup is null)
                {
                    ModelState.AddModelError(nameof(DecideQuarantinePlantRequest.TargetSetupId), $"Ziel-Setup mit Id {request.TargetSetupId.Value} existiert nicht.");
                }
                else if (targetSetup.SetupType != SetupType.Production)
                {
                    ModelState.AddModelError(nameof(DecideQuarantinePlantRequest.TargetSetupId), "Freigabe-Ziel muss ein Production-Setup sein.");
                }
            }

            if (request.TargetGrowId.HasValue)
            {
                targetGrow = _repository.GetGrow(request.TargetGrowId.Value);
                if (targetGrow is null)
                {
                    ModelState.AddModelError(nameof(DecideQuarantinePlantRequest.TargetGrowId), $"Grow mit Id {request.TargetGrowId.Value} existiert nicht.");
                }
            }

            if (targetSetup is not null && targetGrow is not null)
            {
                if (targetGrow.SetupId.HasValue && targetGrow.SetupId.Value != targetSetup.Id)
                {
                    ModelState.AddModelError(nameof(DecideQuarantinePlantRequest.TargetGrowId), "Grow passt nicht zum gewaehlten Production-Setup.");
                }

                if (targetGrow.TentId.HasValue && targetGrow.TentId.Value != targetSetup.TentId)
                {
                    ModelState.AddModelError(nameof(DecideQuarantinePlantRequest.TargetGrowId), "Grow und Production-Setup liegen in unterschiedlichen Zelten.");
                }
            }
        }

        if (isRejected)
        {
            if (request.TargetSetupId.HasValue)
            {
                ModelState.AddModelError(nameof(DecideQuarantinePlantRequest.TargetSetupId), "Rejected darf kein Ziel-Setup enthalten.");
            }

            if (request.TargetGrowId.HasValue)
            {
                ModelState.AddModelError(nameof(DecideQuarantinePlantRequest.TargetGrowId), "Rejected darf keinen Ziel-Grow enthalten.");
            }
        }

        if (!ModelState.IsValid || plant is null || quarantineSetup is null)
        {
            return ValidationError();
        }

        var decidedAt = request.DecidedAt ?? DateTime.Now;
        plant.Notes = Normalize(request.Notes);
        if (isCleared)
        {
            plant.SetupId = request.TargetSetupId ?? plant.SetupId;
            plant.GrowId = request.TargetGrowId ?? plant.GrowId;
            if (request.TargetSetupId.HasValue || request.TargetGrowId.HasValue)
            {
                plant.PlantRole = PlantRole.Production;
            }
            plant.PlantStatus = PlantStatus.Active;
            plant.EndedAt = null;
        }
        else
        {
            plant.PlantStatus = PlantStatus.Culled;
            plant.EndedAt = decidedAt;
        }

        var updated = _repository.DecideQuarantinePlant(plant, quarantineSetup.Id, request.Decision);
        return Ok(updated.ToDto());
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

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

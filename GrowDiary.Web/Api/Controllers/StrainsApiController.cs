using GrowDiary.Web.Api.Contracts;
using GrowDiary.Web.Api.Mapping;
using GrowDiary.Web.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace GrowDiary.Web.Api.Controllers;

[ApiController]
[Route("api/strains")]
[Produces("application/json")]
public sealed class StrainsApiController : ApiControllerBase
{
    private readonly GrowRepository _repository;

    public StrainsApiController(GrowRepository repository)
    {
        _repository = repository;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<StrainDto>), StatusCodes.Status200OK)]
    public ActionResult<IReadOnlyList<StrainDto>> List()
        => Ok(_repository.GetStrains().Select(strain => strain.ToDto()).ToList());

    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(StrainDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public ActionResult<StrainDto> Detail(int id)
    {
        var strain = _repository.GetStrain(id);
        return strain is null
            ? NotFoundError("strain_not_found", $"Strain mit Id {id} existiert nicht.")
            : Ok(strain.ToDto());
    }

    [HttpPost]
    [ProducesResponseType(typeof(StrainDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    public ActionResult<StrainDto> Create([FromBody] CreateStrainRequest request)
    {
        if (!ModelState.IsValid)
        {
            return ValidationError();
        }

        ValidateStrain(request.Name, request.FlowerWeeksMin, request.FlowerWeeksMax, request.NutrientDemandFactor, request.StretchFactor, request.VpdPreferenceShift);
        if (!ModelState.IsValid)
        {
            return ValidationError();
        }

        var strain = _repository.CreateStrain(request.ToModel());
        return CreatedAtAction(nameof(Detail), new { id = strain.Id }, strain.ToDto());
    }

    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(StrainDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public ActionResult<StrainDto> Update(int id, [FromBody] UpdateStrainRequest request)
    {
        if (!ModelState.IsValid)
        {
            return ValidationError();
        }

        var strain = _repository.GetStrain(id);
        if (strain is null)
        {
            return NotFoundError("strain_not_found", $"Strain mit Id {id} existiert nicht.");
        }

        ValidateStrain(request.Name, request.FlowerWeeksMin, request.FlowerWeeksMax, request.NutrientDemandFactor, request.StretchFactor, request.VpdPreferenceShift);
        if (!ModelState.IsValid)
        {
            return ValidationError();
        }

        request.ApplyTo(strain);
        _repository.UpdateStrain(strain);
        return Ok(_repository.GetStrain(id)!.ToDto());
    }

    private void ValidateStrain(string name, int? flowerWeeksMin, int? flowerWeeksMax, params double?[] factors)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            ModelState.AddModelError(nameof(CreateStrainRequest.Name), "Name darf nicht leer sein.");
        }

        if (flowerWeeksMin.HasValue && flowerWeeksMax.HasValue && flowerWeeksMin.Value > flowerWeeksMax.Value)
        {
            ModelState.AddModelError(nameof(CreateStrainRequest.FlowerWeeksMin), "FlowerWeeksMin darf nicht groesser als FlowerWeeksMax sein.");
        }

        if (factors.Any(factor => factor.HasValue && factor.Value <= 0))
        {
            ModelState.AddModelError("Factors", "Genetik-Faktoren muessen groesser als 0 sein.");
        }
    }
}

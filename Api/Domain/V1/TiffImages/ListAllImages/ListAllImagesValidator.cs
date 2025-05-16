using Api.Shared.ErrorHandling;
using FastEndpoints;
using FluentValidation;

namespace Api.Domain.V1.TiffImages.ListAllImages;

public class ListAllImagesValidator : Validator<ListAllImagesRequestDto>
{
    public ListAllImagesValidator()
    {
        ClassLevelCascadeMode = CascadeMode.Stop;
        RuleLevelCascadeMode = CascadeMode.Stop;
        
        RuleFor(x => x.SessionName)
            .NotEmpty()
            .WithErrorCode(ErrorResponseProvider.InvalidSessionName.Code);

        RuleFor(x => x.FileUrl)
            .NotEmpty()
            .WithErrorCode(ErrorResponseProvider.InvalidFileUrl.Code);
    }
}
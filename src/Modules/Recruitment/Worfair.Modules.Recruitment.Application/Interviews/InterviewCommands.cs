namespace Worfair.Modules.Recruitment.Application.Interviews;

using FluentValidation;
using Worfair.BuildingBlocks.Application.Cqrs;
using Worfair.Modules.Recruitment.Application.Dtos;

public sealed record ScheduleInterviewCommand(
    Guid JobRequisitionId,
    Guid CandidateId,
    DateTime ScheduledAtUtc,
    int DurationMinutes,
    int Type) : ICommand<Result<InterviewDto>>;

public sealed class ScheduleInterviewCommandValidator : AbstractValidator<ScheduleInterviewCommand>
{
    public ScheduleInterviewCommandValidator()
    {
        RuleFor(c => c.CandidateId)
            .NotEmpty().WithErrorCode("Interview.CandidateRequired");

        RuleFor(c => c.DurationMinutes)
            .InclusiveBetween(15, 480).WithErrorCode("Interview.InvalidDuration");

        RuleFor(c => c.Type)
            .InclusiveBetween(1, 4).WithErrorCode("Interview.InvalidStatusTransition");
    }
}

public sealed record AddInterviewFeedbackCommand(Guid InterviewId, int Rating, string Notes) : ICommand;

public sealed class AddInterviewFeedbackCommandValidator : AbstractValidator<AddInterviewFeedbackCommand>
{
    public AddInterviewFeedbackCommandValidator()
    {
        RuleFor(c => c.Rating)
            .InclusiveBetween(1, 5).WithErrorCode("Interview.FeedbackRatingInvalid");

        RuleFor(c => c.Notes)
            .NotEmpty().WithErrorCode("Interview.FeedbackNotesRequired");
    }
}

public sealed record CompleteInterviewCommand(Guid InterviewId) : ICommand;

public sealed record CancelInterviewCommand(Guid InterviewId) : ICommand;

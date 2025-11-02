namespace Safe.EntityFramework;

using FluentValidation;
using Safe.Domain.Entities;
using static Safe.Domain.Commands.SafeCommand;

public class CreateChangeCommandValidator : AbstractValidator<CreateChangeCommand>
{
    public CreateChangeCommandValidator()
    {
        RuleFor(x => x.Amount).GreaterThan(0);
        RuleFor(x => x.Category).NotEmpty().MaximumLength(64);
        RuleFor(x => x.Comment).NotEmpty().MaximumLength(512);
        RuleFor(x => x.Reason).IsInEnum();
        RuleFor(x => x.Direction)
            .Must((cmd, dir) =>
            {
                if (cmd.Reason == SafeChangeReason.Surplus) return dir is null or SafeChangeDirection.Credit;
                if (cmd.Reason == SafeChangeReason.Shortage) return dir is null or SafeChangeDirection.Debit;
                return true;
            })
            .WithMessage("Direction должен соответствовать Reason (Surplus=Credit, Shortage=Debit).");
    }
}

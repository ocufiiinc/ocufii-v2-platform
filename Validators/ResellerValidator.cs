using FluentValidation;
using OcufiiAPI.DTO;

namespace OcufiiAPI.Validators
{
    public class ResellerValidator : AbstractValidator<CreateResellerDto>
    {
        public ResellerValidator()
        {
            RuleFor(x => x.Email)
                .NotEmpty().WithMessage("Email is required")
                .EmailAddress().WithMessage("Invalid email format");

            RuleFor(x => x.Name)
                .NotEmpty().WithMessage("First name is required");

            RuleFor(x => x.ContactName)
                .NotEmpty().WithMessage("Last name is required");

            RuleFor(x => x.PhoneNumber)
                .NotEmpty().WithMessage("Phone number is required")
                .Matches(@"^\+?[1-9]\d{1,14}$").WithMessage("Invalid phone number format (E.164 recommended)");
        }
    }
}

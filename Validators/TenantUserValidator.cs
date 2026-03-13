using FluentValidation;
using OcufiiAPI.DTO;

namespace OcufiiAPI.Validators
{
    public class TenantUserValidator : AbstractValidator<CreateTenantDto>
    {
        public TenantUserValidator()
        {
            RuleFor(x => x.OwnerEmail)
                .NotEmpty().WithMessage("Email is required")
                .EmailAddress().WithMessage("Invalid email format");

            RuleFor(x => x.OwnerFirstName)
                .NotEmpty().WithMessage("First name is required");

            RuleFor(x => x.OwnerLastName)
                .NotEmpty().WithMessage("Last name is required");

            RuleFor(x => x.PhoneNumber)
                .NotEmpty().WithMessage("Phone number is required")
                .Matches(@"^\+?[1-9]\d{1,14}$").WithMessage("Invalid phone number format (E.164 recommended)");
        }
    }
}

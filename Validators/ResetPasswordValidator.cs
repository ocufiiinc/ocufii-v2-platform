using FluentValidation;
using OcufiiAPI.DTO;

namespace OcufiiAPI.Validators
{
    public class ResetPasswordValidator : AbstractValidator<ResetPasswordDto>
    {
        public ResetPasswordValidator()
        {
            RuleFor(x => x.Email).NotEmpty().EmailAddress();
            RuleFor(x => x.OTP).NotEmpty().Length(6);
            RuleFor(x => x.NewPassword).NotEmpty().MinimumLength(8);
        }
    }
}

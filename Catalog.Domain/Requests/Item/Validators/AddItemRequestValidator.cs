using FluentValidation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Catalog.Domain.Requests.Item.Validators
{
    public class AddItemRequestValidator : AbstractValidator<AddItemRequest>
    {

        public AddItemRequestValidator()
        {

            RuleFor(x => x.Name).NotEmpty().NotNull();

            RuleFor(x => x.Description).NotEmpty().NotNull();

        }
    }
}

using System;
using FluentValidation;
using McpServer.Domain.Models;

namespace McpServer.Application.Validators;

/// <summary>
/// Validates {{MODEL_NAME}} instances
/// </summary>
public class {{MODEL_NAME}}Validator : AbstractValidator<{{MODEL_NAME}}>
{
    public {{MODEL_NAME}}Validator()
    {
        // Basic property validations
        RuleFor(x => x.{{PROPERTY_NAME}})
            .NotEmpty()
            .WithMessage("{{PROPERTY_NAME}} is required")
            .MaximumLength({{MAX_LENGTH}})
            .WithMessage("{{PROPERTY_NAME}} must not exceed {{MAX_LENGTH}} characters");

        // Conditional validations
        When(x => x.{{CONDITIONAL_PROPERTY}} != null, () =>
        {
            RuleFor(x => x.{{DEPENDENT_PROPERTY}})
                .NotEmpty()
                .WithMessage("{{DEPENDENT_PROPERTY}} is required when {{CONDITIONAL_PROPERTY}} is provided");
        });

        // Complex validations
        RuleFor(x => x.{{COMPLEX_PROPERTY}})
            .Must(BeValidComplexProperty)
            .WithMessage("{{COMPLEX_PROPERTY}} must meet specific criteria")
            .When(x => x.{{COMPLEX_PROPERTY}} != null);

        // Collection validations
        RuleForEach(x => x.{{COLLECTION_PROPERTY}})
            .SetValidator(new {{ITEM_TYPE}}Validator())
            .When(x => x.{{COLLECTION_PROPERTY}} != null);

        // Custom async validation
        RuleFor(x => x.{{ASYNC_PROPERTY}})
            .MustAsync(BeUniqueAsync)
            .WithMessage("{{ASYNC_PROPERTY}} must be unique");

        // Business rule validations
        RuleFor(x => x)
            .Must(SatisfyBusinessRule)
            .WithMessage("{{MODEL_NAME}} must satisfy business rules")
            .WithErrorCode("BUSINESS_RULE_VIOLATION");
    }

    private bool BeValidComplexProperty({{COMPLEX_TYPE}}? property)
    {
        if (property == null) return true;

        // TODO: Implement complex validation logic
        {{COMPLEX_VALIDATION_LOGIC}}

        return true;
    }

    private async Task<bool> BeUniqueAsync(
        {{MODEL_NAME}} model, 
        string propertyValue, 
        CancellationToken cancellationToken)
    {
        // TODO: Implement uniqueness check
        {{UNIQUENESS_CHECK_LOGIC}}
        
        await Task.Delay(10, cancellationToken); // Simulate async check
        return true;
    }

    private bool SatisfyBusinessRule({{MODEL_NAME}} model)
    {
        // TODO: Implement business rule validation
        {{BUSINESS_RULE_LOGIC}}

        return true;
    }
}

// Nested validator for complex properties
public class {{ITEM_TYPE}}Validator : AbstractValidator<{{ITEM_TYPE}}>
{
    public {{ITEM_TYPE}}Validator()
    {
        RuleFor(x => x.{{ITEM_PROPERTY}})
            .NotEmpty()
            .WithMessage("{{ITEM_PROPERTY}} is required");
    }
}

// Validator extensions for dependency injection
public static class {{MODEL_NAME}}ValidatorExtensions
{
    public static IServiceCollection Add{{MODEL_NAME}}Validation(this IServiceCollection services)
    {
        services.AddScoped<IValidator<{{MODEL_NAME}}>, {{MODEL_NAME}}Validator>();
        return services;
    }
}

// Custom validation rules
public static class {{MODEL_NAME}}ValidationRules
{
    public static IRuleBuilderOptions<T, string> Must{{CUSTOM_RULE}}<T>(
        this IRuleBuilder<T, string> ruleBuilder)
    {
        return ruleBuilder
            .Must(value => {{CUSTOM_RULE_LOGIC}})
            .WithMessage("Value must {{CUSTOM_RULE_DESCRIPTION}}");
    }
}

// Unit test template
#if TEST_TEMPLATE
using System.Threading.Tasks;
using FluentValidation.TestHelper;
using NUnit.Framework;

namespace McpServer.Application.Tests.Validators;

[TestFixture]
public class {{MODEL_NAME}}ValidatorTests
{
    private {{MODEL_NAME}}Validator _validator;

    [SetUp]
    public void Setup()
    {
        _validator = new {{MODEL_NAME}}Validator();
    }

    [Test]
    public async Task Validate_ValidModel_PassesValidation()
    {
        // Arrange
        var model = new {{MODEL_NAME}}
        {
            {{PROPERTY_NAME}} = "Valid value",
            // Set other required properties
        };

        // Act
        var result = await _validator.TestValidateAsync(model);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Test]
    public async Task Validate_Empty{{PROPERTY_NAME}}_FailsValidation()
    {
        // Arrange
        var model = new {{MODEL_NAME}}
        {
            {{PROPERTY_NAME}} = string.Empty,
        };

        // Act
        var result = await _validator.TestValidateAsync(model);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.{{PROPERTY_NAME}})
            .WithErrorMessage("{{PROPERTY_NAME}} is required");
    }

    [Test]
    public async Task Validate_{{PROPERTY_NAME}}ExceedsMaxLength_FailsValidation()
    {
        // Arrange
        var model = new {{MODEL_NAME}}
        {
            {{PROPERTY_NAME}} = new string('x', {{MAX_LENGTH}} + 1),
        };

        // Act
        var result = await _validator.TestValidateAsync(model);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.{{PROPERTY_NAME}})
            .WithErrorMessage("{{PROPERTY_NAME}} must not exceed {{MAX_LENGTH}} characters");
    }

    [Test]
    public async Task Validate_ConditionalValidation_WorksCorrectly()
    {
        // Arrange
        var model = new {{MODEL_NAME}}
        {
            {{CONDITIONAL_PROPERTY}} = "Some value",
            {{DEPENDENT_PROPERTY}} = null, // Should fail when conditional property is set
        };

        // Act
        var result = await _validator.TestValidateAsync(model);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.{{DEPENDENT_PROPERTY}});
    }

    [TestCase("valid_value", true)]
    [TestCase("invalid_value", false)]
    public async Task Validate_ComplexProperty_ValidatesCorrectly(string value, bool shouldPass)
    {
        // Arrange
        var model = new {{MODEL_NAME}}
        {
            {{COMPLEX_PROPERTY}} = new {{COMPLEX_TYPE}} { Value = value },
        };

        // Act
        var result = await _validator.TestValidateAsync(model);

        // Assert
        if (shouldPass)
            result.ShouldNotHaveValidationErrorFor(x => x.{{COMPLEX_PROPERTY}});
        else
            result.ShouldHaveValidationErrorFor(x => x.{{COMPLEX_PROPERTY}});
    }
}
#endif
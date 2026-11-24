using System.ComponentModel.DataAnnotations;
using System.Reflection;
using Xunit;

namespace KiaraFin.Api.Tests;

public sealed class ContractValidationTests
{
    /// <summary>
    /// O ASP.NET Core lança InvalidOperationException (HTTP 500) quando um record posicional tem
    /// atributos de validação na propriedade (ex.: [property: StringLength]) em vez do parâmetro.
    /// </summary>
    [Fact]
    public void Request_records_declare_validation_on_constructor_parameters_only()
    {
        var offenders = typeof(Program).Assembly.GetTypes()
            .Where(type => type.Name.EndsWith("Request", StringComparison.Ordinal))
            .SelectMany(type => type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(property => property.GetCustomAttributes<ValidationAttribute>(inherit: true).Any())
                .Select(property => $"{type.Name}.{property.Name}"))
            .ToList();

        Assert.Empty(offenders);
    }
}

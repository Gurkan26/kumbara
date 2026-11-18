using System.Reflection;
using NetArchTest.Rules;
using Xunit;

namespace Kumbara.Architecture.Tests;

public class ArchitectureTests
{
    private static readonly string[] DomainProjects = new[]
    {
        "Kumbara.Identity.Domain",
        "Kumbara.Wallet.Domain",
        "Kumbara.Ledger.Domain",
        "Kumbara.Transfer.Domain",
        "Kumbara.Risk.Domain"
    };

    private static readonly string[] ApplicationProjects = new[]
    {
        "Kumbara.Identity.Application",
        "Kumbara.Wallet.Application",
        "Kumbara.Ledger.Application",
        "Kumbara.Transfer.Application",
        "Kumbara.Risk.Application"
    };

    private static readonly string[] InfrastructureProjects = new[]
    {
        "Kumbara.Identity.Infrastructure",
        "Kumbara.Wallet.Infrastructure",
        "Kumbara.Ledger.Infrastructure",
        "Kumbara.Transfer.Infrastructure",
        "Kumbara.Risk.Infrastructure"
    };

    private static readonly string[] ApiProjects = new[]
    {
        "Kumbara.Identity.Api",
        "Kumbara.Wallet.Api",
        "Kumbara.Ledger.Api",
        "Kumbara.Transfer.Api",
        "Kumbara.Risk.Api",
        "Kumbara.Gateway"
    };

    private static Assembly[] GetDomainAssemblies() => new[]
    {
        Assembly.Load("Kumbara.Identity.Domain"),
        Assembly.Load("Kumbara.Wallet.Domain"),
        Assembly.Load("Kumbara.Ledger.Domain"),
        Assembly.Load("Kumbara.Transfer.Domain"),
        Assembly.Load("Kumbara.Risk.Domain")
    };

    private static Assembly[] GetApplicationAssemblies() => new[]
    {
        Assembly.Load("Kumbara.Identity.Application"),
        Assembly.Load("Kumbara.Wallet.Application"),
        Assembly.Load("Kumbara.Ledger.Application"),
        Assembly.Load("Kumbara.Transfer.Application"),
        Assembly.Load("Kumbara.Risk.Application")
    };

    private static Assembly[] GetApiAssemblies() => new[]
    {
        Assembly.Load("Kumbara.Identity.Api"),
        Assembly.Load("Kumbara.Wallet.Api"),
        Assembly.Load("Kumbara.Ledger.Api"),
        Assembly.Load("Kumbara.Transfer.Api"),
        Assembly.Load("Kumbara.Risk.Api")
    };

    [Fact]
    public void Domain_Should_Not_HaveDependencyOn_OtherProjects()
    {
        // Domain projects in Identity, Wallet, Ledger, Transfer, Risk must not reference Application, Infrastructure, or Api
        var domainAssemblies = GetDomainAssemblies();

        var forbiddenDependencies = ApplicationProjects
            .Concat(InfrastructureProjects)
            .Concat(ApiProjects)
            .ToArray();

        var result = Types.InAssemblies(domainAssemblies)
            .ShouldNot()
            .HaveDependencyOnAny(forbiddenDependencies)
            .GetResult();

        Assert.True(
            result.IsSuccessful,
            $"Domain layer must not depend on Application, Infrastructure, or Api projects. Violating types: {string.Join(", ", result.FailingTypeNames ?? Enumerable.Empty<string>())}");
    }

    [Fact]
    public void Application_Should_Not_HaveDependencyOn_InfrastructureOrApi()
    {
        // Application projects in Identity, Wallet, Ledger, Transfer, Risk must not reference Infrastructure or Api
        var applicationAssemblies = GetApplicationAssemblies();

        var forbiddenDependencies = InfrastructureProjects
            .Concat(ApiProjects)
            .ToArray();

        var result = Types.InAssemblies(applicationAssemblies)
            .ShouldNot()
            .HaveDependencyOnAny(forbiddenDependencies)
            .GetResult();

        Assert.True(
            result.IsSuccessful,
            $"Application layer must not depend on Infrastructure or Api projects. Violating types: {string.Join(", ", result.FailingTypeNames ?? Enumerable.Empty<string>())}");
    }

    [Fact]
    public void Controllers_Should_Not_Directly_Reference_DbContext()
    {
        // Controllers in Api projects must not directly reference DbContext or EntityFrameworkCore
        var apiAssemblies = GetApiAssemblies();

        var forbiddenDependencies = new[]
        {
            "Microsoft.EntityFrameworkCore.DbContext",
            "Microsoft.EntityFrameworkCore"
        }.Concat(InfrastructureProjects).ToArray();

        var result = Types.InAssemblies(apiAssemblies)
            .That()
            .HaveNameEndingWith("Controller")
            .ShouldNot()
            .HaveDependencyOnAny(forbiddenDependencies)
            .GetResult();

        Assert.True(
            result.IsSuccessful,
            $"Controllers must not directly reference DbContext or Infrastructure persistence. Violating controllers: {string.Join(", ", result.FailingTypeNames ?? Enumerable.Empty<string>())}");

        // Additional reflection verification on Controller constructors
        foreach (var assembly in apiAssemblies)
        {
            var controllerTypes = assembly.GetTypes()
                .Where(t => t.IsClass && !t.IsAbstract && t.Name.EndsWith("Controller"));

            foreach (var controller in controllerTypes)
            {
                foreach (var constructor in controller.GetConstructors())
                {
                    foreach (var parameter in constructor.GetParameters())
                    {
                        var paramTypeName = parameter.ParameterType.FullName ?? string.Empty;
                        Assert.False(
                            paramTypeName.Contains("DbContext", StringComparison.OrdinalIgnoreCase),
                            $"Controller {controller.FullName} directly injects DbContext ({paramTypeName}) in constructor.");
                    }
                }
            }
        }
    }

    [Fact]
    public void Microservices_Should_Not_Reference_Other_Microservice_Persistence()
    {
        // Each microservice must own its persistence (Database-per-Service principle).
        // Identity, Wallet, Ledger, Transfer, and Risk cannot reference other services' Infrastructure.
        var microservices = new[]
        {
            new
            {
                Name = "Identity",
                Assemblies = new[]
                {
                    Assembly.Load("Kumbara.Identity.Domain"),
                    Assembly.Load("Kumbara.Identity.Application"),
                    Assembly.Load("Kumbara.Identity.Infrastructure"),
                    Assembly.Load("Kumbara.Identity.Api")
                },
                ForbiddenInfrastructure = new[]
                {
                    "Kumbara.Wallet.Infrastructure",
                    "Kumbara.Ledger.Infrastructure",
                    "Kumbara.Transfer.Infrastructure",
                    "Kumbara.Risk.Infrastructure"
                }
            },
            new
            {
                Name = "Wallet",
                Assemblies = new[]
                {
                    Assembly.Load("Kumbara.Wallet.Domain"),
                    Assembly.Load("Kumbara.Wallet.Application"),
                    Assembly.Load("Kumbara.Wallet.Infrastructure"),
                    Assembly.Load("Kumbara.Wallet.Api")
                },
                ForbiddenInfrastructure = new[]
                {
                    "Kumbara.Identity.Infrastructure",
                    "Kumbara.Ledger.Infrastructure",
                    "Kumbara.Transfer.Infrastructure",
                    "Kumbara.Risk.Infrastructure"
                }
            },
            new
            {
                Name = "Ledger",
                Assemblies = new[]
                {
                    Assembly.Load("Kumbara.Ledger.Domain"),
                    Assembly.Load("Kumbara.Ledger.Application"),
                    Assembly.Load("Kumbara.Ledger.Infrastructure"),
                    Assembly.Load("Kumbara.Ledger.Api")
                },
                ForbiddenInfrastructure = new[]
                {
                    "Kumbara.Identity.Infrastructure",
                    "Kumbara.Wallet.Infrastructure",
                    "Kumbara.Transfer.Infrastructure",
                    "Kumbara.Risk.Infrastructure"
                }
            },
            new
            {
                Name = "Transfer",
                Assemblies = new[]
                {
                    Assembly.Load("Kumbara.Transfer.Domain"),
                    Assembly.Load("Kumbara.Transfer.Application"),
                    Assembly.Load("Kumbara.Transfer.Infrastructure"),
                    Assembly.Load("Kumbara.Transfer.Api")
                },
                ForbiddenInfrastructure = new[]
                {
                    "Kumbara.Identity.Infrastructure",
                    "Kumbara.Wallet.Infrastructure",
                    "Kumbara.Ledger.Infrastructure",
                    "Kumbara.Risk.Infrastructure"
                }
            },
            new
            {
                Name = "Risk",
                Assemblies = new[]
                {
                    Assembly.Load("Kumbara.Risk.Domain"),
                    Assembly.Load("Kumbara.Risk.Application"),
                    Assembly.Load("Kumbara.Risk.Infrastructure"),
                    Assembly.Load("Kumbara.Risk.Api")
                },
                ForbiddenInfrastructure = new[]
                {
                    "Kumbara.Identity.Infrastructure",
                    "Kumbara.Wallet.Infrastructure",
                    "Kumbara.Ledger.Infrastructure",
                    "Kumbara.Transfer.Infrastructure"
                }
            }
        };

        foreach (var ms in microservices)
        {
            var result = Types.InAssemblies(ms.Assemblies)
                .ShouldNot()
                .HaveDependencyOnAny(ms.ForbiddenInfrastructure)
                .GetResult();

            Assert.True(
                result.IsSuccessful,
                $"Microservice {ms.Name} must not reference other microservices' persistence/infrastructure. Violations: {string.Join(", ", result.FailingTypeNames ?? Enumerable.Empty<string>())}");
        }
    }
}

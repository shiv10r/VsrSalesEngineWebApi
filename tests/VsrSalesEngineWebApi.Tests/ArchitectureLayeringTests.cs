using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using Moq;
using VsrSalesEngineWebApi.Controllers;
using VsrSalesEngineWebApi.Core.Interfaces;
using Xunit;

namespace VsrSalesEngineWebApi.Tests;

public class ArchitectureLayeringTests
{
    [Fact]
    public void Controllers_MustNotReferenceRepositoriesDirectly()
    {
        var controllerAssembly = typeof(AuthController).Assembly;
        var controllerTypes = controllerAssembly.GetTypes()
            .Where(t => typeof(ControllerBase).IsAssignableFrom(t) && !t.IsAbstract)
            .ToList();

        Assert.NotEmpty(controllerTypes);

        foreach (var controller in controllerTypes)
        {
            var fields = controller.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            var constructors = controller.GetConstructors();

            foreach (var ctor in constructors)
            {
                foreach (var param in ctor.GetParameters())
                {
                    Assert.DoesNotContain("Repository", param.ParameterType.Name, StringComparison.OrdinalIgnoreCase);
                    Assert.DoesNotContain("DbContext", param.ParameterType.Name, StringComparison.OrdinalIgnoreCase);
                }
            }

            foreach (var field in fields)
            {
                Assert.DoesNotContain("Repository", field.FieldType.Name, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("DbContext", field.FieldType.Name, StringComparison.OrdinalIgnoreCase);
            }
        }
    }

    [Fact]
    public async Task OpportunityService_DeduplicatesIdenticalEntries()
    {
        var repoMock = new Mock<IOpportunityRepository>();
        repoMock.Setup(r => r.GetByDeduplicationKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Core.Entities.Opportunity
            {
                Id = "OP-EXISTING",
                Company = "Acme Corp",
                Title = "Senior Dev",
                Source = "Careers",
                Location = "Remote",
                DeduplicationKey = "acme corp|senior dev"
            });

        var service = new Core.Services.OpportunityService(repoMock.Object);

        var result = await service.CreateOpportunityAsync(new Core.DTOs.CreateOpportunityDto(
            "Acme Corp", "Senior Dev", "Careers", "Remote", "High", "Individual developer", false, null, null, null, null
        ));

        Assert.Equal("OP-EXISTING", result.Id);
        repoMock.Verify(r => r.AddAsync(It.IsAny<Core.Entities.Opportunity>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}

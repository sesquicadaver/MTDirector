using System.Reflection;
using Mfc.Application.Deployment;
using Mfc.Contracts.Mfc.V1;
using Mfc.Desktop.Services;
using Mfc.Desktop.ViewModels;
using Xunit;

namespace Mfc.UnitTests.Desktop;

/// <summary>AUDIT-GUI-01 / W7-232: no Desktop fabrications; sealed compile → Deploy handoff.</summary>
public sealed class DesktopAuditGui01LivingSpecTests
{
    [Fact]
    public void Ac1OnboardingViewModelHasNoFabricatedDefaultFactsOrDevicePlan()
    {
        string source = ReadSource("src/Mfc.Desktop/ViewModels/OnboardingViewModel.cs");
        Assert.DoesNotContain("onboarding-desktop", source, StringComparison.Ordinal);
        Assert.DoesNotContain("7.16.2", source, StringComparison.Ordinal);
        Assert.DoesNotContain("DefaultDevicePlan", source, StringComparison.Ordinal);
        Assert.DoesNotContain("DefaultFacts =", source, StringComparison.Ordinal);
        Assert.DoesNotContain("DefaultFacts()", source, StringComparison.Ordinal);
        Assert.Contains("no longer fabricates DefaultFacts", source, StringComparison.Ordinal);
        Assert.Contains("AUDIT-GUI-01", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Ac2DeploymentViewModelHasNoFabricatedArtifactsOrPacketPathInterfaces()
    {
        string source = ReadSource("src/Mfc.Desktop/ViewModels/DeploymentViewModel.cs");
        Assert.DoesNotContain("old-art", source, StringComparison.Ordinal);
        Assert.DoesNotContain("new-art", source, StringComparison.Ordinal);
        Assert.DoesNotContain("deployment-desktop", source, StringComparison.Ordinal);
        Assert.DoesNotContain("192.0.2.1", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ether1", source, StringComparison.Ordinal);
        Assert.Contains("CreatePlanFromSealedArtifactsAsync", source, StringComparison.Ordinal);
        Assert.Contains("ISealedCompileDeployHandoffStore", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Ac3PoliciesDeployStaysFailClosedWithSealedHandoffResidual()
    {
        string source = ReadSource("src/Mfc.Desktop/ViewModels/PoliciesViewModel.cs");
        Assert.Contains("CanNeverDeploy() => false", source, StringComparison.Ordinal);
        Assert.Contains("sealed artifact handoff", source, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Operations → Deploy", source, StringComparison.Ordinal);

        MethodInfo? canNeverDeploy = typeof(PoliciesViewModel).GetMethod(
            "CanNeverDeploy",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(canNeverDeploy);
        Assert.False(Assert.IsType<bool>(canNeverDeploy.Invoke(null, null)));
    }

    [Fact]
    public void Ac4DeploymentServiceExposesCreatePlanFromSealedArtifacts()
    {
        string[] methods = DeploymentService.Descriptor.Methods.Select(static m => m.Name).ToArray();
        Assert.Contains("CreatePlanFromSealedArtifacts", methods);
        Assert.NotNull(typeof(IDeploymentServiceClient).GetMethod(
            nameof(IDeploymentServiceClient.CreatePlanFromSealedArtifactsAsync)));
        Assert.Contains(
            "CreatePlanFromSealedArtifactsAsync",
            ReadSource("src/Mfc.Desktop/Services/GrpcDeploymentServiceClient.cs"),
            StringComparison.Ordinal);
    }

    [Fact]
    public void Ac5SealedPlanApplicationTypesExistWithoutInventingDocsClaims()
    {
        Assert.Equal(
            "CreateDeploymentPlanFromSealedArtifactsUseCase",
            typeof(CreateDeploymentPlanFromSealedArtifactsUseCase).Name);
        Assert.Equal("SealedDeploymentPlanBuilder", typeof(SealedDeploymentPlanBuilder).Name);

        string root = FindRepoRoot();
        Assert.True(File.Exists(Path.Combine(
            root,
            "src/Mfc.Application/Deployment/CreateDeploymentPlanFromSealedArtifactsUseCase.cs")));
        Assert.True(File.Exists(Path.Combine(
            root,
            "src/Mfc.Application/Deployment/SealedDeploymentPlanBuilder.cs")));
        Assert.True(File.Exists(Path.Combine(
            root,
            "src/Mfc.Desktop/Services/SealedCompileDeployHandoffStore.cs")));
    }

    private static string ReadSource(string relativePath)
        => File.ReadAllText(Path.Combine(FindRepoRoot(), relativePath));

    private static string FindRepoRoot()
    {
        DirectoryInfo? dir = new(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "ROADMAP.md")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException("Repository root not found.");
    }
}

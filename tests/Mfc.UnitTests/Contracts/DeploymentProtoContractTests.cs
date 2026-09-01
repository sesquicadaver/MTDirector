using Google.Protobuf.Reflection;
using Mfc.Contracts.Mfc.V1;
using Xunit;

namespace Mfc.UnitTests.Contracts;

public sealed class DeploymentProtoContractTests
{
    [Fact]
    public void DeploymentServiceExposesSeparateWorkflowRpcs()
    {
        string[] methods = DeploymentService.Descriptor.Methods.Select(static m => m.Name).OrderBy(n => n).ToArray();
        Assert.Equal(
            [
                "CreatePlan",
                "GetRecoveryStatus",
                "Rollback",
                "Start",
                "Watch",
            ],
            methods);
        Assert.Equal("mfc.v1.DeploymentService", DeploymentService.Descriptor.FullName);
        Assert.True(DeploymentService.Descriptor.FindMethodByName("Watch")!.IsServerStreaming);
        Assert.False(DeploymentService.Descriptor.FindMethodByName("Watch")!.IsClientStreaming);
    }

    [Fact]
    public void DeploymentPlanSummaryExposesTypedSemanticDiffEntries()
    {
        Assert.Equal("semantic_diff_entries", DeploymentPlanSummary.Descriptor.FindFieldByNumber(5)!.Name);
        Assert.True(DeploymentPlanSummary.Descriptor.FindFieldByNumber(5)!.IsRepeated);
        Assert.Equal("semantic_diff", DeploymentPlanSummary.Descriptor.FindFieldByNumber(9)!.Name);
        Assert.True(DeploymentPlanSummary.Descriptor.FindFieldByNumber(9)!.IsRepeated);
        string[] fields = DeploymentSemanticDiffEntry.Descriptor.Fields.InDeclarationOrder()
            .Select(static f => f.Name)
            .ToArray();
        Assert.Equal(["kind", "path", "device_id", "before", "after", "hash_delta"], fields);
        string[] kinds = DeploymentPlanSummary.Descriptor.File.EnumTypes
            .Single(static e => e.Name == "DeploymentSemanticDiffKind")
            .Values
            .Select(static v => v.Name)
            .ToArray();
        Assert.Contains("DEPLOYMENT_SEMANTIC_DIFF_KIND_ARTIFACT_UNCHANGED", kinds);
        Assert.Contains("DEPLOYMENT_SEMANTIC_DIFF_KIND_ARTIFACT_CHANGED", kinds);
    }

    [Fact]
    public void MutationRequestsRequireIdempotencyKeyAndStartRequiresPlanHash()
    {
        Assert.Equal("idempotency_key", CreateDeploymentPlanRequest.Descriptor.Fields.InDeclarationOrder()[0].Name);
        Assert.Equal("idempotency_key", StartDeploymentRequest.Descriptor.Fields.InDeclarationOrder()[0].Name);
        Assert.Equal("idempotency_key", RollbackDeploymentRequest.Descriptor.Fields.InDeclarationOrder()[0].Name);
        Assert.NotNull(StartDeploymentRequest.Descriptor.FindFieldByName("plan_hash"));
    }

    [Fact]
    public void ContractHasNoForceApplyOrRawCommandSurface()
    {
        foreach (DescriptorBase item in Walk(DeploymentService.Descriptor.File))
        {
            string name = item switch
            {
                MethodDescriptor method => method.Name,
                FieldDescriptor field => field.Name,
                MessageDescriptor message => message.Name,
                EnumDescriptor enumeration => enumeration.Name,
                _ => string.Empty,
            };
            Assert.DoesNotContain("force_apply", name, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("forceapply", name, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("script_source", name, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("ExecuteCommand", name, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("FreeForm", name, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("raw_command", name, StringComparison.OrdinalIgnoreCase);
        }
    }

    private static IEnumerable<DescriptorBase> Walk(FileDescriptor file)
    {
        foreach (MessageDescriptor message in file.MessageTypes)
        {
            foreach (DescriptorBase nested in Walk(message))
            {
                yield return nested;
            }
        }

        foreach (EnumDescriptor enumeration in file.EnumTypes)
        {
            yield return enumeration;
        }

        foreach (ServiceDescriptor service in file.Services)
        {
            foreach (MethodDescriptor method in service.Methods)
            {
                yield return method;
            }
        }
    }

    private static IEnumerable<DescriptorBase> Walk(MessageDescriptor message)
    {
        yield return message;
        foreach (FieldDescriptor field in message.Fields.InDeclarationOrder())
        {
            yield return field;
        }

        foreach (MessageDescriptor nested in message.NestedTypes)
        {
            foreach (DescriptorBase child in Walk(nested))
            {
                yield return child;
            }
        }

        foreach (EnumDescriptor enumeration in message.EnumTypes)
        {
            yield return enumeration;
        }
    }
}

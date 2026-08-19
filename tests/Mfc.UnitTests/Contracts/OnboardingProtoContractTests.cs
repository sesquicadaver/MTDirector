using Google.Protobuf.Reflection;
using Mfc.Contracts.Mfc.V1;
using Xunit;

namespace Mfc.UnitTests.Contracts;

public sealed class OnboardingProtoContractTests
{
    [Fact]
    public void OnboardingServiceExposesSeparateWorkflowRpcs()
    {
        string[] methods = OnboardingService.Descriptor.Methods.Select(static m => m.Name).OrderBy(n => n).ToArray();
        Assert.Equal(
            [
                "CreatePlan",
                "GetRecoveryStatus",
                "Rollback",
                "Start",
                "ValidatePrerequisites",
                "Watch",
            ],
            methods);
        Assert.Equal("mfc.v1.OnboardingService", OnboardingService.Descriptor.FullName);
        Assert.True(OnboardingService.Descriptor.FindMethodByName("Watch")!.IsServerStreaming);
        Assert.False(OnboardingService.Descriptor.FindMethodByName("Watch")!.IsClientStreaming);
    }

    [Fact]
    public void MutationRequestsRequireIdempotencyKey()
    {
        Assert.Equal("idempotency_key", CreateOnboardingPlanRequest.Descriptor.Fields.InDeclarationOrder()[0].Name);
        Assert.Equal("idempotency_key", StartOnboardingRequest.Descriptor.Fields.InDeclarationOrder()[0].Name);
        Assert.Equal("idempotency_key", RollbackOnboardingRequest.Descriptor.Fields.InDeclarationOrder()[0].Name);
        Assert.NotNull(StartOnboardingRequest.Descriptor.FindFieldByName("plan_hash"));
    }

    [Fact]
    public void ContractHasNoScriptSourceOrArbitraryWriteSurface()
    {
        foreach (DescriptorBase item in Walk(OnboardingService.Descriptor.File))
        {
            string name = item switch
            {
                MethodDescriptor method => method.Name,
                FieldDescriptor field => field.Name,
                MessageDescriptor message => message.Name,
                EnumDescriptor enumeration => enumeration.Name,
                _ => string.Empty,
            };
            Assert.DoesNotContain("script_source", name, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("scriptsource", name, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("ExecuteCommand", name, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("FreeForm", name, StringComparison.OrdinalIgnoreCase);
        }

        Assert.DoesNotContain(
            OnboardingService.Descriptor.Methods,
            static m => m.Name.Contains("Write", StringComparison.OrdinalIgnoreCase)
                        && m.Name is not "Rollback");
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

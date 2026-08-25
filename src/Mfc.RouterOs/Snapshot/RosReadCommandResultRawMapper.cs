using Mfc.RouterOs.Commands;

namespace Mfc.RouterOs.Snapshot;

/// <summary>Maps typed read results into raw snapshot section inputs (M1-20).</summary>
public static class RosReadCommandResultRawMapper
{
    public static RawSectionCaptureInput ToRawSection(RosReadCommandResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        RosReadCommandDefinition definition = RosReadCommandRegistry.Get(result.CommandId);
        RawSectionCaptureStatus status = result.IsSuccess
            ? RawSectionCaptureStatus.Completed
            : RawSectionCaptureStatus.Failed;

        List<RawRecordInput> records = new(result.Records.Count);
        foreach (RosReadRecord record in result.Records)
        {
            records.Add(new RawRecordInput
            {
                KnownProperties = record.KnownProperties,
                UnknownProperties = record.RawProperties,
            });
        }

        return new RawSectionCaptureInput
        {
            SourceMenu = definition.FixedPath,
            CommandId = result.CommandId,
            CaptureStatus = status,
            ErrorCode = result.Error?.Code,
            ErrorMessage = result.Error?.Message,
            Records = records,
        };
    }
}

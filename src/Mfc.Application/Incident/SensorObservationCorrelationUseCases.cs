using Mfc.Application.Abstractions.Authorization;
using Mfc.Application.Common;
using Mfc.Application.Models;
using Mfc.Domain;
using Mfc.Domain.Incident;
using Auth = Mfc.Application.Common.AuthorizationGuard;

namespace Mfc.Application.Incident;

public sealed class CorrelateSensorObservationCommand
{
    public required string Actor { get; init; }

    public required SensorObservationCorrelationQuery Query { get; init; }
}

/// <summary>
/// Correlates Wazuh/Suricata sensor observation points with route resolution traces (M7.3-04).
/// </summary>
public sealed class CorrelateSensorObservationUseCase
{
    private readonly IAuthorizationBoundary _auth;

    public CorrelateSensorObservationUseCase(IAuthorizationBoundary auth)
    {
        ArgumentNullException.ThrowIfNull(auth);
        _auth = auth;
    }

    public async Task<ApplicationResult<SensorObservationCorrelationResultView>> ExecuteAsync(
        CorrelateSensorObservationCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(command.Query);

        ApplicationError? authError = await Auth.EnsureAsync(
            _auth,
            command.Actor,
            ApplicationPermissions.IncidentCorrelationRead,
            cancellationToken).ConfigureAwait(false);
        if (authError is not null)
        {
            return ApplicationResults.Fail(authError);
        }

        try
        {
            SensorObservationCorrelationResult result = SensorObservationCorrelationResolver.Correlate(command.Query);
            return ApplicationResults.Ok(SensorObservationCorrelationResultView.FromResult(result));
        }
        catch (DomainInvariantException ex)
        {
            return ApplicationResults.Fail(ApplicationError.Validation(ex.Message));
        }
        catch (ArgumentException ex)
        {
            return ApplicationResults.Fail(ApplicationError.Validation(ex.Message));
        }
    }
}

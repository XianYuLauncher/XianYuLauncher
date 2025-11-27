namespace XMCL2025.Contracts.Services;

public interface IActivationService
{
    Task ActivateAsync(object activationArgs);
}

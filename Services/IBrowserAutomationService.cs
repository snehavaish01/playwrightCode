using MooseBrowserAutomationService.Models;

namespace MooseBrowserAutomationService.Services
{
    public interface IBrowserAutomationService
    {
        Task<BrowserAutomationResponse> ValidateCredentialsAsync(MooseCredentials credentials);
        Task<BrowserAutomationResponse> ForceSyncAsync(MooseCredentials credentials);
        Task<bool> IsServiceHealthyAsync();
    }
}
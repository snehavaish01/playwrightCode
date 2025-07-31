namespace MooseBrowserAutomationService.Models
{
    public class MooseCredentials
    {
        public int PId { get; set; }
        public int LocalLodgeId { get; set; }
        public string MemberId { get; set; } = string.Empty;
        public string Lastname { get; set; } = string.Empty;
        public string FraternalUnitType { get; set; } = string.Empty;
        public string FRUNumber { get; set; } = string.Empty;
        public string FraternalUnitPasscode { get; set; } = string.Empty;
        public string? IclUrl { get; set; }
    }

    public class BrowserAutomationResponse
    {
        public int StatusCode { get; set; }
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public object? Data { get; set; }
    }
}
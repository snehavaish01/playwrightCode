using Microsoft.Playwright;
using MooseBrowserAutomationService.Models;

namespace MooseBrowserAutomationService.Services
{
    public class BrowserAutomationService : IBrowserAutomationService, IDisposable
    {
        private readonly ILogger<BrowserAutomationService> _logger;
        private readonly IConfiguration _configuration;
        private IBrowser? _browser;
        private IPlaywright? _playwright;
        private bool _isInitialized = false;

        public BrowserAutomationService(ILogger<BrowserAutomationService> logger, IConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;
        }

        private async Task<IBrowser> InitializeBrowserAsync()
        {
            if (_browser == null || !_isInitialized)
            {
                try
                {
                    _playwright = await Playwright.CreateAsync();
                    _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
                    {
                        Headless = true, // Set to true for production
                        Args = new[]
                        {
                            "--no-sandbox",
                            "--disable-setuid-sandbox",
                            "--disable-dev-shm-usage",
                            "--disable-gpu",
                            "--disable-web-security",
                            "--allow-running-insecure-content"
                        }
                    });
                    _isInitialized = true;
                    _logger.LogInformation("Browser initialized successfully");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to initialize browser");
                    throw;
                }
            }
            return _browser;
        }

        public async Task<BrowserAutomationResponse> ValidateCredentialsAsync(MooseCredentials credentials)
        {
            IPage? page = null;
            IBrowserContext? context = null;

            try
            {
                _logger.LogInformation("Validating credentials for MID: {MemberId}", credentials.MemberId);

                // Validate input
                if (string.IsNullOrEmpty(credentials.MemberId) ||
                    string.IsNullOrEmpty(credentials.Lastname) ||
                    string.IsNullOrEmpty(credentials.FRUNumber) ||
                    string.IsNullOrEmpty(credentials.FraternalUnitPasscode))
                {
                    return new BrowserAutomationResponse
                    {
                        StatusCode = 400,
                        Success = false,
                        Message = "All credential fields are required"
                    };
                }

                var iclUrl = credentials.IclUrl ?? _configuration["MooseOSettings:ICL_URL"];
                if (string.IsNullOrEmpty(iclUrl))
                {
                    _logger.LogWarning("ICL URL is missing in configuration");
                    return new BrowserAutomationResponse
                    {
                        StatusCode = 400,
                        Success = false,
                        Message = "ICL URL is missing in configuration"
                    };
                }

                var browser = await InitializeBrowserAsync();
                context = await browser.NewContextAsync(new BrowserNewContextOptions
                {
                    ViewportSize = new ViewportSize { Width = 1280, Height = 720 }
                });
                page = await context.NewPageAsync();

                // Set timeouts
                page.SetDefaultTimeout(30000);
                page.SetDefaultNavigationTimeout(30000);

                _logger.LogInformation("Navigating to ICL URL: {Url}", iclUrl);
                await page.GotoAsync(iclUrl, new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });

                // Wait for login form to load
                await page.WaitForSelectorAsync("input[name='ctl00$pageContent$logUser$UserName']",
                    new PageWaitForSelectorOptions { Timeout = 15000 });

                // Fill login form
                _logger.LogInformation("Filling login form for MID: {MemberId}", credentials.MemberId);
                await page.FillAsync("input[name='ctl00$pageContent$logUser$UserName']", credentials.MemberId);
                await page.FillAsync("input[name='ctl00$pageContent$logUser$txtMemberLastName']", credentials.Lastname);
                await page.FillAsync("input[name='ctl00$pageContent$logUser$txtFRUNumber']", credentials.FRUNumber);
                await page.FillAsync("input[name='ctl00$pageContent$logUser$Password']", credentials.FraternalUnitPasscode);

                // Submit form
                _logger.LogInformation("Submitting login form for MID: {MemberId}", credentials.MemberId);
                await page.ClickAsync("input[name='ctl00$pageContent$logUser$LoginButton']");

                // Wait for response
                await page.WaitForLoadStateAsync(LoadState.NetworkIdle, new PageWaitForLoadStateOptions { Timeout = 20000 });

                // Check for error message
                var errorVisible = await page.Locator("#ctl00_ctlMessageBox_DetailsLabel")
                    .IsVisibleAsync(new LocatorIsVisibleOptions { Timeout = 3000 });

                if (errorVisible)
                {
                    var errorText = await page.InnerTextAsync("#ctl00_ctlMessageBox_DetailsLabel");
                    _logger.LogWarning("Login failed for MID {MemberId}: {ErrorText}", credentials.MemberId, errorText);

                    return new BrowserAutomationResponse
                    {
                        StatusCode = 400,
                        Success = false,
                        Message = $"Wrong Credential: {errorText}"
                    };
                }

                // Check if we're actually logged in by looking for expected elements
                var isLoggedIn = await page.Locator("#ctl00_mnuMainn3").IsVisibleAsync(new LocatorIsVisibleOptions { Timeout = 5000 });

                if (!isLoggedIn)
                {
                    return new BrowserAutomationResponse
                    {
                        StatusCode = 400,
                        Success = false,
                        Message = "Login failed - could not verify successful login"
                    };
                }

                _logger.LogInformation("Login successful for MID: {MemberId}", credentials.MemberId);

                return new BrowserAutomationResponse
                {
                    StatusCode = 200,
                    Success = true,
                    Message = "Credentials validated successfully"
                };
            }
            catch (TimeoutException ex)
            {
                _logger.LogError(ex, "Timeout error validating credentials for MID: {MemberId}", credentials.MemberId);
                return new BrowserAutomationResponse
                {
                    StatusCode = 500,
                    Success = false,
                    Message = "Login page took too long to respond. Please try again."
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating credentials for MID: {MemberId}", credentials.MemberId);
                return new BrowserAutomationResponse
                {
                    StatusCode = 500,
                    Success = false,
                    Message = $"Validation error: {ex.Message}"
                };
            }
            finally
            {
                if (page != null) await page.CloseAsync();
                if (context != null) await context.CloseAsync();
            }
        }

        public async Task<BrowserAutomationResponse> ForceSyncAsync(MooseCredentials credentials)
        {
            IPage? page = null;
            IBrowserContext? context = null;

            try
            {
                _logger.LogInformation("Starting ForceSync for MID: {MemberId}", credentials.MemberId);

                var iclUrl = credentials.IclUrl ?? _configuration["MooseOSettings:ICL_URL"];
                if (string.IsNullOrEmpty(iclUrl))
                {
                    return new BrowserAutomationResponse
                    {
                        StatusCode = 400,
                        Success = false,
                        Message = "ICL URL is missing"
                    };
                }

                var downloadPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MBES");
                if (!Directory.Exists(downloadPath))
                    Directory.CreateDirectory(downloadPath);

                var browser = await InitializeBrowserAsync();
                context = await browser.NewContextAsync(new BrowserNewContextOptions
                {
                    AcceptDownloads = true,
                    ViewportSize = new ViewportSize { Width = 1280, Height = 720 }
                });

                page = await context.NewPageAsync();
                page.SetDefaultTimeout(30000);

                // Login process (same as validate)
                await page.GotoAsync(iclUrl, new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
                await page.WaitForSelectorAsync("input[name='ctl00$pageContent$logUser$UserName']");

                await page.FillAsync("input[name='ctl00$pageContent$logUser$UserName']", credentials.MemberId);
                await page.FillAsync("input[name='ctl00$pageContent$logUser$txtMemberLastName']", credentials.Lastname);
                await page.FillAsync("input[name='ctl00$pageContent$logUser$txtFRUNumber']", credentials.FRUNumber);
                await page.FillAsync("input[name='ctl00$pageContent$logUser$Password']", credentials.FraternalUnitPasscode);

                await page.ClickAsync("input[name='ctl00$pageContent$logUser$LoginButton']");
                await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

                // Check for login error
                if (await page.Locator("#ctl00_ctlMessageBox_DetailsLabel").IsVisibleAsync(new LocatorIsVisibleOptions { Timeout = 5000 }))
                {
                    return new BrowserAutomationResponse
                    {
                        StatusCode = 400,
                        Success = false,
                        Message = "Wrong Credentials"
                    };
                }

                // Handle modals
                for (int k = 0; k < 3; k++)
                {
                    try
                    {
                        if (await page.Locator("input[name='ctl00$pageContent$btnOutstandingAppsModalOk']")
                            .IsVisibleAsync(new LocatorIsVisibleOptions { Timeout = 2000 }))
                        {
                            await page.ClickAsync("input[name='ctl00$pageContent$btnOutstandingAppsModalOk']");
                            await Task.Delay(1000);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug("Modal handling attempt {Attempt} failed: {Error}", k + 1, ex.Message);
                    }
                }

                // Navigate to Export
                await page.HoverAsync("#ctl00_mnuMainn3 > table > tbody > tr > td > a");
                await page.ClickAsync("text=Export");
                await Task.Delay(2000);

                await page.CheckAsync("#ctl00_pageContent_cbSelectAllmemberFields");
                await Task.Delay(2000);

                if (await page.Locator("#ctl00_pageContent_btnMoveRight").IsVisibleAsync())
                    await page.ClickAsync("#ctl00_pageContent_btnMoveRight");

                await page.CheckAsync("#ctl00_pageContent_lvMemberStatus_ctrl0_cbSelected");
                await page.CheckAsync("#ctl00_pageContent_lvMemberStatus_ctrl4_cbSelected");

                var downloadTask = page.WaitForDownloadAsync(new PageWaitForDownloadOptions { Timeout = 60000 });
                await page.ClickAsync("#ctl00_pageContent_btnExportData");
                var download = await downloadTask;

                var filePath = Path.Combine(downloadPath, download.SuggestedFilename ?? $"export_{DateTime.Now:yyyyMMdd_HHmmss}.csv");
                await download.SaveAsAsync(filePath);

                _logger.LogInformation("Force sync completed successfully. File saved: {FilePath}", filePath);

                return new BrowserAutomationResponse
                {
                    StatusCode = 200,
                    Success = true,
                    Message = "Force sync completed successfully",
                    Data = new { FilePath = filePath, FileName = download.SuggestedFilename }
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in ForceSync for MID: {MemberId}", credentials.MemberId);
                return new BrowserAutomationResponse
                {
                    StatusCode = 500,
                    Success = false,
                    Message = $"Force sync error: {ex.Message}"
                };
            }
            finally
            {
                if (page != null) await page.CloseAsync();
                if (context != null) await context.CloseAsync();
            }
        }

        public async Task<bool> IsServiceHealthyAsync()
        {
            try
            {
                if (_browser == null)
                {
                    await InitializeBrowserAsync();
                }
                return _browser != null && _isInitialized;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Health check failed");
                return false;
            }
        }

        public void Dispose()
        {
            try
            {
                _browser?.CloseAsync().Wait(5000);
                _playwright?.Dispose();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error disposing browser automation service");
            }
        }
    }
}
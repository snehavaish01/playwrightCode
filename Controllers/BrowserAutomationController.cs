using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using MooseBrowserAutomationService.Models;
using MooseBrowserAutomationService.Services;
using System.Text.Json;

namespace MooseBrowserAutomationService.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [EnableCors] // Enable CORS for this controller
    public class BrowserAutomationController : ControllerBase
    {
        private readonly IBrowserAutomationService _browserService;
        private readonly ILogger<BrowserAutomationController> _logger;

        public BrowserAutomationController(IBrowserAutomationService browserService, ILogger<BrowserAutomationController> logger)
        {
            _browserService = browserService;
            _logger = logger;
        }

        [HttpOptions("validate-credentials")]
        public IActionResult PreflightValidateCredentials()
        {
            return Ok();
        }

        [HttpPost("validate-credentials")]
        public async Task<IActionResult> ValidateCredentials([FromBody] MooseCredentials credentials)
        {
            _logger.LogInformation("=== VALIDATE CREDENTIALS CALLED ===");

            try
            {
                if (credentials == null)
                {
                    var errorResponse = new BrowserAutomationResponse
                    {
                        StatusCode = 400,
                        Success = false,
                        Message = "Invalid request body"
                    };

                    _logger.LogWarning("Null credentials received");
                    return BadRequest(errorResponse);
                }

                _logger.LogInformation("Processing credentials for MID: {MemberId}", credentials.MemberId);

                var result = await _browserService.ValidateCredentialsAsync(credentials);

                // Ensure result is not null
                if (result == null)
                {
                    result = new BrowserAutomationResponse
                    {
                        StatusCode = 500,
                        Success = false,
                        Message = "Internal service returned null response"
                    };
                }

                _logger.LogInformation("ValidateCredentials completed with status: {StatusCode}, Success: {Success}, Message: {Message}",
                    result.StatusCode, result.Success, result.Message);

                // Return the response with proper status code
                if (result.StatusCode == 200)
                {
                    return Ok(result);
                }
                else if (result.StatusCode == 400)
                {
                    return BadRequest(result);
                }
                else
                {
                    return StatusCode(result.StatusCode, result);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in ValidateCredentials");

                var errorResponse = new BrowserAutomationResponse
                {
                    StatusCode = 500,
                    Success = false,
                    Message = "Internal server error"
                };

                return StatusCode(500, errorResponse);
            }
        }

        [HttpOptions("force-sync")]
        public IActionResult PreflightForceSync()
        {
            return Ok();
        }

        [HttpPost("force-sync")]
        public async Task<IActionResult> ForceSync([FromBody] MooseCredentials credentials)
        {
            try
            {
                if (credentials == null)
                {
                    var errorResponse = new BrowserAutomationResponse
                    {
                        StatusCode = 400,
                        Success = false,
                        Message = "Invalid request body"
                    };
                    return BadRequest(errorResponse);
                }

                var result = await _browserService.ForceSyncAsync(credentials);

                if (result == null)
                {
                    result = new BrowserAutomationResponse
                    {
                        StatusCode = 500,
                        Success = false,
                        Message = "Internal service returned null response"
                    };
                }

                _logger.LogInformation("ForceSync completed with status: {StatusCode}, Success: {Success}",
                    result.StatusCode, result.Success);

                if (result.StatusCode == 200)
                {
                    return Ok(result);
                }
                else if (result.StatusCode == 400)
                {
                    return BadRequest(result);
                }
                else
                {
                    return StatusCode(result.StatusCode, result);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in ForceSync");

                var errorResponse = new BrowserAutomationResponse
                {
                    StatusCode = 500,
                    Success = false,
                    Message = "Internal server error"
                };

                return StatusCode(500, errorResponse);
            }
        }

        [HttpGet("health")]
        public async Task<IActionResult> Health()
        {
            try
            {
                _logger.LogInformation("Health check called");
                var isHealthy = await _browserService.IsServiceHealthyAsync();

                var result = new BrowserAutomationResponse
                {
                    StatusCode = 200,
                    Success = isHealthy,
                    Message = isHealthy ? "Service is healthy" : "Service is not healthy"
                };

                _logger.LogInformation("Health check completed: Success={Success}", result.Success);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in Health check");

                var errorResponse = new BrowserAutomationResponse
                {
                    StatusCode = 500,
                    Success = false,
                    Message = "Health check failed"
                };

                return StatusCode(500, errorResponse);
            }
        }

        [HttpGet("test")]
        public IActionResult Test()
        {
            _logger.LogInformation("Test endpoint called");

            var result = new
            {
                message = "API is working",
                timestamp = DateTime.UtcNow,
                machine = Environment.MachineName,
                success = true
            };

            _logger.LogInformation("Test endpoint returning success");
            return Ok(result);
        }
    }
}
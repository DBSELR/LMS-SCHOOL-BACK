using LMS.DTOs;
using LMS.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace LMS.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PaymentsController : ControllerBase
    {
        private readonly PhonePeService _phonePeService;
        private readonly ILogger<PaymentsController> _logger;

        public PaymentsController(
            PhonePeService phonePeService,
            ILogger<PaymentsController> logger)
        {
            _phonePeService = phonePeService;
            _logger = logger;
        }

        // POST: /api/payments/phonepe/initiate
        [HttpPost("phonepe/initiate")]
        [AllowAnonymous]
        public async Task<IActionResult> InitiatePhonePe([FromBody] PhonePeInitiateDto dto)
        {
            if (dto == null || string.IsNullOrWhiteSpace(dto.Username))
                return BadRequest("Username is required.");

            if (dto.Amount <= 0)
                return BadRequest("Amount must be greater than zero.");

            try
            {
                var result = await _phonePeService.InitiatePaymentAsync(
                    dto.Username,
                    dto.Amount);

                return Ok(new
                {
                    redirectUrl = result.RedirectUrl,
                    merchantOrderId = result.MerchantOrderId,
                    message = "PhonePe payment initiated"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initiating PhonePe payment.");
                return StatusCode(500, new { error = ex.Message });
            }
        }

        // GET: /api/payments/phonepe/status?merchantOrderId=...
        [HttpGet("phonepe/status")]
        [AllowAnonymous]
        public async Task<IActionResult> GetPhonePeStatus([FromQuery] string merchantOrderId)
        {
            if (string.IsNullOrWhiteSpace(merchantOrderId))
                return BadRequest("merchantOrderId is required.");

            try
            {
                var status = await _phonePeService.GetAndUpdatePaymentStatusAsync(merchantOrderId);
                return Ok(status);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching PhonePe status.");
                return StatusCode(500, new { error = ex.Message });
            }
        }

        // OPTIONAL: webhook (if you configure CallbackUrl later)
        [HttpPost("phonepe/callback")]
        [AllowAnonymous]
        public async Task<IActionResult> PhonePeCallback([FromBody] JsonElement payload)
        {
            _logger.LogInformation("PhonePe callback payload: {Payload}", payload.GetRawText());

            string merchantOrderId = null;
            if (payload.TryGetProperty("merchantOrderId", out var idEl))
            {
                merchantOrderId = idEl.GetString();
            }

            if (!string.IsNullOrWhiteSpace(merchantOrderId))
            {
                try
                {
                    await _phonePeService.GetAndUpdatePaymentStatusAsync(merchantOrderId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing PhonePe callback.");
                }
            }

            return Ok();
        }
    }
}

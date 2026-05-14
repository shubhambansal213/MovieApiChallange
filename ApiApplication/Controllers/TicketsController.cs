using System;
using System.Threading;
using System.Threading.Tasks;
using ApiApplication.Models;
using ApiApplication.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace ApiApplication.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TicketsController : ControllerBase
    {
        private readonly IReservationService _reservationService;
        private readonly ILogger<TicketsController> _logger;

        public TicketsController(
            IReservationService reservationService,
            ILogger<TicketsController> logger)
        {
            _reservationService = reservationService;
            _logger = logger;
        }

        /// <summary>
        /// Confirm/Buy a reservation
        /// </summary>
        /// <remarks>
        /// Rules:
        /// - Reservation must exist and not be already paid
        /// - Reservation must not be expired (must be within 10 minutes of creation)
        /// - Cannot buy already sold seats
        /// </remarks>
        [HttpPost("confirm")]
        [ProducesResponseType(typeof(ConfirmPaymentResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> ConfirmPayment([FromBody] ConfirmPaymentRequest request, CancellationToken cancellationToken)
        {
            if (request.ReservationId == Guid.Empty)
            {
                return BadRequest(new { message = "Invalid ReservationId" });
            }

            var result = await _reservationService.ConfirmReservationAsync(request.ReservationId, cancellationToken);

            if (!result.Success)
            {
                _logger.LogWarning("Failed to confirm reservation {ReservationId}: {Message}",
                    request.ReservationId, result.Message);
                
                return BadRequest(result);
            }

            _logger.LogInformation("Successfully confirmed payment for reservation {ReservationId}",
                request.ReservationId);

            return Ok(result);
        }

        /// <summary>
        /// Alternative endpoint: Confirm payment using reservation ID in route
        /// </summary>
        [HttpPut("{reservationId}/confirm")]
        [ProducesResponseType(typeof(ConfirmPaymentResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> ConfirmPaymentByRoute(Guid reservationId, CancellationToken cancellationToken)
        {
            if (reservationId == Guid.Empty)
            {
                return BadRequest(new { message = "Invalid ReservationId" });
            }

            var result = await _reservationService.ConfirmReservationAsync(reservationId, cancellationToken);

            if (!result.Success)
            {
                _logger.LogWarning("Failed to confirm reservation {ReservationId}: {Message}",
                    reservationId, result.Message);
                
                return BadRequest(result);
            }

            _logger.LogInformation("Successfully confirmed payment for reservation {ReservationId}",
                reservationId);

            return Ok(result);
        }
    }
}

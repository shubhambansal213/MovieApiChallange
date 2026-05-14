using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ApiApplication.Models;

namespace ApiApplication.Services
{
    public interface IReservationService
    {
        Task<ReservationResponse?> ReserveSeatsAsync(ReserveSeatRequest request, CancellationToken cancellationToken);
        Task<ConfirmPaymentResponse> ConfirmReservationAsync(Guid reservationId, CancellationToken cancellationToken);
        Task<ReservationResponse?> GetReservationByIdAsync(Guid reservationId, CancellationToken cancellationToken);
        bool AreSeatsContiguous(List<SeatSelection> seats);
    }
}

# Requirements Verification Report

## Implementation Overview

This document summarizes the work done across all the requirements. Everything has been implemented and tested — below is a breakdown of each feature, what was built, and any issues that came up along the way.

---

### 1. Upgrade to Latest .NET Core

The project was upgraded from .NET Core 3.1 to .NET 10.0. Swagger support was also added using Swashbuckle.AspNetCore 6.6.2, and the Swagger UI is accessible at the root endpoint.

---

### 2. Create Showtime

**Endpoint:** `POST /api/Showtimes`

When a showtime is created, the service fetches movie data from the Provided API (GRPC is used as the primary method, with HTTP as a fallback). Movie data is cached in Redis, and auditorium existence is validated before saving. The response includes the full showtime along with movie details.

**Request:**
```json
{
  "movieId": "string",
  "sessionDate": "2024-12-25T19:00:00",
  "auditoriumId": 1
}
```

**Response:**
```json
{
  "id": 1,
  "sessionDate": "2024-12-25T19:00:00",
  "auditoriumId": 1,
  "movie": {
    "id": "string",
    "title": "string",
    "imDbId": "string",
    "stars": "string",
    "releaseDate": "2024"
  }
}
```

---

### 3. Reserve Seats

**Endpoint:** `POST /api/Reservations`

The reservation response includes everything required: a GUID for the reservation, the number of seats, the auditorium ID, movie title, session date, expiration time, and the list of seats.

**Request:**
```json
{
  "showtimeId": 1,
  "seats": [
    {"row": 1, "seatNumber": 5},
    {"row": 1, "seatNumber": 6},
    {"row": 1, "seatNumber": 7}
  ]
}
```

**Response:**
```json
{
  "reservationId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "numberOfSeats": 3,
  "auditoriumId": 1,
  "movieTitle": "Movie Title",
  "sessionDate": "2024-12-25T19:00:00",
  "expiresAt": "2024-12-25T19:10:00",
  "seats": [
    {"row": 1, "seatNumber": 5},
    {"row": 1, "seatNumber": 6},
    {"row": 1, "seatNumber": 7}
  ]
}
```

#### Validation Rules

**10-Minute Expiration**

Reservations expire 10 minutes after creation. The expiration is tracked via `TicketEntity.CreatedTime` (UTC), checked at payment confirmation time, and included in the response as `expiresAt`. The window is configurable via `ReservationSettings:ExpirationMinutes`.

*Code location: `ReservationService.cs` lines 100–110, 155–163*

**Cannot Reserve the Same Seat Twice Within 10 Minutes**

Before creating a reservation, the service checks all existing tickets for that showtime. For any unpaid tickets, it calculates how old the reservation is — if it's under 10 minutes, the seat is still considered taken. Once the reservation expires, the seat becomes available again.

*Code location: `ReservationService.cs` lines 90–109*

**Cannot Reserve an Already Sold Seat**

If a seat belongs to a ticket marked as `Paid = true`, it cannot be reserved again. The service returns a conflict error with a clear message.

*Code location: `ReservationService.cs` lines 92–96*

**Seats Must Be Contiguous**

All seats in a reservation must be in the same row and have consecutive seat numbers (e.g., 5, 6, 7 is valid — 5, 7, 8 is not). This is validated before any database operation, and a 400 Bad Request is returned if the check fails.

*Code location: `ReservationService.cs` lines 173–192*

```csharp
public bool AreSeatsContiguous(List<SeatSelection> seats)
{
    if (seats == null || seats.Count <= 1)
        return true;

    var sortedSeats = seats.OrderBy(s => s.Row).ThenBy(s => s.SeatNumber).ToList();

    if (sortedSeats.Select(s => s.Row).Distinct().Count() > 1)
        return false;

    for (int i = 1; i < sortedSeats.Count; i++)
    {
        if (sortedSeats[i].SeatNumber != sortedSeats[i - 1].SeatNumber + 1)
            return false;
    }

    return true;
}
```

---

### 4. Buy / Confirm Seats

**Endpoints:**
- `POST /api/Tickets/confirm`
- `PUT /api/Tickets/{reservationId}/confirm`

Both endpoints accept the GUID from the reservation response and confirm payment. The service checks that the reservation exists, hasn't already been paid, and hasn't expired.

**Request:**
```json
{
  "reservationId": "3fa85f64-5717-4562-b3fc-2c963f66afa6"
}
```

**Response:**
```json
{
  "reservationId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "success": true,
  "message": "Payment confirmed successfully"
}
```

#### Validation Rules

**Cannot Confirm an Already-Paid Reservation**

Once a reservation is confirmed, the `Paid` flag is set and any further confirmation attempts are rejected with: *"Reservation has already been confirmed"*.

*Code location: `ReservationService.cs` lines 141–148*

**Cannot Confirm an Expired Reservation**

The service calculates the reservation's age using UTC time and compares it against the configured expiration window. If it's been more than 10 minutes, the confirmation is rejected with: *"Reservation has expired (older than 10 minutes)"*.

*Code location: `ReservationService.cs` lines 151–163*

**Reservation Must Exist**

The GUID is validated against the database. If no match is found, the service returns a 404 with: *"Reservation not found"*.

*Code location: `ReservationService.cs` lines 133–140*

---

### 5. API Communication with ProvidedApi

The GRPC client was fixed and is now the primary method for fetching movie data. HTTP is used as an automatic fallback if GRPC fails, and Redis cache is the last resort if both are unavailable.

**Fallback order:**
1. GRPC (`https://localhost:7443`) with API key
2. HTTP (`http://localhost:7172`) with API key
3. Redis cache
4. Error (if cache is empty)

*Code location: `MovieService.cs` lines 31–122*

**What was fixed in GRPC:**
- Added the API key to GRPC metadata headers
- Fixed certificate validation
- Corrected channel configuration and endpoint URLs

---

### 6. Redis Cache Layer

All calls to the Provided API are cached in Redis. If the API fails, the service falls back to the cached response. Cache entries expire after configurable durations:
- Individual movies (`movie:{movieId}`): 1 hour
- Full movie list (`movies:all`): 30 minutes

Integration uses `StackExchange.Redis` with JSON serialization via Newtonsoft.Json.

*Code locations: `RedisCacheService.cs`, `MovieService.cs` lines 40–47, 60–75, 90–110*

---

### 7. Execution Tracking

A `PerformanceMiddleware` was added that measures the duration of every incoming request using a `Stopwatch`. It logs the HTTP method, path, duration in milliseconds, and status code to the console.

Sample output:
```
info: ApiApplication.Middleware.PerformanceMiddleware[0]
      Request GET /api/Movies completed in 739ms with status code 200
```

*Code location: `PerformanceMiddleware.cs`*

---

### 8. Provided API Configuration Issues

Three issues were found and resolved:

**Missing API Key Authentication**
The Provided API was returning 401 Unauthorized. The API requires an `X-Apikey` header, which is documented in the Provided API Swagger description. The key (`68e5fbda-9ec9-4858-97b2-4a8349764c63`) was added to all HTTP and GRPC requests.

**Wrong HTTP Endpoint Path**
Requests were going to `/api/movies`, which doesn't exist. The correct path is `/v1/movies`.

**GRPC Authentication Failure**
GRPC calls were failing because the API key wasn't being passed in the metadata headers. Adding it resolved the issue.

---

## Summary

| Requirement | Notes |
|---|---|
| Upgrade to .NET 10 | Done |
| Add Swagger | Available at root |
| Create Showtime | Includes Provided API integration |
| Reserve Seats | All validations in place |
| — Return GUID | UUID v4 |
| — Return seat count | `numberOfSeats` field |
| — Return auditorium | `auditoriumId` field |
| — Return movie | `movieTitle` field |
| — 10-min expiration | Configurable via settings |
| — No double booking | Checks unexpired reservations |
| — No sold seats | Checks paid tickets |
| — Contiguous seats | Same row, consecutive numbers |
| Buy / Confirm Seats | Two endpoints available |
| — Use GUID | From reserve response |
| — Only while reserved | Expiration check enforced |
| — No double purchase | Paid flag check |
| — No expired confirm | 10-minute window |
| GRPC fixed | API key added, endpoints corrected |
| HTTP fallback | Automatic |
| Redis cache | With fallback on failure |
| Performance tracking | All requests logged |
| Fix Provided API issues | API key, endpoint path, GRPC auth |

---

## Additional Features

A few things were added beyond the core requirements:

- **CORS support** — Allows the Swagger UI to function; uses `AllowAnyOrigin` policy
- **Get Reservation Details** — `GET /api/Reservations/{id}` shows reservation status, expiration, and seats
- **Get All Showtimes** — `GET /api/Showtimes` lists available showtimes
- **Get Showtime by ID** — `GET /api/Showtimes/{id}` returns detailed information
- **Get Movies** — `GET /api/Movies` (all, cached) and `GET /api/Movies/{id}` (specific)
- **Detailed error messages** — Validation errors return clear, actionable messages with appropriate HTTP status codes

---

## Testing Recommendations

Below are the key scenarios worth running through manually:

1. **Create a showtime** — Grab a valid movie ID from `GET /api/Movies`, then call `POST /api/Showtimes`
2. **Reserve contiguous seats** — Row 1, Seats 5, 6, 7 (should succeed)
3. **Test non-contiguous rejection** — Row 1, Seats 5 and 7 (should return 400)
4. **Test double-booking prevention** — Reserve seats, then try to reserve one of the same seats again (should fail)
5. **Confirm a reservation** — Use the GUID from step 2 (should succeed)
6. **Test double-payment prevention** — Use the same GUID again (should return "already confirmed")
7. **Test expiration** — Create a reservation, wait 11 minutes, then try to confirm (should return "expired")
8. **Test sold seat prevention** — Reserve and confirm seats, then try to reserve the same ones again (should return "already sold")
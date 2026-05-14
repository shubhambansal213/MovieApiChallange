# Quick Start Guide

## Prerequisites
- Docker Desktop installed and running
- .NET 10.0 SDK installed

## Step 1: Start Infrastructure

Open PowerShell in the project root directory and run:

```powershell
docker-compose up
```

This will start:
- Provided Movies API (HTTP: http://localhost:7172, HTTPS: https://localhost:7443)
- Redis cache (localhost:6379)

Wait until you see the containers running successfully.

## Step 2: Run the Application

Open a new PowerShell window and run:

```powershell
cd ApiApplication
dotnet run
```

The application will start on:
- HTTPS: https://localhost:7629
- HTTP: http://localhost:7628

## Step 3: Access Swagger UI

Open your browser and navigate to:
- https://localhost:7629 (Swagger UI at root)

You can test all endpoints directly from the Swagger interface.

## Step 4: Test the APIs

### Using Swagger UI
1. Navigate to https://localhost:7629
2. Try the endpoints in this order:
   - GET /api/movies (to see available movies)
   - POST /api/showtimes (create a showtime)
   - POST /api/reservations (reserve seats)
   - POST /api/tickets/confirm (buy the reserved seats)

### Using cURL
All sample cURL commands are provided in `cUrls.txt`

## Example Workflow

1. **Get available movies:**
   ```
   GET /api/movies
   ```

2. **Create a showtime:**
   ```
   POST /api/showtimes
   {
     "movieId": "tt1375666",
     "sessionDate": "2024-12-25T19:00:00",
     "auditoriumId": 1
   }
   ```

3. **Reserve contiguous seats:**
   ```
   POST /api/reservations
   {
     "showtimeId": 1,
     "seats": [
       {"row": 1, "seatNumber": 5},
       {"row": 1, "seatNumber": 6},
       {"row": 1, "seatNumber": 7}
     ]
   }
   ```
   
   **Note:** Returns a GUID reservation ID

4. **Confirm the reservation (within 10 minutes):**
   ```
   POST /api/tickets/confirm
   {
     "reservationId": "YOUR-GUID-FROM-STEP-3"
   }
   ```

## Important Notes

- **Seat Contiguity**: All reserved seats must be consecutive in the same row
- **Reservation Expiration**: Reservations expire after 10 minutes
- **Caching**: Movie data is cached in Redis for reliability
- **Fallback**: If Provided API fails, cached data is used
- **Performance Logging**: All request execution times are logged to console

## Validation Rules

 **Reserve Seats:**
- Seats must be contiguous (e.g., Row 1: Seats 5,6,7)
- Cannot reserve already reserved seats (within 10 min)
- Cannot reserve already sold seats

 **Confirm Payment:**
- Reservation must exist
- Must be within 10-minute window
- Cannot confirm already paid reservations

## Troubleshooting

**Issue**: Cannot connect to Redis
- **Solution**: Make sure `docker-compose up` is running

**Issue**: Cannot connect to Provided API
- **Solution**: Check docker containers are healthy. The app will fallback to cached data.

**Issue**: Certificate errors on HTTPS
- **Solution**: Use the `-k` flag with cURL or accept the certificate in Swagger UI

**Issue**: Build warnings about nullable reference types
- **Solution**: These are non-critical warnings and don't affect functionality

## Clean Up

When finished testing:

```powershell
docker-compose down
```

This stops and removes all containers.

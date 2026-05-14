### Feedback

1. **No Background Cleanup Job** - Expired reservations remain in database (validated at confirmation time). Production deployment should include a background job to clean up expired reservations.

2. **DateTime Usage** 
   - Fixed: Changed from `DateTime.Now` to `DateTime.UtcNow` for consistency
   - Ensures accurate timezone-independent expiration


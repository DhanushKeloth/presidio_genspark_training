namespace ShipmentTrackingAPI.DTOs.Auth
{
    /// <summary>
    /// Represents the final authentication payload returned to the client.
    /// Excludes refresh tokens to maintain a stateless architecture.
    /// </summary>
    public class LoginResponseDto
    {
        public string AccessToken { get; set; } = null!;
        
        /// <summary>
        /// Seconds until the JWT access token expires.
        /// </summary>
        public int ExpiresIn { get; set; }
        public int UserId {get;set;}
        public string FullName {get;set;}=null!;
        public string Role { get; set; } = null!;
    }
}

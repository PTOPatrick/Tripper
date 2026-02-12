namespace Tripper.Core.DTOs;

public record SignupRequest(string Username, string Email, string Password);
public record LoginRequest(string Email, string Password);
public record AuthResponse(string Token, Guid UserId, string Username);

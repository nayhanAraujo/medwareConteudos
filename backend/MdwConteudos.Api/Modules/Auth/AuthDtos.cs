namespace MdwConteudos.Api.Modules.Auth;

public record LoginRequest(string Usuario, string Senha);
public record ForgotPasswordRequest(string Identificacao, string NewPassword, string ConfirmPassword);
public record LoginResponse(bool Success, string? Token, object? User, string? Error);
public record SessionUser(int CodUsuario, string Nome, string Role);

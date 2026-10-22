namespace MdwConteudos.Api.Modules.Users;

public record UserListItem(int CodUsuario, string Nome, string Identificacao, string Perfil, int Status, string? Email = null);
public record UserStatusRequest(int? Status);
public record CreateUserRequest(string Nome, string Identificacao, string Senha, string ConfirmarSenha, string Perfil, int Status, string? Email = null);
public record UpdateUserRequest(string Nome, string Identificacao, string? Senha, string? ConfirmarSenha, string Perfil, int Status, string? Email = null);

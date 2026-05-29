namespace MdwConteudos.Api.Modules.Users;

public record UserListItem(int CodUsuario, string Nome, string Identificacao, string Perfil, int Status);
public record CreateUserRequest(string Nome, string Identificacao, string Senha, string ConfirmarSenha, string Perfil, int Status);
public record UpdateUserRequest(string Nome, string Identificacao, string? Senha, string? ConfirmarSenha, string Perfil, int Status);

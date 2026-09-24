namespace MdwConteudos.Api.Modules.Users;

public static class AccountSecurity
{
    public static bool IsAdmin(string? role) => string.Equals(role?.Trim(), "admin", StringComparison.OrdinalIgnoreCase);

    public static string? Validate(string? nome, string? login, string? perfil, string? password, string? confirmation, bool creating)
    {
        if (string.IsNullOrWhiteSpace(nome) || string.IsNullOrWhiteSpace(login) || login.Any(char.IsWhiteSpace))
            return "Nome e login válidos são obrigatórios; login não pode conter espaços.";
        if (perfil?.Trim().ToLowerInvariant() is not ("admin" or "usuario" or "comum"))
            return "Perfil inválido.";
        if (creating || !string.IsNullOrEmpty(password) || !string.IsNullOrEmpty(confirmation))
        {
            if (string.IsNullOrWhiteSpace(password) || password.Length < 6)
                return "Senha deve ter no mínimo 6 caracteres.";
            if (password != confirmation) return "As senhas não coincidem.";
        }
        return null;
    }
}

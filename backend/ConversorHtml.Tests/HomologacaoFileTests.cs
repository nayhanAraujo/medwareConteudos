using MdwConteudos.Api.Infrastructure;

namespace ConversorHtml.Tests;

public sealed class HomologacaoFileTests
{
    [Fact]
    public void Persisted_paths_resolve_only_to_independent_copied_files()
    {
        var root = Path.Combine(Path.GetTempPath(), "mdw-copy-path-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "static", "uploads"));
        var copy = Path.Combine(root, "static", "uploads", "teste.pdf");
        try
        {
            File.WriteAllBytes(copy, [1, 2, 3]);
            foreach (var value in new[] { "/static/uploads/teste.pdf", "static/uploads/teste.pdf", "uploads/teste.pdf", "teste.pdf", copy, "C:/original/static/uploads/teste.pdf" })
                Assert.Equal(copy, HomologacaoGuard.ResolveCopiedFile(root, value));
            foreach (var value in new[] { "C:/original/private.pdf", "../outside.pdf", "static/../../outside.pdf", "//server/share/teste.pdf", "https://example.com/teste.pdf", "/static/uploads/missing.pdf" })
                Assert.Null(HomologacaoGuard.ResolveCopiedFile(root, value));
        }
        finally
        {
            File.Delete(copy);
            Directory.Delete(Path.Combine(root, "static", "uploads"));
            Directory.Delete(Path.Combine(root, "static"));
            Directory.Delete(root);
        }
    }
}

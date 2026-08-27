using BA.Dmo.IntegrationTests.Access;
using Microsoft.AspNetCore.Hosting;

var port = args.Length > 0 && int.TryParse(args[0], out var requestedPort)
    ? requestedPort
    : 5052;
var profile = args.Length > 1 ? args[1].ToLowerInvariant() switch
{
    "boquilhas" => ShellRoutingTests.ShellFixture.UserProfile.BoquilhasOnly,
    "jobon" => ShellRoutingTests.ShellFixture.UserProfile.JobOnResponsible,
    "peso" => ShellRoutingTests.ShellFixture.UserProfile.PesoOperador,
    "peso-responsavel" => ShellRoutingTests.ShellFixture.UserProfile.PesoResponsavel,
    "armazem-create" => ShellRoutingTests.ShellFixture.UserProfile.ArmazemWithFerramentas,
    "reparacao-interna" => ShellRoutingTests.ShellFixture.UserProfile.ReparacaoInternaOnly,
    "tampoes" => ShellRoutingTests.ShellFixture.UserProfile.TampoesOnly,
    _ => ShellRoutingTests.ShellFixture.UserProfile.ArmazemOnly
} : ShellRoutingTests.ShellFixture.UserProfile.ArmazemOnly;

using var baseFactory = new ShellRoutingTests.ShellFixture
{
    Profile = profile
};
using var factory = baseFactory.WithWebHostBuilder(builder =>
{
    builder.UseContentRoot(Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory,
        "..", "..", "..", "..", "..", "..", "..",
        "src", "BA.Dmo.Web")));
    builder.UseUrls($"http://localhost:{port}");
});

factory.UseKestrel(port);
factory.StartServer();

Console.WriteLine($"BA DMO visual verification host listening at http://localhost:{port}");
Console.WriteLine("Use any email/password on the test-only login page.");

await Task.Delay(Timeout.Infinite);

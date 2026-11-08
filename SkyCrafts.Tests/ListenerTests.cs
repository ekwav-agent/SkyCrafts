using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Coflnet.Sky.Crafts.Tests;

public class ListenerTests
{
    [Fact]
    public void DockerfileBindsHttpOnPort8000()
    {
        var dockerfile = File.ReadAllText(Path.Combine(FindRepoRoot(), "Dockerfile"));

        Assert.Contains("ENV ASPNETCORE_URLS=http://+:8000", dockerfile);
        Assert.DoesNotContain("8443", dockerfile);
    }

    [Fact]
    public async Task HttpsSectionDoesNotAddHttpsListener()
    {
        var settings = new Dictionary<string, string?>
        {
            ["urls"] = "http://127.0.0.1:0",
            ["HTTPS:TLS:CA_CERT_PATH"] = "/missing/ca.pem",
            ["HTTPS:TLS:CERT_PATH"] = "/missing/cert.pem",
            ["HTTPS:TLS:KEY_PATH"] = "/missing/key.pem"
        };
        using var host = Program.CreateHostBuilder([])
            .ConfigureHostConfiguration(config => config.AddInMemoryCollection(settings))
            .ConfigureServices(RemoveBackgroundServices)
            .Build();

        await host.StartAsync();
        var addresses = host.Services.GetRequiredService<IServer>()
            .Features.Get<IServerAddressesFeature>()!.Addresses.ToList();
        await host.StopAsync();

        Assert.NotEmpty(addresses);
        Assert.All(addresses, a => Assert.StartsWith("http://", a));
        Assert.DoesNotContain(addresses, a => a.Contains(":8443"));
    }

    private static void RemoveBackgroundServices(IServiceCollection services)
    {
        var own = services.Where(s => s.ServiceType == typeof(IHostedService)
            && s.ImplementationType?.Namespace?.StartsWith("Coflnet.Sky.Crafts") == true
            || s.ServiceType == typeof(IHostedService) && s.ImplementationFactory != null).ToList();
        foreach (var descriptor in own)
            services.Remove(descriptor);
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(System.AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Dockerfile")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new FileNotFoundException("Dockerfile not found above test output");
    }
}

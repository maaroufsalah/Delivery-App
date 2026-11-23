using Delivery.Api;
using Serilog;

Log.Logger = new LoggerConfiguration().WriteTo.Console(new Serilog.Formatting.Compact.CompactJsonFormatter()).CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);
    builder.AddDeliveryApi();
    var app = builder.Build();
    app.UseDeliveryApi();
    app.Run();
}
catch (Exception exception) when (exception is not HostAbortedException)
{
    Log.Fatal(exception, "Arrêt de l'API sur une erreur");
    throw;
}
finally
{
    Log.CloseAndFlush();
}

public partial class Program;

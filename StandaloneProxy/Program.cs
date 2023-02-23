using Yarp.ReverseProxy.Forwarder;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpClient();
builder.Services.AddHttpForwarder();

var app = builder.Build();

app.UseRouting();

app.Map("{**catch-all}", async (HttpContext httpContext, IHttpForwarder httpForwarder, IConfiguration configuration, IHttpMessageHandlerFactory httpMessageHandlerFactory) =>
{
    static void HandleError(HttpContext httpContext, ForwarderError forwarderError)
    {
        var exception = httpContext.GetForwarderErrorFeature()?.Exception;
        var loggerFactory = httpContext.RequestServices.GetRequiredService<ILoggerFactory>();
        var logger = loggerFactory.CreateLogger("StandaloneProxy");

        logger.LogWarning(exception, "Error while forwarding request: {Error}.", forwarderError);
    }

    var destinationPrefix = configuration.GetValue<string>("DestinationPrefix")!;
    using var httpMessageHandler = httpMessageHandlerFactory.CreateHandler();
    using var httpMessageInvoker = new HttpMessageInvoker(httpMessageHandler, disposeHandler: false);

    var forwarderError = await httpForwarder
        .SendAsync(httpContext, destinationPrefix, httpMessageInvoker, ForwarderRequestConfig.Empty, HttpTransformer.Default)
        .ConfigureAwait(false);

    if (forwarderError is not ForwarderError.None)
    {
        HandleError(httpContext, forwarderError);
    }
});

app.Run();
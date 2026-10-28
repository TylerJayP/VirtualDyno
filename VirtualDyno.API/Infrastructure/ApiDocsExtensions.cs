namespace VirtualDyno.API.Infrastructure;

public static class ApiDocsExtensions
{
    public static IServiceCollection AddApiDocs(this IServiceCollection services) => services.AddOpenApi();

    /// <summary>OpenAPI document at /openapi/v1.json and Swagger UI at /swagger.</summary>
    public static WebApplication MapApiDocs(this WebApplication app)
    {
        app.MapOpenApi();
        app.UseSwaggerUI(o =>
        {
            o.SwaggerEndpoint("/openapi/v1.json", "Virtual Dyno API");
            o.RoutePrefix = "swagger";
        });
        return app;
    }
}

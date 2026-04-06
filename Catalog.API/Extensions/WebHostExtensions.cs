namespace Catalog.API.Extensions
{
    public static class WebHostExtensions
    {
        public static bool IsTesting(this IWebHostEnvironment environment) =>
           environment.EnvironmentName == "Testing";

        public static bool IsIntegration(this IWebHostEnvironment environment) =>
           environment.EnvironmentName == "Integration";
    }
}

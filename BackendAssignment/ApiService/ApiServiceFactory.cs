using CleoAssignment.ApiService.Intrefaces;

namespace CleoAssignment.ApiService;

public static class ApiServiceFactory
{
    public static IApiService<T> CreateApiService<T>(ThrottleSettings throttleSettings,
                                                     IResourceProvider<T> resourceProvider,
                                                     ITimeProvider timeProvider)
    {
        return ApiService<T>.GetInstance().Configure(resourceProvider, throttleSettings, timeProvider);
    }
}

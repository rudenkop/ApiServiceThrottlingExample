using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using CleoAssignment.ApiService.Exceptions;
using CleoAssignment.ApiService.Intrefaces;

namespace CleoAssignment.ApiService
{
    public class ApiService<T> : IApiService<T>
    {
        private static IResourceProvider<T> _resourceProvider;
        private static ITimeProvider _timeProvider;
        private static ThrottleSettings _throttleSettings;
        private static readonly ConcurrentDictionary<string, SemaphoreSlim> _resourceLocks = new();
        private static readonly ConcurrentDictionary<string, Lazy<Task<T>>> _resourceCache = new();
        private static readonly SemaphoreSlim _throttleSemaphore = new(1, 1);
        private static readonly ConcurrentDictionary<string, (int requestCount, DateTime lastRequestTime, DateTime? banStartTime)> _ipRequestTracking = new();

        #region Singleton 
        private static readonly ApiService<T> _instance = new ApiService<T>();
        static ApiService()
        {
        }
        private ApiService()
        {
        }
        public static ApiService<T> GetInstance()
        {
            return _instance;
        }
        #endregion

        public ApiService<T> Configure(IResourceProvider<T> resourceProvider, ThrottleSettings throttleSettings, ITimeProvider timeProvider)
        {
            _resourceProvider = resourceProvider ?? throw new ArgumentNullException(nameof(resourceProvider));
            _throttleSettings = throttleSettings ?? throw new ArgumentNullException(nameof(throttleSettings));
            _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
            return _instance;
        }

        public async Task<GetResponse<T>> GetResource(GetRequest request)
        {
            try
            {

                await ThrottleAsync(request.IpAddress);

                if (!_resourceCache.ContainsKey(request.ResourceId))
                {
                    var getresourceTask = new Lazy<Task<T>>(() => Task<T>.Run(()=>_resourceProvider.GetResource(request.ResourceId)));
                    _resourceCache[request.ResourceId] = getresourceTask;
                }

                return await Task<T>.FromResult(new GetResponse<T>(true, _resourceCache[request.ResourceId].Value.Result, null));
            }
            catch (BannedIpException be)
            {
                return await Task<T>.FromResult(new GetResponse<T>(false, default, ErrorType.BannedIpError));
            }
            catch (RequestLimitExceededException be)
            {
                return await Task<T>.FromResult(new GetResponse<T>(false, default, ErrorType.RequestLimitExceededError));
            }
            catch (Exception ex)
            {
                return await Task<T>.FromResult(new GetResponse<T>(false, default, ErrorType.GetResourceError));
            }
        }

        public async Task<AddOrUpdateResponse> AddOrUpdateResource(AddOrUpdateRequest<T> request)
        {

            SemaphoreSlim resourceLock = null;
            try 
            { 
                await ThrottleAsync(request.IpAddress);

                resourceLock = _resourceLocks.GetOrAdd(request.ResourceId, new SemaphoreSlim(1, 1));
                await resourceLock.WaitAsync();

                _resourceProvider.AddOrUpdateResource(request.ResourceId, request.Resource);
                _resourceCache[request.ResourceId] = new Lazy<Task<T>>(() => Task<T>.FromResult(request.Resource));

                return await Task<T>.FromResult(new AddOrUpdateResponse(true, null));
            }
            catch (BannedIpException be)
            {
                return await Task<T>.FromResult(new AddOrUpdateResponse(false, ErrorType.BannedIpError));
            }
            catch (RequestLimitExceededException be)
            {
                return await Task<T>.FromResult(new AddOrUpdateResponse(false, ErrorType.RequestLimitExceededError));
            }
            catch(Exception ex)
            {
                return await Task<T>.FromResult(new AddOrUpdateResponse(false, ErrorType.AddOrUpdateResourceError));
            }
            finally
            {
                resourceLock?.Release();
            }
        }

        private async Task ThrottleAsync(string ip)
        {
            await _throttleSemaphore.WaitAsync();

            try
            {
               
                var (requestCount, lastRequestTime, banStartTime) = _ipRequestTracking.GetOrAdd(ip, _ => (0, _throttleSettings.IntervalRootUtc, null));

                bool isIpBanned = banStartTime.HasValue && _timeProvider.UtcNow < banStartTime.Value.Add(_throttleSettings.BanTimeOut);
                if (isIpBanned)
                {
                    throw new BannedIpException(ip);
                }

                bool isTthrottleIntrevalPassed = _timeProvider.UtcNow - lastRequestTime >= _throttleSettings.ThrottleInterval;
                if (isTthrottleIntrevalPassed)
                {
                    requestCount = 0;
                    lastRequestTime = _timeProvider.UtcNow;
                }

                bool isRequestLimitExceeded = requestCount >= _throttleSettings.MaxRequestsPerIp;
                if (isRequestLimitExceeded)
                {
                    throw new RequestLimitExceededException(ip);
                }

                _ipRequestTracking[ip] = (requestCount + 1, lastRequestTime, null);
            }
            finally
            {
                _throttleSemaphore.Release();
            }
        }
    }
}


using System;
using System.Threading;
using System.Threading.Tasks;
using Infrastructure.Interface;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Services
{
    public class MemoryCacheService : ICacheService
    {
        private readonly ReaderWriterLockSlim _rwLock = new ReaderWriterLockSlim();
        private readonly SemaphoreSlim _semaphoreSlim = new SemaphoreSlim(1);
        private readonly IMemoryCache _cache;
        private readonly ILogger<MemoryCacheService> _logger;

        public MemoryCacheService(IMemoryCache cache, ILogger<MemoryCacheService> logger)
        {
            _cache = cache;
            _logger = logger;
        }
        
        public object GetValue(string key)
        {
            _cache.TryGetValue(key, out var value);
             return value;
        }
        public void SetValue(object key, object value, TimeSpan absoluteExpirationRelativeToNow)
        {
            _rwLock.EnterWriteLock();
            try
            {
                var entry = _cache.CreateEntry(key);
                entry.AbsoluteExpirationRelativeToNow = absoluteExpirationRelativeToNow;
                entry.Value = value;
                entry.Dispose();
            }
            finally
            {
                _rwLock.ExitWriteLock();   
            } 
        }

        public async Task<object> GetOrAdd(object key, Func<Task<object>> factory)
        {
            if (_cache.TryGetValue(key, out var obj) 
                && obj != null)
            {
                return  obj;
            }
            
            await _semaphoreSlim.WaitAsync();

            try
            {
                if (_cache.TryGetValue(key, out obj))
                {
                    return obj;
                }
              
                obj = await factory();
                
                if (obj == null)
                {
                    return null;
                }
                
                var entry = _cache.CreateEntry(key);
                entry.SetValue(obj);
                entry.Dispose();
            }
            catch (Exception e)
            {
                _logger.LogError("GetOrAdd: " + e.Message);
            }
            finally
            {
                _semaphoreSlim.Release();
            }
            
            return obj;
        }

        public void Remove(string key)
        {
            if (!_cache.TryGetValue(key, out _))
            {
                return;
            }
            
            _rwLock.EnterWriteLock();
            try
            {
                _cache.Remove(key);
            }
            finally
            {
                _rwLock.ExitWriteLock();   
            }
        }
    }
}
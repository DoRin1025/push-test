using System;
using System.Threading.Tasks;
using ApplicationCore.Interfaces;
using Microsoft.Extensions.Caching.Memory;
 
namespace Infrastructure.Cache
{
    public class MemoryCachedResources : ICachedResources
    {
        private readonly IMemoryCache _memoryCache;

        public MemoryCachedResources(IMemoryCache memoryCache)
        {
            _memoryCache = memoryCache;
        }
        public async Task<object> GetOrCreateAsync(string key, int expiration, Func<Task<object>> factory)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return null;
            }

            if (_memoryCache.TryGetValue(key, out var obj) && obj != null)
            {
                return  obj;
            }
            
            obj = await factory();
            await PutValue(key, obj, expiration);
            return  obj;
        }

        public async Task<object> GetValueAsync(string key)
        {
            return await GetValue(key);
        }

        public Task SetValueAsync(string key, object value, int expiration)
        {
            var entry = _memoryCache.CreateEntry(key);
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(expiration);
            entry.SetValue(value);
            entry.Dispose();
            
            return Task.CompletedTask;
        }

        public Task RemoveAsync(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return Task.CompletedTask;
            }

            if (_memoryCache.TryGetValue(key, out _))
            {
                _memoryCache.Remove(key);
            }
            return Task.CompletedTask;
        }

        public async Task UpdateAsync(string key, object directoryRootResource,
            int expiration = 180)
        {
            await PutValue(key, directoryRootResource, expiration);
        }
        
        private Task PutValue(string key, object value, int expiration)
        {
            var entry = _memoryCache.CreateEntry(key);
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(expiration);
            entry.SetValue(value);
            entry.Dispose();
            return Task.CompletedTask;
        }
        private async Task<object> GetValue(string key)
        {
            return await Task.FromResult<object>(_memoryCache.Get(key));
        }
    }
}
using System;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using System.Threading.Tasks;
using ApplicationCore.Interfaces;
using Microsoft.Extensions.Caching.Distributed;
 

namespace Infrastructure.Cache
{
    public class RedisCachedResources : ICachedResources
    {
        private readonly IDistributedCache _memoryCache;
        
        public RedisCachedResources(IDistributedCache distributedCache)
        {
            _memoryCache = distributedCache;
        }
        
        public async Task<object> GetOrCreateAsync(string key, int expiration, Func<Task<object>> factory)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return null;
            }
            
            var dir = await GetValueAsync(key);
            
            if (dir != null)
            {
                return  dir;
            }
            
            dir  = await factory();
            await SetRedisValueAsync(key, dir, expiration);
            return  dir;
        }
        public async Task RemoveAsync(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return;
            } 
            await _memoryCache.RemoveAsync(key);
        }
        
        public async Task UpdateAsync(string key, object directoryRootResource, int expiration)
        {
            await _memoryCache.SetAsync(key,Serialize(directoryRootResource), new DistributedCacheEntryOptions()
            {
                AbsoluteExpirationRelativeToNow =  TimeSpan.FromMinutes(expiration)
            });
        }
        public async Task<object> GetValueAsync(string key)
        {
            return string.IsNullOrWhiteSpace(key)
                ? null : await GetRedisValueAsync(key);
        }

        public async Task SetValueAsync(string key, object value, int expiration)
        { 
            await SetRedisValueAsync(key, value, expiration);
        }
        
        private async Task<object> GetRedisValueAsync(string key)
        {
            var result = await _memoryCache.GetAsync(key);
            return result == null ? null : Deserialize<object>(result);
        }
        
        private async Task SetRedisValueAsync(string key, object obj, int expiration)
        {
            var byteDir = Serialize(obj);
            await _memoryCache.SetAsync(key, byteDir, new DistributedCacheEntryOptions()
            {
                AbsoluteExpirationRelativeToNow =  TimeSpan.FromMinutes(expiration)
            });
        }
        
        private byte[] Serialize( object obj )  
        {  
            if ( obj == null )  
            {  
                return null;  
            }  
            var objBinaryFormatter = new BinaryFormatter();  
            using ( var objMemoryStream = new MemoryStream() )  
            {  
                objBinaryFormatter.Serialize( objMemoryStream, obj );  
                var objDataAsByte = objMemoryStream.ToArray();  
                return objDataAsByte;  
            }  
        }
        private T Deserialize<T>( byte[] bytes )  
        {  
            var objBinaryFormatter = new BinaryFormatter();  
            if ( bytes == null )  
                return default( T );

            using (var objMemoryStream = new MemoryStream(bytes))
            {
                var result = (T) objBinaryFormatter.Deserialize(objMemoryStream);
                return result;
            }
        }  
    }
}
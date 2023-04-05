using System;
using System.Threading.Tasks;


namespace ApplicationCore.Interfaces
{
    public interface ICachedResources
    {
        Task<object> GetOrCreateAsync(string key, int expiration, Func<Task<object>> factory);
        Task<object> GetValueAsync(string key);
        Task SetValueAsync(string key, object value, int expiration);
        Task RemoveAsync(string key);
        Task UpdateAsync(string key, object directoryRootResource, int expiration = 180);
    }
}
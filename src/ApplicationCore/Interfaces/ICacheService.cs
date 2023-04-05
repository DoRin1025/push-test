using System;
using System.Threading.Tasks;

namespace Infrastructure.Interface
{
    public interface ICacheService
    {
        object GetValue(string key);
        void SetValue(object key, object value, TimeSpan absoluteExpirationRelativeToNow);
        Task<object> GetOrAdd(object key, Func<Task<object>> factory);
        void Remove(string key);
    }
}
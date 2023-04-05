using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using Model;
using Model.PWA;

namespace ApplicationCore.Interfaces
{
    /// <summary>
    /// Summary description for IRepository
    /// </summary>
    public interface IRepository
    {
        #region APN

        Task<Dictionary<string, int>> APNSRegister(ConcurrentQueue<DeviceRegistration> registrationsQueue);

        Task<List<string>> GetAPNSRegistrations(Message message);

        Task<int> RemoveApnDeviceRegistrationsAsync(string publisherId, string userName, string appId,
            IEnumerable<string> deviceTokens);

        Task<List<string>> GetAPNSTopicSubscriptions(Topic topic);
        Task<Dictionary<string, object>> SetAPNSTopicSubscriptions(Topic topic);

        #endregion


        bool ExecuteNonQueryWithParams(string queryString, string[] parameters);

        string BuildStringOfQueryWithParams(string query, string[] qparams);

        Dictionary<String, object> GetTopics(string topics, string publisherId, string username, string appId,
            string type,
            string deviceId);

        List<string> ReadColumn(string query, string[] qparams);
        Dictionary<string, string> ReadColumns(string query, string[] qparams, string[] dictkeys);

        Dictionary<string, int> DeleteInvalidRegistrations(ConcurrentQueue<string> invalidRegistrationsQueue,
            int regDeleteBatch, string type);


        #region GCM

        Dictionary<string, int> GCMRegister(ConcurrentQueue<DeviceRegistration> registrationsQueue);

        long DeleteBlackListedDevices(Message message);

        List<string> GetGCMRegistrations(Message message, string[] version);

        # endregion

        #region Pwa

        Dictionary<string, int> PwaRegister(ConcurrentQueue<PwaDeviceRegistration> registrationsQueue);

        List<PwaDeviceRegistration> GetPwaRegistrations(Message message);

        Dictionary<string, object> InsertPwaTopics(string topics, string publisherId, string username, string appId,
            string type,
            string deviceId);

        #endregion
    }
}
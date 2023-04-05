using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using ApplicationCore.Interfaces;
using Microsoft.Extensions.Logging;
using Model;
using Model.PWA;
using Npgsql;
using NpgsqlTypes;

namespace Infrastructure
{
    public class DatabaseRepository : IRepository
    {
        private readonly string _connString;
        private readonly ILogger<DatabaseRepository> _logger;

        public DatabaseRepository(string connString, ILogger<DatabaseRepository> logger)
        {
            _connString = connString;
            _logger = logger;
        }

        #region APNS

        public async Task<List<string>> GetAPNSTopicSubscriptions(Topic topic)
        {
            var topics = new List<string>();
            var cmdString = "SELECT topic_id "
                            +" FROM apns_topic_subscriptions "
                            +" WHERE publisher_id=@publisher_id " 
                            +" AND username=@username " 
                            +" AND app_id=@app_id " 
                            +" AND topic_type=@topic_type " 
                            +" AND device_id=@device_id ";
            try
            {
                await using var conn = new NpgsqlConnection(_connString);
                await conn.OpenAsync();

                await using var cmd = new NpgsqlCommand(cmdString, conn);
                cmd.Parameters.AddWithValue("@publisher_id", topic.PublisherId);
                cmd.Parameters.AddWithValue("@username", topic.AppOwnerUsername);
                cmd.Parameters.AddWithValue("@app_id", topic.AppId);
                cmd.Parameters.AddWithValue("@topic_type", topic.Type);
                cmd.Parameters.AddWithValue("@device_id", topic.DeviceId);

                await using var rdr = await cmd.ExecuteReaderAsync();

                while (await rdr.ReadAsync())
                {
                    var token = rdr.GetString(0);
                    topics.Add(token);
                }
            }
            catch (Exception e)
            {
                _logger.LogError("Error GetAPNSTopicSubscriptions: " + e);
            }

            return topics;
        }

        public async Task<Dictionary<string, object>> SetAPNSTopicSubscriptions(Topic topic)
        {
            var topicIds = topic.Topics.Split(new[] {","}, StringSplitOptions.RemoveEmptyEntries);
            var inserted = 0;
            var cmdString = "INSERT INTO"
                            + " apns_topic_subscriptions (publisher_id, username, app_id, topic_type, topic_id, device_id) "
                            + " VALUES(@publisher_id, @username, @app_id, @topic_type, @topic_id, @device_id)";
            try
            {
                await using var conn = new NpgsqlConnection(_connString);
                await conn.OpenAsync();
                await DeleteAllTopics(topic, conn);

                foreach (var topicId in topicIds)
                {
                    await using var cmd = new NpgsqlCommand(cmdString, conn);
                    {
                        cmd.Parameters.AddWithValue("@publisher_id", topic.PublisherId);
                        cmd.Parameters.AddWithValue("@username", topic.AppOwnerUsername);
                        cmd.Parameters.AddWithValue("@app_id", topic.AppId);
                        cmd.Parameters.AddWithValue("@topic_type", topic.Type);
                        cmd.Parameters.AddWithValue("@topic_id", topicId);
                        cmd.Parameters.AddWithValue("@device_id", topic.DeviceId);

                        inserted += cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception e)
            {
                _logger.LogError("Error SetAPNSTopicSubscriptions: " + e);
            }

            return new Dictionary<string, object>()
            {
                {"topicIds", topicIds},
                {"inserted", inserted}
            };
        }

        public async Task<int> RemoveApnDeviceRegistrationsAsync(string publisherId, string userName, string appId,
            IEnumerable<string> deviceTokens)
        {
            var count = 0;
            try
            {
                await using var conn = new NpgsqlConnection(_connString);
                await conn.OpenAsync();

                var cmdString = "DELETE FROM"
                                + " apns_registrations WHERE publisher_id=@publisher_id " +
                                " AND username=@username " +
                                " AND app_id=@app_id " +
                                " AND device_token=@device_token";

                foreach (var token in deviceTokens)
                {
                    await using (var cmd = new NpgsqlCommand(cmdString, conn))
                    {
                        cmd.Parameters.Add("@publisher_id", NpgsqlDbType.Varchar, 50).Value = publisherId;
                        cmd.Parameters.Add("@username", NpgsqlDbType.Varchar, 50).Value = userName;
                        cmd.Parameters.Add("@app_id", NpgsqlDbType.Varchar, 50).Value = appId;
                        cmd.Parameters.Add("@device_token", NpgsqlDbType.Varchar, 300).Value = token;

                        await cmd.ExecuteNonQueryAsync();
                    }

                    count++;
                }
            }
            catch (Exception e)
            {
                _logger.LogError("Error RemoveApnDeviceRegistrationsAsync: " + e);
            }

            return count;
        }

        public async Task<List<string>> GetAPNSRegistrations(Message message)
        {
            var registrations = new HashSet<string>();

            try
            {
                await using var conn = new NpgsqlConnection(_connString);
                await conn.OpenAsync();

                var topics = message.Topics ?? "";
                var topicIds = topics.Split(new char[] {','}, StringSplitOptions.RemoveEmptyEntries);

                NpgsqlCommand cmd;

                if (topicIds.Length > 0 && message.Type != null)
                {
                    var topicType = message.Type;
                    if (message.Module != null)
                        topicType = message.Module + "." + topicType;

                    // TODO test and optimize this SQL request for performance (use JOIN as an alternative)
                    var cmdString = "SELECT D.device_token "
                                    + " FROM apns_registrations AS D "
                                    + " WHERE D.publisher_id = @publisher_id "
                                    + " AND D.username = @username"
                                    + " AND D.app_id = @app_id "
                                    + " AND D.device_token IS NOT NULL "
                                    + " AND D.device_id in ("
                                    + " SELECT S.device_id "
                                    + " FROM apns_topic_subscriptions AS S "
                                    + " WHERE S.publisher_id = @publisher_id "
                                    + " AND S.username = @username "
                                    + " AND S.app_id = @app_id "
                                    + " AND S.topic_type = @type "
                                    + " AND S.topic_id in ("
                                    + " @topic0";

                    for (var i = 1; i < topicIds.Length; i++)
                    {
                        cmdString += ", @topic" + i;
                    }

                    cmdString += ")";

                    if (message.DeviceIds != null) // Send to selected app users' devices
                    {
                        cmdString += " AND S.device_id in (" + message.GetDeviceIdsAsSqlListString() + ")";
                    }

                    cmdString += ")";

                    cmd = new NpgsqlCommand(cmdString, conn);

                    cmd.Parameters.Add("@publisher_id", NpgsqlDbType.Varchar, 50).Value = message.PublisherId;
                    cmd.Parameters.Add("@username", NpgsqlDbType.Varchar, 50).Value = message.AppOwnerUsername;
                    cmd.Parameters.Add("@app_id", NpgsqlDbType.Varchar, 50).Value = message.AppId;
                    cmd.Parameters.Add("@type", NpgsqlDbType.Varchar, 50).Value = topicType;

                    for (int i = 0; i < topicIds.Length; i++)
                    {
                        cmd.Parameters.Add("@topic" + i, NpgsqlDbType.Varchar, 50).Value = topicIds[i];
                    }
                }
                else
                {
                    var cmdString = "SELECT device_token"
                                    + " FROM apns_registrations "
                                    + " WHERE publisher_id=@publisher_id "
                                    + " AND username=@username "
                                    + " AND app_id=@app_id "
                                    + " AND device_token IS NOT NULL";

                    if (message.DeviceIds != null) // Send to selected app users' devices
                    {
                        cmdString += " AND device_id in (" + message.GetDeviceIdsAsSqlListString() + ")";
                    }

                    cmd = new NpgsqlCommand(cmdString, conn);
                    cmd.Parameters.Add("@publisher_id", NpgsqlDbType.Varchar, 50).Value = message.PublisherId;
                    cmd.Parameters.Add("@username", NpgsqlDbType.Varchar, 50).Value = message.AppOwnerUsername;
                    cmd.Parameters.Add("@app_id", NpgsqlDbType.Varchar, 50).Value = message.AppId;
                }

                await using var rdr = await cmd.ExecuteReaderAsync();
                while (await rdr.ReadAsync())
                {
                    var token = rdr.GetString(0);
                    registrations.Add(token);
                }
            }
            catch (Exception e)
            {
                _logger.LogError("Error Get APN registration: " + e);
            }

            return new List<string>(registrations);
        }

        public async Task<Dictionary<string, int>> APNSRegister(ConcurrentQueue<DeviceRegistration> registrationsQueue)
        {
            var success = 0;
            var fail = 0;
            var deviceRegistrationsProcessed = 0;

            try
            {
                await using var conn = new NpgsqlConnection(_connString);
                await conn.OpenAsync();

                while (registrationsQueue.TryDequeue(out var registration))
                {
                    try
                    {
                        var itemExist = await CheckIfExit(registration, conn);
                        var rowsAffected = 0;

                        if (itemExist)
                        {
                            rowsAffected = await UpdateToken(registration, conn);
                        }
                        else
                        {
                            rowsAffected = await InsertToken(registration, conn);
                        }

                        if (rowsAffected > 0)
                            success++;
                        else
                            fail++;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError("Eror persisting APN registration: " + ex);
                        fail++;
                    }

                    deviceRegistrationsProcessed++;
                }
            }
            catch (Exception e)
            {
                _logger.LogError("Error persisting APN registration: " + e);
            }

            return new Dictionary<string, int>
            {
                {"success", success},
                {"fail", fail},
                {"deviceRegistrationsProcessed", deviceRegistrationsProcessed},
            };
        }

        private async Task<bool> CheckIfExit(DeviceRegistration registration,
            NpgsqlConnection connection)
        {
            await using var cmd =
                new NpgsqlCommand(
                    "SELECT device_id"
                    + " FROM apns_registrations  "
                    + " WHERE device_id = (@deviceId) "
                    + " AND username = (@appOwnerUsername) "
                    + " AND app_id = (@appId) "
                    + " AND publisher_id = (@publisherId)",
                    connection);

            cmd.Parameters.AddWithValue("@deviceId", registration.DeviceId);
            cmd.Parameters.AddWithValue("@publisherId", registration.PublisherId);
            cmd.Parameters.AddWithValue("@appOwnerUsername", registration.AppOwnerUsername);
            cmd.Parameters.AddWithValue("@appId", registration.AppId);

            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                if (reader.HasRows)
                {
                    return true;
                }
            }

            return false;
        }

        private async Task<int> InsertToken(DeviceRegistration registration,
            NpgsqlConnection connection)
        {
            await using (var cmd =
                new NpgsqlCommand(
                    "INSERT INTO"
                    + " apns_registrations "
                    + " (device_id, device_token, install_date, last_open_date, "
                    + "publisher_id, username, app_id) "
                    + "VALUES (@deviceId, @deviceToken, CURRENT_TIMESTAMP(3), CURRENT_TIMESTAMP(3),"
                    + " @publisherId, @appOwnerUsername, @appId)",
                    connection))
            {
                cmd.Parameters.AddWithValue("@deviceToken", registration.RegistrationId);
                cmd.Parameters.AddWithValue("@deviceId", registration.DeviceId);
                cmd.Parameters.AddWithValue("@publisherId", registration.PublisherId);
                cmd.Parameters.AddWithValue("@appOwnerUsername", registration.AppOwnerUsername);
                cmd.Parameters.AddWithValue("@appId", registration.AppId);

                return await cmd.ExecuteNonQueryAsync();
            }
        }

        private async Task<int> UpdateToken(
            DeviceRegistration registration,
            NpgsqlConnection connection)
        {
            await using var cmd =
                new NpgsqlCommand(
                    "UPDATE apns_registrations"
                    + " SET device_token = (@deviceToken), last_open_date = CURRENT_TIMESTAMP(3) "
                    + " WHERE device_id = (@deviceId) "
                    + " AND username =  (@appOwnerUsername) "
                    + " AND app_id = (@appId) "
                    + " AND publisher_id = (@publisherId)",
                    connection);

            cmd.Parameters.AddWithValue("@deviceToken", registration.RegistrationId);
            cmd.Parameters.AddWithValue("@deviceId", registration.DeviceId);
            cmd.Parameters.AddWithValue("@publisherId", registration.PublisherId);
            cmd.Parameters.AddWithValue("@appOwnerUsername", registration.AppOwnerUsername);
            cmd.Parameters.AddWithValue("@appId", registration.AppId);

            return await cmd.ExecuteNonQueryAsync();
        }

        private async Task DeleteAllTopics(Topic topic, NpgsqlConnection conn)
        {
            try
            {
                var cmdString = "DELETE FROM" +
                                " apns_topic_subscriptions " +
                                " WHERE publisher_id=@publisher_id " +
                                " AND username=@username " +
                                " AND app_id=@app_id " +
                                " AND topic_type=@topic_type " +
                                " AND device_id=@device_id";

                await using var cmd = new NpgsqlCommand(cmdString, conn);

                cmd.Parameters.AddWithValue("@publisher_id", topic.PublisherId);
                cmd.Parameters.AddWithValue("@username", topic.AppOwnerUsername);
                cmd.Parameters.AddWithValue("@app_id", topic.AppId);
                cmd.Parameters.AddWithValue("@topic_type", topic.Type);
                cmd.Parameters.AddWithValue("@device_id", topic.DeviceId);

                await cmd.ExecuteNonQueryAsync();
            }
            catch (Exception e)
            {
                _logger.LogError("Error RemoveApnDeviceRegistrationsAsync: " + e);
            }
        }

        #endregion


        #region Others

        public bool ExecuteNonQueryWithParams(string queryString, string[] parameters)
        {
            throw new NotImplementedException();
        }

        public string BuildStringOfQueryWithParams(string query, string[] qparams)
        {
            throw new NotImplementedException();
        }

        public List<string> GetTopicSubscriptions(string publisherId, string username, string appId, string type,
            string deviceId)
        {
            throw new NotImplementedException();
        }

        public Dictionary<string, object> GetTopics(string topics, string publisherId, string username, string appId,
            string type, string deviceId)
        {
            throw new NotImplementedException();
        }

        public List<string> ReadColumn(string query, string[] qparams)
        {
            throw new NotImplementedException();
        }

        public Dictionary<string, string> ReadColumns(string query, string[] qparams, string[] dictkeys)
        {
            throw new NotImplementedException();
        }

        public Dictionary<string, int> DeleteInvalidRegistrations(ConcurrentQueue<string> invalidRegistrationsQueue,
            int regDeleteBatch, string type)
        {
            throw new NotImplementedException();
        }

        public Dictionary<string, int> GCMRegister(ConcurrentQueue<DeviceRegistration> registrationsQueue)
        {
            throw new NotImplementedException();
        }

        public long DeleteBlackListedDevices(Message message)
        {
            throw new NotImplementedException();
        }

        public List<string> GetGCMRegistrations(Message message, string[] version)
        {
            throw new NotImplementedException();
        }


        public Dictionary<string, int> PwaRegister(ConcurrentQueue<PwaDeviceRegistration> registrationsQueue)
        {
            throw new NotImplementedException();
        }

        public List<PwaDeviceRegistration> GetPwaRegistrations(Message message)
        {
            throw new NotImplementedException();
        }

        public Dictionary<string, object> InsertPwaTopics(string topics, string publisherId, string username,
            string appId, string type,
            string deviceId)
        {
            throw new NotImplementedException();
        }

        #endregion Others
    }
}
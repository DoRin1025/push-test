using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using ApplicationCore.APNS;
using ApplicationCore.Interfaces;
using Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Model;
using Newtonsoft.Json.Linq;

namespace Api.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api")]
    public class ApiController : ControllerBase
    {
        private readonly ICloudMessageManager _cloudMessageManager;
        private readonly IConfiguration _configuration;
        private readonly ITokenService _tokenService;
        private readonly IApnHelper _apnHelper;
        private readonly IRepository _repository;
        private readonly ILogger<ApiController> _logger;


        public ApiController(ICloudMessageManager cloudMessageManager, IConfiguration configuration,
            ITokenService tokenService, IApnHelper apnHelper, ILogger<ApiController> logger, IRepository repository)
        {
            _cloudMessageManager = cloudMessageManager;
            _configuration = configuration;
            _tokenService = tokenService;
            _apnHelper = apnHelper;
            _logger = logger;
            _repository = repository;
        }

        [AllowAnonymous]
        [HttpPost("authenticate")]
        public IActionResult Authenticate([FromBody] AuthenticateModel model)
        {
            if (!model.IsValidModel())
            {
                return BadRequest("Invalid request");
            }

            var apiKey = _configuration["ApiKey"];

            if (string.IsNullOrWhiteSpace(apiKey))
            {
                return BadRequest();
            }

            if (!apiKey.Equals(model.ApiKey))
            {
                return BadRequest();
            }

            var token = _tokenService.GetToken(model);

            if (token == null)
            {
                return BadRequest();
            }

            return Ok(new Dictionary<string, string>()
            {
                {"token", token}
            });
        }

        [HttpPost("register_device")]
        public ActionResult RegisterDevice([FromBody] DeviceRegistration registration)
        {
            if (registration == null)
            {
                return BadRequest();
            }

            if (!registration.IsValid(out var validationArgs))
            {
                var error = "Invalid request. At least one of this params is invalid: " + validationArgs;

                var response = new ApiError();
                response.SetError(HttpStatusCode.BadRequest, "invalidParameters", error);

                return Ok(response);
            }

            _cloudMessageManager.RegisterDevice(registration);
            return Ok();
        }
        
        
        [HttpPost("send_message")]
        public async Task<ActionResult> SendMessage()
        {
            string requestBody;
            Message message;
            using (var reader = new StreamReader(Request.Body))
            {
                requestBody = await reader.ReadToEndAsync();
            }

            try
            {
                message = Message.FromJson(requestBody);
            }
            catch (Exception ex)
            {
                var apiError = new ApiError();
                apiError.SetError(HttpStatusCode.BadRequest, "badRequest", "Invalid parameters");
                _logger.LogError("Error " + ex.Message);

                return Ok(apiError);
            }

            var success = _cloudMessageManager.Enqueue(message);

            if (success)
            {
                if (message.Platform == Message.PLATFORM_APN)
                {
                    message.SetApnPayload(_apnHelper.GenerateJsonObjectApnPayload(message.Data));
                }

                if (message.QuickDeliveryTimeout > 0)
                {
                    await Task.Delay(message.QuickDeliveryTimeout);
                }
            }
            else
            {
                message.Status = Message.STATUS_FAILED;
                message.ErrorReason = Message.ERROR_OVER_CAPACITY;
                message.ErrorMessage = "Message queue is full. Try again later.";
            }

            return Content(message.ToJsonString(), "application/json");
        }
        
        [HttpPost("get_message")]
        public ActionResult GetMessage([FromBody] MessageStatus messageStatus)
        { 
            var id = messageStatus.Id ?? "";
            if (string.IsNullOrEmpty(id))
            {
                var apiError = new ApiError();
                apiError.SetError(HttpStatusCode.BadRequest, "badRequest", "Invalid parameters");
                return Ok(apiError);
            }

            if (!_cloudMessageManager.Messages.ContainsKey(id))
            {
                var apiError = new ApiError();
                apiError.SetError(HttpStatusCode.NotFound, "notFound", "Message not found.");

                _logger.LogError("get_message message not found with id: " + id);
                return Ok(apiError);
            }

            return Content(_cloudMessageManager.Messages[id].ToJsonString(), "application/json");
        }
        
        [HttpPost("upload_cert")]
        public async Task<ActionResult> UploadCert([FromQuery] string publisherId, [FromQuery] string username,
            [FromQuery] string appId)
        {
            publisherId ??= SCEnvironment.ScPublisherId;

            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(appId))
            {
                var apiError = new ApiError();
                var message = "At least one of {" + publisherId + "}, {" + username + "}, {" + appId + "} is invalid";
                apiError.SetError(HttpStatusCode.BadRequest, "notFound", message);
                return Ok(apiError);
            }

            var response = await _apnHelper.SaveUserCertFile(publisherId, username, appId, Request.Body);

            var res = new Dictionary<string, string>()
            {
                {"result","ok"}
            };
            return  Ok(res);
        }
        
        [HttpPost("get_topic_subscriptions")]
        public async Task<ActionResult> GetTopicSubscriptions([FromBody] Topic topic)
        {
            if (topic == null)
            {
                return BadRequest();
            }

            topic.PublisherId ??= SCEnvironment.ScPublisherId;
            
            if (!topic.IsValid())
            {
                var apiError = new ApiError();
                apiError.SetError(HttpStatusCode.BadRequest, "invalidParameters",
                    "At least one of {publisherId}, {username}, {appId}, {deviceId}, {type} is invalid");
                return Ok(apiError);
            }

            var topics = await _repository.GetAPNSTopicSubscriptions(topic);

            var jObject = new JObject {{"items", string.Join(",", topics)}};
            return new JsonResult(jObject);
        }
        
        [HttpPost("topic_subscriptions")]
        public async Task<ActionResult> PostTopicSubscriptions([FromBody] Topic topic)
        {
            if (topic == null)
            {
                return BadRequest();
            }
            
            topic.PublisherId ??= SCEnvironment.ScPublisherId;
            
            if (!topic.IsValid())
            {
                var apiError = new ApiError();
                apiError.SetError(HttpStatusCode.BadRequest, "invalidParameters",
                    "At least one of {publisherId}, {username}, {appId}, {deviceId}, {type} is invalid");
                return Ok(apiError);
            }

            var inserted = 0;

            if (string.IsNullOrEmpty(topic.Topics)) return Ok();

            var sqlResult = await _repository.SetAPNSTopicSubscriptions(topic);
            var topicIds = (sqlResult["topicIds"] as string[]) ?? new string[] { };
            inserted = (int) sqlResult["inserted"];

            if (inserted != topicIds.Length)
            {
                return BadRequest("Not all topics inserted");
            }

            return Ok();
        }
    }
}
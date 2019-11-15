using JobTrackerX.Entities;
using JobTrackerX.SharedLibs;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Polly;
using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Mail;
using System.Text;
using System.Threading.Tasks;

namespace JobTrackerX.WebApi.Services.ActionHandler
{
    public class ActionHandlerPool
    {
        private readonly EmailConfig _emailConfig;
        private readonly SmtpClient _smtpClient;
        private readonly ILogger<ActionHandlerPool> _logger;
        private readonly IHttpClientFactory _httpFactory;
        private readonly JsonSerializerSettings _serializerSettings;

        public ActionHandlerPool(IOptions<EmailConfig> options, IHttpClientFactory httpFactory, ILogger<ActionHandlerPool> logger)
        {
            _serializerSettings = new JsonSerializerSettings()
            {
                ContractResolver = new CamelCasePropertyNamesContractResolver()
            };
            _httpFactory = httpFactory;

            _emailConfig = options.Value;
            _smtpClient = new SmtpClient(_emailConfig.SmtpHost, _emailConfig.SmtpPort)
            {
                UseDefaultCredentials = false,
                Credentials = new NetworkCredential(_emailConfig.Account, _emailConfig.Password)
            };
            _logger = logger;
        }

        public async Task<bool> HandleMessageAsync(ActionMessageDto msg)
        {
            var flag = false;
            if (msg.ActionConfig.ActionWrapper.EmailConfig != null)
            {
                flag = await SendEmailAsync(msg);
                if (!flag)
                {
                    return false;
                }
            }

            if (msg.ActionConfig.ActionWrapper.HttpConfig != null)
            {
                flag = await SendPostRequestAsync(msg);
                if (!flag)
                {
                    return false;
                }
            }
            return flag;
        }

        private async Task<bool> SendPostRequestAsync(ActionMessageDto config)
        {
            try
            {
                var dto = config.ActionConfig.ActionWrapper.HttpConfig;
                var client = _httpFactory.CreateClient();
                if (string.IsNullOrEmpty(dto.Url))
                {
                    return true;
                }
                var req = new HttpRequestMessage(HttpMethod.Post, dto.Url);
                if (dto.Headers?.Any() == true)
                {
                    foreach (var header in dto.Headers)
                    {
                        req.Headers.TryAddWithoutValidation(header.Key, header.Value);
                    }
                }
                var body = new HttpActionBody()
                {
                    Config = dto,
                    JobId = config.JobId,
                    JobState = config.JobState,
                    Payload = dto.Payload
                };
                req.Content = new StringContent(JsonConvert.SerializeObject(body, _serializerSettings), Encoding.UTF8, "application/json");
                await Policy.Handle<Exception>()
                     .WaitAndRetryAsync(Constants.GlobalRetryWaitSec, _ => TimeSpan.FromSeconds(Constants.GlobalRetryWaitSec))
                     .ExecuteAsync(async () => await client.SendAsync(req));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"exception in {nameof(SendPostRequestAsync)}");
                return false;
            }
            return true;
        }

        private async Task<bool> SendEmailAsync(ActionMessageDto config)
        {
            var dto = config.ActionConfig.ActionWrapper.EmailConfig;
            if (dto.Recipients?.Any() != true)
            {
                return true;
            }
            var message = new MailMessage { From = new MailAddress(_emailConfig.Account) };
            foreach (var recipient in dto.Recipients)
            {
                message.To.Add(recipient);
            }
            message.Subject = $"[{Constants.BrandName}]-{config.JobId}-{config.JobState}";
            message.Body = $"[{Constants.BrandName}]-{config.JobId}-{config.JobState}";
            try
            {
                await Policy.Handle<Exception>()
                      .WaitAndRetryAsync(Constants.GlobalRetryWaitSec, _ => TimeSpan.FromSeconds(Constants.GlobalRetryWaitSec))
                      .ExecuteAsync(async () => await _smtpClient.SendMailAsync(message));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"exception in {nameof(SendEmailAsync)}");
                return false;
            }
            return true;
        }
    }
}

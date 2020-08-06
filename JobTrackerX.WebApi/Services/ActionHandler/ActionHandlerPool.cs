using JobTrackerX.Entities;
using JobTrackerX.Entities.GrainStates;
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
            _logger = logger;
        }

        public async Task<bool> HandleMessageAsync(ActionMessageDto msg)
        {
            var flag = false;
            if (msg.ActionConfig.ActionWrapper.EmailConfig != null)
            {
                flag = await Policy.Handle<Exception>()
                     .WaitAndRetryAsync(Constants.GlobalRetryTimes, _ => TimeSpan.FromSeconds(Constants.GlobalRetryWaitSec))
                     .ExecuteAsync(async () => await SendEmailAsync(msg));
                if (!flag)
                {
                    return false;
                }
            }

            if (msg.ActionConfig.ActionWrapper.HttpConfig != null)
            {
                flag = await Policy.Handle<Exception>()
                    .WaitAndRetryAsync(Constants.GlobalRetryTimes, _ => TimeSpan.FromSeconds(Constants.GlobalRetryWaitSec))
                    .ExecuteAsync(async () => await SendPostRequestAsync(msg));
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
                if (string.IsNullOrEmpty(dto.Url))
                {
                    return true;
                }
                var client = _httpFactory.CreateClient();

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
                var resp = await client.SendAsync(req);
                resp.EnsureSuccessStatusCode();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"exception in {nameof(SendPostRequestAsync)}");
                throw;
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
            if (dto.Ccs?.Any() == true)
            {
                foreach (var cc in dto.Ccs)
                {
                    message.CC.Add(cc);
                }
            }
            message.Subject =
                string.IsNullOrWhiteSpace(dto.Subject)
                ? $"[{Constants.BrandName}]-{config.JobId}-{config.JobState}" : dto.Subject;
            message.Body = $"[{Constants.BrandName}]-{config.JobId}-{config.JobState}";
            try
            {
                using var smtp = new SmtpClient(_emailConfig.SmtpHost, _emailConfig.SmtpPort)
                {
                    UseDefaultCredentials = false,
                    Credentials = new NetworkCredential(_emailConfig.Account, _emailConfig.Password)
                };
                await smtp.SendMailAsync(message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"exception in {nameof(SendEmailAsync)}");
                throw;
            }
            return true;
        }

        public async Task<bool> HandleStateCheckerMessageAsync(ActionConfigWrapper actionWrapper, StateCheckMessageDto msg, JobEntityState jobEntity)
        {
            if (actionWrapper == null)
            {
                return true;
            }
            var flag = false;
            if (actionWrapper.EmailConfig != null)
            {
                flag = await Policy.Handle<Exception>()
                     .WaitAndRetryAsync(Constants.GlobalRetryTimes, _ => TimeSpan.FromSeconds(Constants.GlobalRetryWaitSec))
                     .ExecuteAsync(async () => await SendStateCheckEmailAsync(actionWrapper, msg, jobEntity));
                if (!flag)
                {
                    return false;
                }
            }

            if (actionWrapper.HttpConfig != null)
            {
                flag = await Policy.Handle<Exception>()
                    .WaitAndRetryAsync(Constants.GlobalRetryTimes, _ => TimeSpan.FromSeconds(Constants.GlobalRetryWaitSec))
                    .ExecuteAsync(async () => await SendStateCheckPostRequestAsync(actionWrapper, msg, jobEntity));
                if (!flag)
                {
                    return false;
                }
            }
            return flag;
        }

        private async Task<bool> SendStateCheckPostRequestAsync(ActionConfigWrapper actionWrapper, StateCheckMessageDto msg, JobEntityState jobEntity)
        {
            try
            {
                var dto = actionWrapper.HttpConfig;
                if (string.IsNullOrEmpty(dto.Url))
                {
                    return true;
                }
                var client = _httpFactory.CreateClient();

                var req = new HttpRequestMessage(HttpMethod.Post, dto.Url);
                if (dto.Headers?.Any() == true)
                {
                    foreach (var header in dto.Headers)
                    {
                        req.Headers.TryAddWithoutValidation(header.Key, header.Value);
                    }
                }
                req.Headers.TryAddWithoutValidation(Constants.StateCheckerHeaderKey, "true");
                var body = new StateCheckActionBody()
                {
                    Config = dto,
                    JobId = msg.JobId,
                    JobState = jobEntity.CurrentJobState,
                    Payload = dto.Payload,
                    TargetJobStateList = msg.StateCheckConfig.TargetStateList
                };
                req.Content = new StringContent(JsonConvert.SerializeObject(body, _serializerSettings), Encoding.UTF8, "application/json");
                var resp = await client.SendAsync(req);
                resp.EnsureSuccessStatusCode();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"exception in {nameof(SendStateCheckPostRequestAsync)}");
                throw;
            }
            return true;
        }

        private async Task<bool> SendStateCheckEmailAsync(ActionConfigWrapper wrapper, StateCheckMessageDto msg, JobEntityState jobEntity)
        {
            var dto = wrapper.EmailConfig;
            if (dto.Recipients?.Any() != true)
            {
                return true;
            }
            var message = new MailMessage { From = new MailAddress(_emailConfig.Account) };
            foreach (var recipient in dto.Recipients)
            {
                message.To.Add(recipient);
            }
            if (dto.Ccs?.Any() == true)
            {
                foreach (var cc in dto.Ccs)
                {
                    message.CC.Add(cc);
                }
            }
            message.Subject =
                string.IsNullOrWhiteSpace(dto.Subject)
                ? $"[{Constants.BrandName}]: {jobEntity.JobId}'s State Check Notification" : dto.Subject;
            message.Body = @$"
JobId:   {jobEntity.JobId}
Expect:  [{string.Join(',', msg.StateCheckConfig.TargetStateList.Select(s => s.ToString()))}]
Actual:  {jobEntity.CurrentJobState}
Success: {msg.StateCheckConfig.TargetStateList.Contains(jobEntity.CurrentJobState)}";
            try
            {
                using var smtp = new SmtpClient(_emailConfig.SmtpHost, _emailConfig.SmtpPort)
                {
                    UseDefaultCredentials = false,
                    Credentials = new NetworkCredential(_emailConfig.Account, _emailConfig.Password)
                };
                await smtp.SendMailAsync(message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"exception in {nameof(SendStateCheckEmailAsync)}");
                throw;
            }
            return true;
        }
    }
}

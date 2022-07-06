using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace HttpClientHelpers;

public class LoggingDelegateHandler : DelegatingHandler
{
    private ILogger _logger;

    private void LogRequestStart(dynamic requst)
                => Logger.ReqeustStarted(_logger, null);
    private void LogResponse(dynamic requst)
                => Logger.ReqeustStarted(_logger, null);

    public LoggingDelegateHandler(ILogger repository)
    {
        _logger = repository;
    }

    //public LoggingDelegateHandler(HttpMessageHandler innerHandler, ILogger<LoggingDelegateHandler> repository)
    //    : base(innerHandler)
    //{
    //    _repository = repository;
    //}

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        // Log the request information
        LogRequestLoggingInfo(request);

        // Execute the request
        return base.SendAsync(request, cancellationToken).ContinueWith(task =>
        {
            var response = task.Result;
            // Extract the response logging info then persist the information
            LogResponseLoggingInfo(response);
            return response;
        });
    }

    private void LogRequestLoggingInfo(HttpRequestMessage request)
    {
        dynamic info = new Dictionary<string, string>();

        info.HttpMethod = request.Method.Method;
        info.UriAccessed = request.RequestUri.AbsoluteUri;

        ExtractMessageHeadersIntoLoggingInfo(info, request.Headers.ToList());

        if (request.Content != null)
        {
            request.Content.ReadAsByteArrayAsync()
                .ContinueWith(task =>
                {
                    info.BodyContent = Encoding.UTF8.GetString(task.Result);
                    LogRequestStart(info);
                });

            return;
        }

        LogRequestStart(info);
    }

    private void LogResponseLoggingInfo(HttpResponseMessage response)
    {
        dynamic info = new Dictionary<string, string>();

        info.HttpMethod = response.RequestMessage.Method.ToString();
        info.ResponseStatusCode = response.StatusCode;
        info.ResponseStatusMessage = response.ReasonPhrase;
        info.UriAccessed = response.RequestMessage.RequestUri.AbsoluteUri;

        ExtractMessageHeadersIntoLoggingInfo(info, response.Headers.ToList());

        if (response.Content != null)
        {
            response.Content.ReadAsByteArrayAsync()
                .ContinueWith(task =>
                {
                    var responseMsg = Encoding.UTF8.GetString(task.Result);
                    info.BodyContent = responseMsg;
                    LogResponse(info);
                });

            return;
        }

        LogResponse(info);
    }

    private void ExtractMessageHeadersIntoLoggingInfo(dynamic info, List<KeyValuePair<string, IEnumerable<string>>> headers)
    {
        headers.ForEach(h =>
        {
            // convert the header values into one long string from a series of IEnumerable<string> values so it looks for like a HTTP header
            var headerValues = new StringBuilder();

            if (h.Value != null)
            {
                foreach (var hv in h.Value)
                {
                    if (headerValues.Length > 0)
                    {
                        headerValues.Append(", ");
                    }

                    headerValues.Append(hv);
                }
            }

            info.Headers.Add(string.Format("{0}: {1}", h.Key, headerValues.ToString()));
        });
    }

    //Below classes are used for high performance logging
    private static class Events
    {
        public static readonly EventId StartRequest = new(1, "Start Request");
        public static readonly EventId StartResponse = new(2, "Start Response");
    }

    private static class Logger
    {
        public static readonly Action<ILogger, Exception> ReqeustStarted = LoggerMessage.Define(LogLevel.Information,
                                                                                                Events.StartRequest,
                                                                                                "Request started");
    }
}
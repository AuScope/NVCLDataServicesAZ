using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using Newtonsoft.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Enums;

namespace NVCLDataServicesAZ
{
    public class imageCarousel
    {
        private readonly ILogger<imageCarousel> _logger;

        public imageCarousel(ILogger<imageCarousel> log)
        {
            _logger = log;
        }

        [Function("imageCarousel")]
        [OpenApiOperation(operationId: "Run", tags: new[] { "logid" })]
        [OpenApiSecurity("function_key", SecuritySchemeType.ApiKey, Name = "code", In = OpenApiSecurityLocationType.Query)]
        [OpenApiParameter(name: "logid", In = ParameterLocation.Query, Required = true, Type = typeof(string), Description = "The **logid** parameter")]
        [OpenApiParameter(name: "sampleno", In = ParameterLocation.Query, Required = false, Type = typeof(string), Description = "The **sampleno** parameter")]
        [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "text/plain", bodyType: typeof(string), Description = "The OK response")]
        public async Task<IActionResult> Run(
            // {x:regex(^(imageCarousel.html|imageCarousel)$)}
            [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = "imageCarousel.html")] HttpRequest req,
            ExecutionContext context)
        {
            _logger.LogInformation("C# HTTP trigger function processed a request.");

            string logid = req.Query["logid"];
            int sampleno = 0;
            int samplenotmp = 0;
            if (int.TryParse(req.Query["sampleno"], out samplenotmp)) sampleno = samplenotmp;

            String baseurl = req.GetEncodedUrl();
            baseurl = baseurl.Substring(0, baseurl.LastIndexOf('/'));
            string proxyBaseUrl = Environment.GetEnvironmentVariable("proxyBaseUrl", EnvironmentVariableTarget.Process);
            if (!string.IsNullOrEmpty(proxyBaseUrl)) baseurl = proxyBaseUrl;

            string home = Environment.GetEnvironmentVariable("HOME", EnvironmentVariableTarget.Process);
            if (string.IsNullOrEmpty(home)) throw new Exception("home environment variable is not set.  This should be set by Azure");

            string FunctionAppDirectory = Path.Combine(home, "site", "wwwroot");

            string sqlcon = Environment.GetEnvironmentVariable("SqlConnectionString", EnvironmentVariableTarget.Process);
            if (string.IsNullOrEmpty(sqlcon)) throw new Exception("SqlConnectionString environment variable is not set.");
            using (SqlConnection connection = new SqlConnection(sqlcon))
            {
                Log imglog = NVCLDSDataAccess.getLogDetails(connection, logid);
                var domaindatapoints = NVCLDSDataAccess.getDomainDataPoints(connection, imglog.DomainLogID);

                if (sampleno > domaindatapoints.Last().SAMPLENUMBER) throw new Exception("sample number requested is outside of this image log's range");

                StringBuilder htmlcarouselcontent = new StringBuilder();
                foreach(var samplepoint in domaindatapoints )
                {

                    htmlcarouselcontent.AppendLine("<div class=\"carousel-item" + ((samplepoint.SAMPLENUMBER == sampleno) ? " active" : "") + "\">");
                    htmlcarouselcontent.AppendLine("<img class=\"caroimage\" alt=\"0\" src=\"" + baseurl + "/getImage.html?logid=" + logid + "&sampleno=" + samplepoint.SAMPLENUMBER + "\">");
                    htmlcarouselcontent.AppendLine("</div>");

                }

                var path = Path.Combine(FunctionAppDirectory, "imagecarouseltemplate.html");
                var stylepath = Path.Combine(FunctionAppDirectory, "style.css");

                if (File.Exists(path) && File.Exists(stylepath))
                {
                    string readText = File.ReadAllText(path);
                    readText=readText.Replace("<!-- CAROUSEL CONTENT -->", htmlcarouselcontent.ToString());
                    string readStyleText = File.ReadAllText(stylepath);
                    readText=readText.Replace("<!-- STYLE -->", "<style>" + readStyleText.ToString() + "</style>");
                    return new ContentResult()
                    {
                        Content = readText,
                        ContentType = "text/html",
                        StatusCode = 200
                    };
                }


            }

                return new OkObjectResult("");
        }
    }
}


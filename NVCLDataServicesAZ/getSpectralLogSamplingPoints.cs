using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Enums;

namespace NVCLDataServicesAZ
{
    public class getSpectralLogSamplingPoints
    {
        private readonly ILogger<getSpectralLogSamplingPoints> _logger;

        public getSpectralLogSamplingPoints(ILogger<getSpectralLogSamplingPoints> log)
        {
            _logger = log;
        }

        [Function("getSpectralLogSamplingPoints")]
        [OpenApiOperation(operationId: "Run", tags: new[] { "speclogid" })]
        [OpenApiSecurity("function_key", SecuritySchemeType.ApiKey, Name = "code", In = OpenApiSecurityLocationType.Query)]
        [OpenApiParameter(name: "speclogid", In = ParameterLocation.Query, Required = true, Type = typeof(List<String>), Description = "The **speclogid** parameter")]
        [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(string), Description = "The OK response")]
        public async Task<IActionResult> Run(
            // {x:regex(^(getSpectralLogSamplingPoints.html|getSpectralLogSamplingPoints)$)}
            [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = "getSpectralLogSamplingPoints.html")] HttpRequest req)
        {
            _logger.LogInformation("C# HTTP trigger function processed a request.");

            String speclogidsstring = req.Query["speclogid"];
            string[] speclogidsarray = speclogidsstring?.Split(",");
            var speclogids = new List<string>();
            if (speclogidsarray != null) speclogids.AddRange(speclogidsarray);

            string sqlcon = Environment.GetEnvironmentVariable("SqlConnectionString", EnvironmentVariableTarget.Process);
            if (string.IsNullOrEmpty(sqlcon)) throw new Exception("SqlConnectionString environment variable is not set.");
            string wavelengthstrings="";
            using (SqlConnection connection = new SqlConnection(sqlcon))
            {
                foreach (var speclogid in speclogids) {
                    string wavelengths = NVCLDSDataAccess.getSpectralLogSamplingPoints(connection, speclogid);
                    wavelengthstrings+=wavelengths;
                }
                
                return new OkObjectResult(new { wavelengths = wavelengthstrings });
            }
        }
    }
}


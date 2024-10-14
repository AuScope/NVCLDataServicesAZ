using System;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Cosmos;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using Newtonsoft.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Enums;

namespace NVCLDataServicesAZ
{
    public class getDatasetID
    {
        private readonly ILogger<getDatasetID> _logger;

        public getDatasetID(ILogger<getDatasetID> log)
        {
            _logger = log;
        }

        [Function("getDatasetID")]
        [OpenApiOperation(operationId: "Run", tags: new[] { "logid" })]
        [OpenApiSecurity("function_key", SecuritySchemeType.ApiKey, Name = "code", In = OpenApiSecurityLocationType.Query)]
        [OpenApiParameter(name: "logid", In = ParameterLocation.Query, Required = true, Type = typeof(string), Description = "The **logid** parameter")]
        [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "text/plain", bodyType: typeof(string), Description = "The OK response")]
        public async Task<IActionResult> Run(
            // {x:regex(^(getDatasetID.html|getDatasetID)$)}
            [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = "getDatasetID.html")] HttpRequest req)
        {
            _logger.LogInformation("C# HTTP trigger function processed a request.");

            string logid = req.Query["logid"];

            string sqlcon = Environment.GetEnvironmentVariable("SqlConnectionString", EnvironmentVariableTarget.Process);
            if (string.IsNullOrEmpty(sqlcon)) throw new Exception("SqlConnectionString environment variable is not set.");
            using (SqlConnection connection = new SqlConnection(sqlcon))
            {
                var imgdetails = NVCLDSDataAccess.getLogDetails(connection, logid);

                return new OkObjectResult(new { datasetid = imgdetails.DatasetID });
            }

        }
    }
}


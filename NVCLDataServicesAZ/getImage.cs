using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Dapper;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Enums;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using Newtonsoft.Json;

namespace NVCLDataServicesAZ
{
    public class getImage
    {
        private readonly ILogger<getImage> _logger;

        public getImage(ILogger<getImage> log)
        {
            _logger = log;
        }

        [FunctionName("getImage")]
        [OpenApiOperation(operationId: "Run", tags: new[] { "name" })]
        [OpenApiSecurity("function_key", SecuritySchemeType.ApiKey, Name = "code", In = OpenApiSecurityLocationType.Query)]
        [OpenApiParameter(name: "logid", In = ParameterLocation.Query, Required = false, Type = typeof(string), Description = "The **logid** parameter")]
        [OpenApiParameter(name: "datasetid", In = ParameterLocation.Query, Required = false, Type = typeof(string), Description = "The **datasetid** parameter")]
        [OpenApiParameter(name: "sampleno", In = ParameterLocation.Query, Required = false, Type = typeof(int), Description = "The **sampleno** parameter")]
        [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "image/jpeg", bodyType: typeof(byte[]), Description = "The OK response")]
        public async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = "{x:regex(^(getImage.html|getImage)$)}")] HttpRequest req)
        {
            _logger.LogInformation("C# HTTP trigger function processed a request.");

            string logid = req.Query["logid"];
            int sampleno = 0;
            int.TryParse(req.Query["sampleno"], out sampleno);

            string azureBlobStore = System.Environment.GetEnvironmentVariable("AzureBlobStore", EnvironmentVariableTarget.Process);

            if (string.IsNullOrEmpty(azureBlobStore))
            {
                string sqlcon = Environment.GetEnvironmentVariable("SqlConnectionString", EnvironmentVariableTarget.Process);
                if (string.IsNullOrEmpty(sqlcon)) throw new Exception("SqlConnectionString environment variable is not set.");
                using (SqlConnection connection = new SqlConnection(sqlcon))
                {
                    byte[] imgbytes=NVCLDSDataAccess.getImageData(connection, logid, sampleno);
                    return new FileStreamResult(new MemoryStream(imgbytes), "image/jpeg");
                }
            }
            else
            {
                // do blob store stuff
            }

            return new OkObjectResult("");
        }
    }
}


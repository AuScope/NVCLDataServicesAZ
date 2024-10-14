using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using System.Xml.Serialization;
using System.Xml;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using Newtonsoft.Json;
using System;
using System.Data;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;

namespace NVCLDataServicesAZ
{
    public class getLogCollection
    {
        private readonly ILogger<getLogCollection> _logger;

        public getLogCollection(ILogger<getLogCollection> log)
        {
            _logger = log;
        }

        [Function("getLogCollection")]
        [OpenApiOperation(operationId: "Run", tags: new[] { "datasetid" })]
        [OpenApiParameter(name: "datasetid", In = ParameterLocation.Query, Required = true, Type = typeof(string), Description = "The **datasetid** parameter")]
        [OpenApiParameter(name: "mosaicsvc", In = ParameterLocation.Query, Required = false, Type = typeof(string), Description = "The **mosaicsvc** parameter")]
        [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "text/xml, application/json", bodyType: typeof(string), Description = "The OK response")]
        public async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = "getLogCollection.html")] HttpRequest req)
        {
            _logger.LogInformation("C# HTTP trigger function processed a request.");

            string datasetid = req.Query["datasetid"];
            Boolean mosaicsvc = "true".Equals(req.Query["mosaicsvc"], StringComparison.OrdinalIgnoreCase) || "yes".Equals(req.Query["mosaicsvc"], StringComparison.OrdinalIgnoreCase);
            string outputformat = req.Query["outputformat"];
            Boolean outputjson = "json".Equals(outputformat, StringComparison.OrdinalIgnoreCase);

            string sqlcon = Environment.GetEnvironmentVariable("SqlConnectionString", EnvironmentVariableTarget.Process);
            if (string.IsNullOrEmpty(sqlcon)) throw new Exception("SqlConnectionString environment variable is not set.");

            using (SqlConnection connection = new SqlConnection(sqlcon))
            {
                List<Dataset> DatasetCollection = NVCLDSDataAccess.getdatasets(connection, null, datasetid, false);

                List <NVCLDataServicesAZ.Log> logs = null;

                if (mosaicsvc)
                {
                    logs = DatasetCollection[0].ImageLogs;
                }
                else
                {
                    logs = DatasetCollection[0].Logs;
                }

                if (!outputjson)
                {
                    var serializer = new XmlSerializer(typeof(List<Log>), new XmlRootAttribute("LogCollection"));

                    using (var stream = new StringWriter())
                    using (var writer = XmlWriter.Create(stream))
                    {
                        serializer.Serialize(writer, logs);
                        return new ContentResult()
                        {
                            Content = stream.ToString(),
                            ContentType = "text/xml",
                            StatusCode = 200
                        };
                    }
                }
                else
                {
                    return new OkObjectResult(DatasetCollection);
                }
            }
        }
    }
}


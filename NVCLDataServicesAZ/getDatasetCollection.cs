using System.Collections.Generic;
using System;
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
using Dapper;
using System.Linq;
using Microsoft.AspNetCore.Routing;
using Microsoft.IdentityModel.Tokens;
using System.Data;
using System.Net.Http;
using System.Text;
using Microsoft.AspNetCore.Hosting.Server;
using System.Globalization;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;

namespace NVCLDataServicesAZ
{
    public class getDatasetCollection
    {
        private readonly ILogger<getDatasetCollection> _logger;

        static readonly HttpClient client = new();

        public getDatasetCollection(ILogger<getDatasetCollection> log)
        {
            _logger = log;
        }

        [Function("getDatasetCollection")]
        [OpenApiOperation(operationId: "Run", tags: new[] { "holeidentifier", "datasetid" })]
        [OpenApiParameter(name: "holeidentifier", In = ParameterLocation.Query, Required = false, Type = typeof(string), Description = "The **holeidentifier** parameter")]
        [OpenApiParameter(name: "datasetid", In = ParameterLocation.Query, Required = false, Type = typeof(string), Description = "The **datasetid** parameter")]
        [OpenApiParameter(name: "headersonly", In = ParameterLocation.Query, Required = false, Type = typeof(string), Description = "The **headersonly** parameter")]
        [OpenApiParameter(name: "checkdownloadavailable", In = ParameterLocation.Query, Required = false, Type = typeof(string), Description = "The **checkdownloadavailable** parameter")]
        [OpenApiParameter(name: "outputformat", In = ParameterLocation.Query, Required = false, Type = typeof(string), Description = "The **outputformat** parameter")]
        [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "text/xml, application/json", bodyType: typeof(string), Description = "The OK response")]
       // [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "text/json", bodyType: typeof(string), Description = "The OK response")]
        public async Task<IActionResult> Run(
            //{x:regex(^(getDatasetCollection.html|getDatasetCollection)$)}
            [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = "getDatasetCollection.html")] HttpRequest req)
        {
            _logger.LogInformation("C# HTTP trigger function processed a request.");
            string sqlcon = Environment.GetEnvironmentVariable("SqlConnectionString", EnvironmentVariableTarget.Process);
            if (string.IsNullOrEmpty(sqlcon)) throw new Exception("SqlConnectionString environment variable is not set.");

            string holeid = req.Query["holeidentifier"];
            string datasetid = req.Query["datasetid"];
            string outputformat = req.Query["outputformat"];
            Boolean checkdownloadavailable = "true".Equals(req.Query["checkdownloadavailable"], StringComparison.OrdinalIgnoreCase) || "yes".Equals(req.Query["checkdownloadavailable"], StringComparison.OrdinalIgnoreCase);

            Boolean outputjson = "json".Equals(outputformat, StringComparison.OrdinalIgnoreCase);

            Boolean headersonly = "true".Equals(req.Query["headersonly"], StringComparison.OrdinalIgnoreCase) || "yes".Equals(req.Query["headersonly"], StringComparison.OrdinalIgnoreCase);

            using (SqlConnection connection = new SqlConnection(sqlcon))
            {
                List<Dataset> DatasetCollection = NVCLDSDataAccess.getdatasets(connection, holeid, datasetid, headersonly);

                if (checkdownloadavailable || !headersonly)
                {
                    foreach (Dataset dataset in DatasetCollection)
                    {
                        string cacheurl = NVCLDSDataAccess.getDownloadLink(dataset);
                        if (!string.IsNullOrEmpty(cacheurl))
                        {
                            dataset.downloadLink = cacheurl;
                        }
                    }
                }

                if (!outputjson)
                {
                    var serializer = new XmlSerializer(typeof(List<Dataset>), new XmlRootAttribute("DatasetCollection"));

                    using (var stream = new StringWriter())
                    using (var writer = XmlWriter.Create(stream))
                    {
                        serializer.Serialize(writer, DatasetCollection);
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


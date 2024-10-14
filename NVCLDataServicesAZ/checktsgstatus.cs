using System.IO;
using System.Net;
using System.Threading.Tasks;
using System.Xml.Xsl;
using System.Xml;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Data;
using System.Xml.Serialization;
using Microsoft.Data.SqlClient;
using System.Xml.Linq;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Enums;


namespace NVCLDataServicesAZ
{
    public class CheckTSGStatus
    {
        private readonly ILogger<CheckTSGStatus> _logger;

        public CheckTSGStatus(ILogger<CheckTSGStatus> log)
        {
            _logger = log;
        }

        [Function("checktsgstatus")]
        [OpenApiOperation(operationId: "checktsgstatus", tags: new[] { "name" })]
        [OpenApiSecurity("function_key", SecuritySchemeType.ApiKey, Name = "code", In = OpenApiSecurityLocationType.Query)]
        [OpenApiParameter(name: "name", In = ParameterLocation.Query, Required = true, Type = typeof(string), Description = "The **Name** parameter")]
        [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "text/plain", bodyType: typeof(string), Description = "The OK response")]
        public async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = "checktsgstatus.html")] HttpRequest req, ExecutionContext context)
        {
            _logger.LogInformation("checktsgstatus processed a request.");

            string sqlcon = Environment.GetEnvironmentVariable("SqlConnectionString", EnvironmentVariableTarget.Process);
            if (string.IsNullOrEmpty(sqlcon)) throw new Exception("SqlConnectionString environment variable is not set.");

            string rawxml = "";

            using (SqlConnection connection = new SqlConnection(sqlcon))
            {
                List<Dataset> DatasetCollection = NVCLDSDataAccess.getdatasets(connection, "all", null, true);

                foreach (Dataset dataset in DatasetCollection) {
                    string cacheurl = NVCLDSDataAccess.getDownloadLink(dataset);
                    if (!string.IsNullOrEmpty(cacheurl))
                    {
                        dataset.downloadLink = cacheurl;
                    }
                }

                var serializer = new XmlSerializer(typeof(List<Dataset>), new XmlRootAttribute("DatasetCollection"));

                using (var stream = new StringWriter())
                using (var writer = XmlWriter.Create(stream))
                {
                    serializer.Serialize(writer, DatasetCollection);
                    rawxml = stream.ToString();
                }
                
            }

            if (!string.IsNullOrEmpty(rawxml)) {
                string home = Environment.GetEnvironmentVariable("HOME", EnvironmentVariableTarget.Process);
                if (string.IsNullOrEmpty(home)) throw new Exception("home environment variable is not set.  This should be set by Azure");

                string xsltpath = Path.Combine(home, "site", "wwwroot", "datasetlist.xslt");
                //var xsltpath = Path.GetFullPath(Path.Combine(context.FunctionDirectory, $"..{Path.DirectorySeparatorChar}datasetlist.xslt"));


                //var newDocument = new HTMLDocument();

                using (FileStream fileReader = File.OpenRead(xsltpath))
                using (XmlReader xsltReader = XmlReader.Create(fileReader))
                {
                    var transformer = new XslCompiledTransform();
                    transformer.Load(xsltReader);
                    TextReader reader = new StringReader(rawxml);
                    using (XmlReader oldDocumentReader = XmlReader.Create(reader))
                    {
                        using (StringWriter results = new StringWriter())
                        {
                            transformer.Transform(oldDocumentReader,null , results);
                            return new ContentResult()
                            {
                                Content = results.ToString(),
                                ContentType = "text/html",
                                StatusCode = 200
                            };
                        }
                    }
                }

            }
            throw new Exception("error");

        }
    }
}


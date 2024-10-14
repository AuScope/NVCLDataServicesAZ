using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
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
    public class getspectraldata
    {
        private readonly ILogger<getspectraldata> _logger;

        public getspectraldata(ILogger<getspectraldata> log)
        {
            _logger = log;
        }

        [FunctionName("getspectraldata")]
        [OpenApiOperation(operationId: "Run", tags: new[] { "speclogid" })]
        [OpenApiSecurity("function_key", SecuritySchemeType.ApiKey, Name = "code", In = OpenApiSecurityLocationType.Query)]
        [OpenApiParameter(name: "speclogid", In = ParameterLocation.Query, Required = true, Type = typeof(string), Description = "The **speclogid** parameter")]
        [OpenApiParameter(name: "startsampleno", In = ParameterLocation.Query, Required = false, Type = typeof(string), Description = "The **startsampleno** parameter")]
        [OpenApiParameter(name: "endsampleno", In = ParameterLocation.Query, Required = false, Type = typeof(string), Description = "The **endsampleno** parameter")]
        [OpenApiParameter(name: "outputformat", In = ParameterLocation.Query, Required = false, Type = typeof(string), Description = "The **outputformat** parameter")]
        [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "text/plain", bodyType: typeof(string), Description = "The OK response")]
        public async Task<IActionResult> Run(
            // "{x:regex(^(getspectraldata.html|getspectraldata)$)}"
            [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = "getspectraldata.html")] HttpRequest req)
        {
            _logger.LogInformation("C# HTTP trigger function processed a request.");

            string speclogid = req.Query["speclogid"];
            string outputformat = req.Query["outputformat"];
            Boolean outputjson = "json".Equals(outputformat, StringComparison.OrdinalIgnoreCase);
            int startsampleno = 0;
            int startsamplenotmp = 0;
            if (int.TryParse(req.Query["startsampleno"], out startsamplenotmp)) startsampleno = startsamplenotmp;
            int endsampleno = 9999999;
            int endsamplenotmp = 0;
            if (int.TryParse(req.Query["endsampleno"], out endsamplenotmp)) endsampleno = endsamplenotmp;



            List<SpectralData> spectraldata = new List<SpectralData>();

            string sqlcon = Environment.GetEnvironmentVariable("SqlConnectionString", EnvironmentVariableTarget.Process);
            if (string.IsNullOrEmpty(sqlcon)) throw new Exception("SqlConnectionString environment variable is not set.");
            using (SqlConnection connection = new SqlConnection(sqlcon))
            {
                spectraldata = NVCLDSDataAccess.getSpectralData(connection, speclogid, startsampleno, endsampleno);   
            }


            if (outputjson)
            {
                var jsonlist = new List<Object>();
                foreach (SpectralData data in spectraldata)
                {
                    var floatArray = new float[data.floatspectraldata.Length / 4];
                    Buffer.BlockCopy(data.floatspectraldata, 0, floatArray, 0, data.floatspectraldata.Length);
                    jsonlist.Add(new {sampleNo = data.sampleNo, floatspectraldata = floatArray });
                }
                return new OkObjectResult(jsonlist);
            }
            else
            {
                byte[] output = new byte[spectraldata.Sum(arr => arr.floatspectraldata.Length)];
                using (var stream = new MemoryStream(output))
                {
                    foreach (var bytes in spectraldata)
                        stream.Write(bytes.floatspectraldata, 0, bytes.floatspectraldata.Length);

                    stream.Flush();
                }
                return new FileStreamResult(new MemoryStream(output), "application/octet-stream");
            }

        }
    }
}


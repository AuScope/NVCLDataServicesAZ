using System.Collections.Generic;
using System;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using System.Xml.Serialization;
using System.Xml;
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
using Dapper;
using System.Linq;
using Microsoft.AspNetCore.Routing;
using Microsoft.IdentityModel.Tokens;
using System.Data;
using System.Net.Http;
using System.Text;
using Microsoft.AspNetCore.Hosting.Server;
using System.Globalization;

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

        [FunctionName("getDatasetCollection")]
        [OpenApiOperation(operationId: "Run", tags: new[] { "holeidentifier" })]
        [OpenApiSecurity("function_key", SecuritySchemeType.ApiKey, Name = "code", In = OpenApiSecurityLocationType.Query)]
        [OpenApiParameter(name: "holeidentifier", In = ParameterLocation.Query, Required = false, Type = typeof(string), Description = "The **holeidentifier** parameter")]
        [OpenApiParameter(name: "datasetid", In = ParameterLocation.Query, Required = false, Type = typeof(string), Description = "The **datasetid** parameter")]
        [OpenApiParameter(name: "headersonly", In = ParameterLocation.Query, Required = false, Type = typeof(string), Description = "The **headersonly** parameter")]
        [OpenApiParameter(name: "checkdownloadavailable", In = ParameterLocation.Query, Required = false, Type = typeof(string), Description = "The **checkdownloadavailable** parameter")]
        [OpenApiParameter(name: "outputformat", In = ParameterLocation.Query, Required = false, Type = typeof(string), Description = "The **outputformat** parameter")]
        [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "text/xml, application/json", bodyType: typeof(string), Description = "The OK response")]
       // [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "text/json", bodyType: typeof(string), Description = "The OK response")]
        public async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = "{x:regex(^(getDatasetCollection.html|getDatasetCollection)$)}" )] HttpRequest req)
        {
            _logger.LogInformation("C# HTTP trigger function processed a request.");
            string sqlcon = Environment.GetEnvironmentVariable("SqlConnectionString", EnvironmentVariableTarget.Process);
            if (string.IsNullOrEmpty(sqlcon)) throw new Exception("SqlConnectionString environment variable is not set.");

            string holeid = req.Query["holeidentifier"];
            string datasetid = req.Query["datasetid"];
            string outputformat = req.Query["outputformat"];

            Boolean outputjson = "json".Equals(outputformat, StringComparison.OrdinalIgnoreCase);

            Boolean headersonly = "true".Equals(req.Query["headersonly"], StringComparison.OrdinalIgnoreCase);
            
            List <Dataset> DatasetCollection = new List<Dataset>();

            using (SqlConnection connection = new SqlConnection(sqlcon))
            {
                IEnumerable<Dataset> datasets = new List<Dataset>();
                if (!string.IsNullOrEmpty(holeid))
                {
                    datasets = connection.Query<Dataset>("if @holeidentifier = 'all' select dataset_id DatasetID, datasetname, holedatasourcename, holeidentifier, dsdescription, traylog_id, sectionlog_id, domain_id, modifieddate, createddate from publisheddatasets else select dataset_id DatasetID, datasetname, holedatasourcename, holeidentifier, dsdescription, traylog_id, sectionlog_id, domain_id, modifieddate, createddate from publisheddatasets where @holeidentifier = holeidentifier", new { holeidentifier = holeid });
                }
                else if (!string.IsNullOrEmpty(datasetid))
                {
                    datasets = connection.Query<Dataset>("select dataset_id DatasetID, datasetname, holedatasourcename, holeidentifier, dsdescription, traylog_id, sectionlog_id, domain_id, modifieddate, createddate from publisheddatasets where dataset_id = @datasetid", new { datasetid = datasetid });
                }
                foreach (var ds in datasets)
                {
                    DatasetCollection.Add(ds);

                    if (!headersonly)
                    {
                        string sql = @"
                        SELECT MIN(STARTVALUE) start, MAX(ENDVALUE) [end] FROM DOMAINLOGDATA WHERE LOG_ID=@logid;
                        select logs.LOG_ID LogID,logs.LOGNAME, dbo.GETDATAPOINTS(logs.LOG_ID) as samplecount, logs.customscript script, spectrallogs.SPECTRALSAMPLINGPOINTS wavelengthsbytes,spectrallogs.SPECTRALUNITS wavelengthUnits,spectrallogs.fwhm,spectrallogs.tirq from logs inner join spectrallogs on logs.log_id=spectrallogs.log_id where logs.dataset_id=@datasetid and logtype =5 order by spectrallogs.LAYERORDER;
                        select log_id logid, logname, dbo.GETDATAPOINTS(logs.DOMAINLOG_ID) as samplecount from logs where dataset_id=@datasetid and logtype=3 order by case logname when 'Mosaic' then 1 when 'Tray Thumbnail Images' then 2 when 'Tray Images' then 3 when 'Imagery' then 4 when 'holeimg' then 5 else 6 end, logname;
                        select log_id logid, logname, ispublic, logtype, ALGORITHMOUTPUT_ID algorithmoutID, masklog_id maskLogID from logs where dataset_id = @datasetid and logtype in (1,2,6);
                        select logs.LOG_ID logid, logs.LOGNAME, dbo.GETDATAPOINTS(logs.LOG_ID) as samplecount, PROFLOGS.FLOATSPERSAMPLE, PROFLOGS.MINVAL, PROFLOGS.MAXVAL from logs inner join PROFLOGS on logs.log_id=PROFLOGS.LOG_ID where logs.dataset_id=@datasetid and logtype =4;
                        ";

                        using (var multi = await connection.QueryMultipleAsync(sql, new { logid = ds.domain_id, datasetid = ds.DatasetID }))
                        {
                            ds.DepthRange =  multi.Read<DepthRange>().Single();
                            ds.SpectralLogs = multi.Read<SpectralLog>().ToList();
                            ds.ImageLogs = multi.Read<Log>().ToList();
                            ds.Logs = multi.Read<Log>().ToList();
                            ds.ProfilometerLogs = multi.Read<ProfLog>().ToList();
                        }
                        /*
                        ds.DepthRange = connection.Query<DepthRange>("SELECT MIN(STARTVALUE) start, MAX(ENDVALUE) [end] FROM DOMAINLOGDATA WHERE LOG_ID=@logid", new { logid = ds.domain_id }).First();

                        var speclogs = connection.Query<SpectralLog>("select logs.LOG_ID LogID,logs.LOGNAME, dbo.GETDATAPOINTS(logs.LOG_ID) as samplecount, logs.customscript script, spectrallogs.SPECTRALSAMPLINGPOINTS wavelengthsbytes,spectrallogs.SPECTRALUNITS wavelengthUnits,spectrallogs.fwhm,spectrallogs.tirq from logs inner join spectrallogs on logs.log_id=spectrallogs.log_id where logs.dataset_id=@datasetid and logtype =5 order by spectrallogs.LAYERORDER", new { datasetid = ds.DatasetID });
                        List<SpectralLog> specloglist = new List<SpectralLog>();
                        foreach (var speclog in speclogs)
                        {
                            specloglist.Add(speclog);
                        }
                        ds.SpectralLogs = specloglist;

                        var imglogs = connection.Query<Log>("select log_id logid, logname, dbo.GETDATAPOINTS(logs.DOMAINLOG_ID) as samplecount from logs where dataset_id=@datasetid and logtype=3 order by case logname when 'Mosaic' then 1 when 'Tray Thumbnail Images' then 2 when 'Tray Images' then 3 when 'Imagery' then 4 when 'holeimg' then 5 else 6 end, logname", new { datasetid = ds.DatasetID });
                        List<Log> imgloglist = new List<Log>();
                        foreach (var imglog in imglogs)
                        {
                            imgloglist.Add(imglog);
                        }
                        ds.ImageLogs = imgloglist;

                        var logs = connection.Query<Log>("select log_id logid, logname, ispublic, logtype, ALGORITHMOUTPUT_ID algorithmoutID, masklog_id maskLogID from logs where dataset_id = @datasetid and logtype in (1,2,6)", new { datasetid = ds.DatasetID });
                        List<Log> loglist = new List<Log>();
                        foreach (var scallog in logs)
                        {
                            loglist.Add(scallog);
                        }
                        ds.Logs = loglist;

                        var proflogs = connection.Query<ProfLog>("select logs.LOG_ID logid, logs.LOGNAME, dbo.GETDATAPOINTS(logs.LOG_ID) as samplecount, PROFLOGS.FLOATSPERSAMPLE, PROFLOGS.MINVAL, PROFLOGS.MAXVAL from logs inner join PROFLOGS on logs.log_id=PROFLOGS.LOG_ID where logs.dataset_id=@datasetid and logtype =4", new { datasetid = ds.DatasetID });
                        List<ProfLog> profloglist = new List<ProfLog>();
                        foreach (var proflog in profloglist)
                        {
                            profloglist.Add(proflog);
                        }
                        ds.ProfilometerLogs = profloglist;*/

                        string cacheUrl = System.Environment.GetEnvironmentVariable("AzureCacheUrl", EnvironmentVariableTarget.Process);
                        if (!string.IsNullOrEmpty(cacheUrl))
                        {
                            try
                            {
                                using HttpResponseMessage response = await client.SendAsync(new HttpRequestMessage(HttpMethod.Head, cacheUrl + ds.datasetname + ".zip"));
                                if (response.StatusCode == HttpStatusCode.OK && response.Content.Headers.LastModified >= ds.modifieddate)
                                {
                                    ds.downloadLink = cacheUrl + ds.datasetname + ".zip";
                                }
                            }
                            catch { }
                        }
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


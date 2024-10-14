using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;
using System.Xml;
using Dapper;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using Newtonsoft.Json;
using Sharpen;
using System.Data;
using CsvHelper;
using System.Text.RegularExpressions;

namespace NVCLDataServicesAZ
{
    public class downloadscalars
    {
        private readonly ILogger<downloadscalars> _logger;

        public downloadscalars(ILogger<downloadscalars> log)
        {
            _logger = log;
        }

        [FunctionName("downloadscalars")]
        [OpenApiOperation(operationId: "Run", tags: new[] { "logid" })]
        [OpenApiParameter(name: "logid", In = ParameterLocation.Query, Required = true, Type = typeof(string), Description = "The **logid** of the scalar to download.  This can be a single or list of scalar ids")]
        [OpenApiParameter(name: "startdepth", In = ParameterLocation.Query, Required = false, Type = typeof(float), Description = "The **startdepth** parameter")]
        [OpenApiParameter(name: "enddepth", In = ParameterLocation.Query, Required = false, Type = typeof(float), Description = "The **enddepth** parameter")]
        [OpenApiParameter(name: "outputformat", In = ParameterLocation.Query, Required = false, Type = typeof(string), Description = "The **outputformat** parameter")]
        [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "text/plain", bodyType: typeof(string), Description = "The OK response")]
        public async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = "downloadscalars.html")] HttpRequest req)
        {
            _logger.LogInformation("C# HTTP trigger function processed a request.");

            string scalaridsstring = req.Query["logid"];
            string[] scalarray = scalaridsstring?.Split(",");
            var scalarids = new List<string>();
            if (scalarray != null) scalarids.AddRange(scalarray);

            float startdepth = 0;
            float startdepthtmp = 0;
            if (float.TryParse(req.Query["startdepth"], out startdepthtmp)) startdepth = startdepthtmp;
            float enddepth = float.MaxValue;
            float enddepthtmp = 0;
            if (float.TryParse(req.Query["enddepth"], out enddepthtmp)) enddepth = enddepthtmp;

            string outputformat = req.Query["outputformat"];
            Boolean outputjson = "json".Equals(outputformat, StringComparison.OrdinalIgnoreCase);

            string sqlcon = Environment.GetEnvironmentVariable("SqlConnectionString", EnvironmentVariableTarget.Process);
            if (string.IsNullOrEmpty(sqlcon)) throw new Exception("SqlConnectionString environment variable is not set.");
            using (SqlConnection connection = new SqlConnection(sqlcon))
            {
                if (NVCLDSDataAccess.validateLogId(connection, scalarids) != true)
                {
                    throw new Exception("invalid logids");
                }
                if (NVCLDSDataAccess.validateDomainlogId(connection, scalarids) != true)
                {
                    throw new Exception("logids are from different datasets.");
                }


                List<Log> logDetailsVoList = new List<Log>();
                foreach (string logid in scalarids) logDetailsVoList.Add(NVCLDSDataAccess.getLogDetails(connection, logid));
                Object[] paramss = new Object[logDetailsVoList.Count + 3];
                String logId = null;
                String logName = null;
                String domainlogId = null;
                int logType = 0;
                int i = 0;
                String sqlSelectPart = "select DOMAINLOGDATA.STARTVALUE AS StartDepth, DOMAINLOGDATA.ENDVALUE as EndDepth";
                String sqlFromPart = "from DOMAINLOGDATA ";
                String sqlWherePart = "where DOMAINLOGDATA.LOG_ID=@param0";
                String finalSql = null;
                List<String> strKeysArr = new List<String>();

                strKeysArr.Add("StartDepth");
                strKeysArr.Add("EndDepth");

                foreach (Log logDetailsVo in logDetailsVoList)
                {
                    i = i + 1;
                    logId = logDetailsVo.LogID;
                    logName = logDetailsVo.LogName;
                    domainlogId = logDetailsVo.DomainLogID;
                    logType = (int)logDetailsVo.logType;
                    if (logType == 5)
                    {
                        List<Dataset> dsinfo = NVCLDSDataAccess.getdatasets(connection,null,logDetailsVo.DatasetID,false);
                        foreach (var speclog in dsinfo[0].SpectralLogs)
                        {
                            if (speclog.logID.Equals(logId))
                            {
                                var wavelengtharray = speclog.wavelengths.Split(',');
                                for (int j = 0; j < wavelengtharray.Count(); j++)
                                    strKeysArr.Add(logName + wavelengtharray[j]);
                            }
                        }
                    }
                    else
                        strKeysArr.Add(Regex.Replace(logName,"\\W", "_"));
                    // c) check that the log id(s) will have only log type 1 or 2
                    if (logType != 1 && logType != 2 && logType != 5 && logType != 6)
                    {
                        throw new Exception("All logids must have logtype 1, 2, 5 or 6 for this service to function.");
                    }
                    // initialize the variables
                    if (i == 1)
                    {
					paramss[0] = domainlogId;
                    }

                    switch (logDetailsVo.logType)
                    {
                        case 1:
                            sqlSelectPart += ", coalesce (classspec" + i + ".CLASSTEXT, class" + i + ".CLASSTEXT) as Scal" + i
                                    + " ";
                            sqlFromPart += " inner join CLASSLOGDATA result"
                                    + i
                                    + " on result"
                                    + i
                                    + ".SAMPLENUMBER=DOMAINLOGDATA.SAMPLENUMBER LEFT OUTER JOIN CLASSSPECIFICCLASSIFICATIONS classspec"
                                    + i + " on result" + i + ".CLASSLOGVALUE = classspec" + i + ".INTINDEX and classspec" + i
                                    + ".LOG_ID=result" + i + ".LOG_ID LEFT OUTER JOIN LOGS log" + i + " on result" + i
                                    + ".log_id=log" + i + ".log_id LEFT OUTER JOIN CLASSIFICATIONS class" + i + " ON result"
                                    + i + ".CLASSLOGVALUE = class" + i + ".INTINDEX and class" + i + ".ALGORITHMOUTPUT_ID=log"
                                    + i + ".algorithmoutput_id";
                            break;

                        case 2:
                            sqlSelectPart += ", result" + i + ".DECIMALVALUE as Scal" + i + " ";
                            sqlFromPart += " inner join DECIMALLOGDATA result" + i + " on result" + i
                                    + ".SAMPLENUMBER=DOMAINLOGDATA.SAMPLENUMBER";
                            break;
                        case 5:
                            sqlSelectPart += ", result" + i + ".SPECTRALVALUES as Spec" + i + " ";
                            sqlFromPart += " inner join SPECTRALLOGDATA result" + i + " on result" + i
                                    + ".SAMPLENUMBER=DOMAINLOGDATA.SAMPLENUMBER";
                            break;
                        case 6:
                            sqlSelectPart += ", result" + i + ".MASKVALUE as Scal" + i + " ";
                            sqlFromPart += " inner join MASKLOGDATA result" + i + " on result" + i
                                    + ".SAMPLENUMBER=DOMAINLOGDATA.SAMPLENUMBER";
                            break;
                        default:
                            throw new Exception("Error !!  Logtype does not equal 1, 2, 5 or 6 !!");

                    }

                    sqlWherePart += " AND result" + i + ".LOG_ID = @param"+i;
				    paramss[i] = logId;

                }
                sqlWherePart += " AND DOMAINLOGDATA.STARTVALUE > @param"+ (logDetailsVoList.Count + 1) + " AND DOMAINLOGDATA.ENDVALUE < @param"+(logDetailsVoList.Count + 2);
				paramss[logDetailsVoList.Count +1] = startdepth;
				paramss[logDetailsVoList.Count +2] = enddepth;
                finalSql = sqlSelectPart + " " + sqlFromPart + " " + sqlWherePart + " order by DOMAINLOGDATA.samplenumber";

                var parameters = new DynamicParameters();
                int k = 0;
                foreach (var strparam in paramss)
                {
                    parameters.Add("@param" + k, strparam);
                    k++;
                }

                var result = connection.Query<Object>(finalSql, parameters).ToList();
                
                if (!outputjson)
                {

                    MemoryStream ms = new MemoryStream();
                    StreamWriter sw = new StreamWriter(ms, Encoding.UTF8);

                    var csv = new CsvHelper.CsvWriter(sw, System.Globalization.CultureInfo.CurrentCulture);
                    var dapperRows = result.Cast<IDictionary<string, object>>().ToList();

                    foreach (var item in strKeysArr)
                    {
                        csv.WriteField(item);
                    }
                    csv.NextRecord();

                    foreach (IDictionary<string, object> row in dapperRows)
                    {
                        foreach (KeyValuePair<string, object> item in row)
                        {
                            if (item.Value!=null) csv.WriteField(item.Value);
                            else csv.WriteField("null");
                        }
                        csv.NextRecord();
                    }
                    csv.Flush();
                    sw.Flush();
                    ms.Seek(0, SeekOrigin.Begin);

                    return new FileStreamResult(ms, "text/csv");
                }
                else
                {
                    return new OkObjectResult(result);
                }
            }

        }
    }
}


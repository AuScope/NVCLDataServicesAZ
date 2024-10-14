using System.Data;
using System;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Enums;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Newtonsoft.Json;
using Microsoft.Data.SqlClient;
using Dapper;
using System.Linq;
using MoreLinq;
using System.Collections.Generic;
using System.Text;
using Microsoft.SqlServer.TransactSql.ScriptDom;
using Microsoft.AspNetCore.Http.Extensions;

namespace NVCLDataServicesAZ
{
    public class mosaic
    {
        private readonly ILogger<mosaic> _logger;

        public mosaic(ILogger<mosaic> log)
        {
            _logger = log;
        }

        [FunctionName("mosaic")]
        [OpenApiOperation(operationId: "Run", tags: new[] { "logid", "datasetid" })]
        [OpenApiSecurity("function_key", SecuritySchemeType.ApiKey, Name = "code", In = OpenApiSecurityLocationType.Query)]
        [OpenApiParameter(name: "logid", In = ParameterLocation.Query, Required = false, Type = typeof(string), Description = "The **logid** parameter")]
        [OpenApiParameter(name: "datasetid", In = ParameterLocation.Query, Required = false, Type = typeof(string), Description = "The **datasetid** parameter")]
        [OpenApiParameter(name: "startsampleno", In = ParameterLocation.Query, Required = false, Type = typeof(string), Description = "The **startsampleno** parameter")]
        [OpenApiParameter(name: "endsampleno", In = ParameterLocation.Query, Required = false, Type = typeof(string), Description = "The **endsampleno** parameter")]
        [OpenApiParameter(name: "width", In = ParameterLocation.Query, Required = false, Type = typeof(int), Description = "The **width** parameter")]
        [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "text/plain", bodyType: typeof(string), Description = "The OK response")]
        public async Task<IActionResult> Run(
            //{x:regex(^(mosaic.*.html|mosaic)$)}
            [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = "mosaic.html")] HttpRequest req)
        {
            _logger.LogInformation("C# HTTP trigger function processed a request.");

            string logid = req.Query["logid"];
            int startsampleno = 0;
            int startsamplenotmp = 0;
            if (int.TryParse(req.Query["startsampleno"], out startsamplenotmp)) startsampleno=startsamplenotmp;
            int endsampleno = 9999999;
            int endsamplenotmp = 0;
            if (int.TryParse(req.Query["endsampleno"],out endsamplenotmp)) endsampleno=endsamplenotmp;
            int colWidth = 3;
            int colWidthtmp = 0;
            if (int.TryParse(req.Query["width"], out colWidthtmp)) colWidth=colWidthtmp;
            bool showdepths = "true".Equals(req.Query["showdepths"], StringComparison.OrdinalIgnoreCase);

            string datasetid = req.Query["datasetid"];
            String scalaridsstring = req.Query["scalarids"];
            string[] scalarray = scalaridsstring?.Split(",");
            var scalarids = new List<string>();
            if (scalarray!=null) scalarids.AddRange(scalarray);
            String trayThumbnailLogId = null;
            String trayLogId = null;
            String domainlogId = null;

            String baseurl = req.GetEncodedUrl();
            baseurl= baseurl.Substring(0, baseurl.LastIndexOf('/'));

            string proxyBaseUrl = Environment.GetEnvironmentVariable("proxyBaseUrl", EnvironmentVariableTarget.Process);
            if (!string.IsNullOrEmpty(proxyBaseUrl)) baseurl = proxyBaseUrl;


            string sqlcon = Environment.GetEnvironmentVariable("SqlConnectionString", EnvironmentVariableTarget.Process);
            if (string.IsNullOrEmpty(sqlcon)) throw new Exception("SqlConnectionString environment variable is not set.");
            using (SqlConnection connection = new SqlConnection(sqlcon))
            {
                if (datasetid != null)
                {

                    string sql = @"select log_id logid, logname, dbo.GETDATAPOINTS(logs.DOMAINLOG_ID) as samplecount, logs.DOMAINLOG_ID DOMAINLOGID from logs where dataset_id=@datasetid and logtype=3 order by case logname when 'Mosaic' then 1 when 'Tray Thumbnail Images' then 2 when 'Tray Images' then 3 when 'Imagery' then 4 when 'holeimg' then 5 else 6 end, logname;";
                    var imglogList = connection.Query<Log>(sql, new { datasetid = datasetid }).ToList();

                    foreach (var imagelog in imglogList)
                    {
                        if (imagelog.LogName.Equals("Tray Thumbnail Images"))
                        {
                            trayThumbnailLogId = imagelog.LogID;
                            domainlogId = imagelog.DomainLogID;
                        }
                        else if (imagelog.LogName.Equals("Tray Images"))
                        {
                            trayLogId = imagelog.LogID;
                        }
                    }
                    if (string.IsNullOrEmpty(trayThumbnailLogId) || string.IsNullOrEmpty(trayLogId))
                    {
                        throw new Exception("tray images could not be found for this datasetid");
                    }
                    logid = trayThumbnailLogId;
                }
                else
                {
                    string sql = @"select dlog.log_id logid, dlog.logname, dlog.dataset_id datasetid from logs dlog where log_id=(select logs.DOMAINLOG_ID from logs where log_id=@logid)";
                    var domainlog = connection.Query<Log>(sql, new { logid = logid }).Single();
                    domainlogId = domainlog.LogID;
                    // check this is a tray domain based image log. Scalar try maps
                    // cannot be generated on other domains.
                    if (domainlogId == null  || !domainlog.LogName.Equals("Tray Domain"))
                    {
                        scalarids.Clear();
                    }
                    else
                    {
                        datasetid = domainlog.DatasetID;

                        string logssql = @"select log_id logid, logname, dbo.GETDATAPOINTS(logs.DOMAINLOG_ID) as samplecount, logs.DOMAINLOG_ID DOMAINLOGID from logs where dataset_id=@datasetid and logtype=3 order by case logname when 'Mosaic' then 1 when 'Tray Thumbnail Images' then 2 when 'Tray Images' then 3 when 'Imagery' then 4 when 'holeimg' then 5 else 6 end, logname;";
                        var imglogList = connection.Query<Log>(logssql, new { datasetid = datasetid }).ToList();

                        foreach (var imagelog in imglogList)
                        {
                            if (imagelog.LogName.Equals("Tray Images"))
                            {
                                trayLogId = imagelog.LogID;
                            }
                        }
                        if (trayLogId == logid)
                            trayLogId = null;
                    }
                }

                StringBuilder imageURL = new StringBuilder("<!DOCTYPE HTML>\r\n<html>\r\n  <head>\r\n    <title>NVCL Data Services :: Mosaic Web Service</title>\r\n</head><body><section><div class=\"NVCLMosaicContainer\" >");

                string rangesql = @"select max(samplenumber) from domainlogdata where log_id = @logid and samplenumber between @startsampleno and @endsampleno ";
                var lastsamp = connection.Query<int>(rangesql, new { logid = domainlogId, startsampleno = startsampleno, endsampleno = endsampleno }).Single();

                endsampleno = Math.Min(endsampleno, lastsamp);
                startsampleno = Math.Min(startsampleno, endsampleno);

                List<DomainLogData> domainDataList = new List<DomainLogData>();
                if (domainlogId != null)
                {
                    string domaindatasql = @"GETDOMAINDATA";
                    domainDataList = connection.Query<DomainLogData>(domaindatasql, new { v_domainlog_id = domainlogId }, commandType: CommandType.StoredProcedure).ToList();
                }
                int i = 0;
                colWidth = Math.Min(endsampleno - startsampleno + 1, colWidth);
                String mosaicCellClass = ((colWidth > 1) ? "NVCLMosaicCelltwoDCell" : "NVCLMosaicCell");
                for (int j = startsampleno; j <= endsampleno; j++)
                {
                    // extract sample number from the array list
                    //ImageDataVo imageDataVo = it1.next();
                    // by default display 3 image per row
                    if (i != 0 && (i % colWidth == 0))
                    {
                        imageURL.Append("<div style=\"clear:both;\"></div>");
                    }

                    imageURL.Append("<div class=\"" + mosaicCellClass + "\" style=\"max-width: " + Math.Floor(100 * (100F / colWidth)) / 100 + "%;\">");

                    String titletext = "Core Image";

                    float? startdepth = null, enddepth = null;
                    if (domainDataList.Count() > 0)
                    {
                        foreach (DomainLogData datapoint in domainDataList)
                        {
                            // extract sampleNo, startValue and endValue from the array
                            // list
                            if (datapoint.SAMPLENUMBER == j)
                            {
                                startdepth = datapoint.STARTVALUE;
                                enddepth = datapoint.ENDVALUE;
                                break;
                            }
                        }
                    }
                    if (showdepths || scalarids.Count > 0)
                        imageURL.Append("<div class=\"NVCLMosaicCellContent\">");

                    if (startdepth != null && enddepth != null)
                    {
                        if (startdepth == enddepth)
                        {
                            if (showdepths)
                                imageURL.Append("<div class=\"NVCLMosaicCellDepths\" ><p class=\"NVCLMosaicCellPara\" >"
                                        + startdepth?.ToString("0.000") + "m</p></div>");
                            titletext += " at depth " + startdepth?.ToString("0.000") + "m";
                        }
                        else
                        {
                            if (showdepths)
                                imageURL.Append("<div class=\"NVCLMosaicCellDepths\" ><p class=\"NVCLMosaicCellPara\" >"
                                        + startdepth?.ToString("0.000") + "m - " + enddepth?.ToString("0.000")
                                        + "m</p></div>");
                            titletext += " for depth range " + startdepth?.ToString("0.000") + "m - "
                                    + enddepth?.ToString("0.000") + "m";
                        }
                    }

                    imageURL.Append("<div class=\"NVCLMosaicCellImg\" >");

                    if (trayLogId != null)
                    {
                        imageURL.Append("<a href=\""+ baseurl + "/imageCarousel.html?logid=" + trayLogId + "&sampleno="
                                + j + "\" target=\"_blank\">" + "<img title=\"" + titletext
                                + "\" class=\"NVCLMosaicImage\" " + "src=\""+ baseurl + "/getImage.html?logid="
                                + logid + "&sampleno=" + j + "\" alt=\"Core Image\" ></a></div>");

                    }
                    else
                    {
                        imageURL.Append("<img title=\"" + titletext + "\" class=\"NVCLMosaicImage\" " + "src=\""+ baseurl + "/getImage.html?logid=" + logid + "&sampleno=" + j
                                + "\" alt=\"Core Image\" ></div>");

                    }
                    foreach (var scal in scalarids)
                    {
                        imageURL.Append("<div class=\"NVCLMosaicCellImg\" ><img class=\"pixelated\" src=\""+ baseurl + "/gettraymap.html?logid=" + scal + "&trayindex=" + j
                                + "\" style=\"display: block;  height:100%;width:100%;\" ></div>");
                    }

                    if (showdepths || scalarids.Count > 0)
                        imageURL.Append("</div>");

                    imageURL.Append("</div>");

                    i++;
                }

                imageURL.Append("</div></section>");

                imageURL.Append("<style>\r\nh2.NVCLDSh2 {\r\n  padding:8px;\r\n  text-align:center;\r\n}\r\n\r\ntable.NVCLDSTable {\r\n  background:#CCFFFF;\r\n  margin-left:auto; \r\n  margin-right:auto;\r\n  width:800px;\r\n  border: 2px solid #000066;\r\n}\r\n\r\ntable.usageTable {\r\n\tborder: 1px solid black;\r\n\tborder-collapse:collapse;\r\n}\r\n\r\ntable.usageTable td, table.usageTable th {\r\n\tborder: 1px solid black;\r\n\tpadding:8px;\r\n}\r\n\r\ntable.NVCLDSTable td, table.NVCLDSTable th {\r\n  padding:8px;\r\n}\r\n\r\ntable.NVCLDSTable thead {\r\n  background-color:#99CCCC;\r\n}\r\n\r\n.pixelated {\r\n  image-rendering:optimizeSpeed;             /* Legal fallback */\r\n  image-rendering:-moz-crisp-edges;          /* Firefox        */\r\n  image-rendering:-o-crisp-edges;            /* Opera          */\r\n  image-rendering:-webkit-optimize-contrast; /* Safari         */\r\n  image-rendering:optimize-contrast;         /* CSS3 Proposed  */\r\n  image-rendering:crisp-edges;               /* CSS4 Proposed  */\r\n  image-rendering:pixelated;                 /* CSS4 Proposed  */\r\n  -ms-interpolation-mode:nearest-neighbor;   /* IE8+           */\r\n}\r\n\r\n.NVCLMosaicCell, .NVCLMosaicCelltwoDCell {\r\n\tdisplay:block;\r\n\tfloat:left;\r\n}\r\n\r\n.NVCLMosaicCelltwoDCell {\r\n  border-bottom: black solid 2px;\r\n  border-top: black solid 2px;\r\n  margin-bottom: 2px;\r\n}\r\n\r\n.NVCLMosaicCellContent {\r\n\tdisplay:table;\r\n\ttable-layout:fixed;\r\n\twidth:100%;\r\n\theight: 100%;\r\n}\r\n\r\n.NVCLMosaicCellDepths {\r\n\tvertical-align:middle;\r\n\tdisplay: table-cell;\r\n\twidth:33%;\r\n}\r\n\r\n.NVCLMosaicCellPara {\r\n\ttext-align: center;\r\n\tmargin: 0;\r\n}\r\n\r\n.NVCLMosaicCellImg {\r\n\tdisplay:table-cell;\r\n\theight: 100%;\r\n}\r\n\r\n.NVCLMosaicImage {\r\n\tdisplay: block;\r\n\twidth: 100%;\r\n\r\n}\r\n\r\n.floatingGraph {\r\n\tposition: absolute;\r\n\tz-index: 99;\r\n\twidth:800px;\r\n\tbackground:white;\r\n\toverflow: hidden;\r\n  display:none;\r\n  resize: both;\r\n}\r\n\r\n.circle\r\n  {\r\n    border-radius: 50%;\r\n    width: 26px;\r\n    height: 26px; \r\n    background:green;\r\n    position:absolute;\r\n  }\r\n  \r\n.flabel-center{\r\n  text-align: center;\r\n  vertical-align: middle;\r\n  position: relative;\r\n  line-height: 26px; \r\n  font-size:1em;\r\n}  \r\n\r\n.carousel-inner > .carousel-item > img,\r\n.carousel-inner > .carousel-item > a > img {\r\n    margin: auto;\r\n}\r\n\r\n.carousel {\r\n  position: absolute;\r\n  width: 90%;\r\n}\r\n\r\n.carousel-control-prev {\r\nwidth:5% !important;\r\n}\r\n\r\n.carousel-control-next {\r\nwidth:5% !important;\r\nright:-10% !important;\r\n}\r\n\r\n.caroimage {\r\nwidth:100%;\r\n}\r\n\r\n.carousel-inner {\r\n    left:5%;\r\n}\r\n\r\n.CarouselBody {\r\nbackground-color:black;\r\n}\r\n\r\n.legend\r\n{\r\n  position: absolute;\r\n  top: 10px;\r\n  right: 10px;\r\n  display:none !important;\r\n}\r\n\r\n.LSImageSlice\r\n{\r\n  display: block;\r\n  max-width: 100%;\r\n  border-left-width: 8px;\r\n  border-right-width: 8px;\r\n  border-style: solid;\r\n  border-top-width: 0px;\r\n  border-bottom-width: 0px;\r\n}\r\n\r\n.imgSlicesRotatedDiv\r\n{\r\n  transform: rotate(270deg);\r\n  transform-origin: top left;\r\n  width: 75%;\r\n  top:100%;\r\n  position: absolute;\r\n\r\n}\r\n\r\n.plotspectracontainer\r\n{\r\n  position: relative;\r\n  left:32%;\r\n}\r\n\r\n.chart_container\r\n{\r\n  margin: 10px;\r\n}\r\n\r\n.chart\r\n{\r\n  display: flex\r\n}\r\n\r\n.LSImageLink\r\n{\r\n  display: flex;\r\n  flex-direction: column;\r\n  align-items: center;\r\n}\r\n\r\n.hiddenLSImage\r\n{\r\n  width: 0px;\r\n}</style>");
                
                imageURL.Append("</body></html>");

                return new ContentResult()
                {
                    Content = imageURL.ToString(),
                    ContentType = "text/html",
                    StatusCode = 200
                };


            }

        }
    }
}


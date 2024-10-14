using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Newtonsoft.Json;
using SkiaSharp;
using static System.Net.Mime.MediaTypeNames;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;

namespace NVCLDataServicesAZ
{
    public class GetTrayMap
    {
        private readonly ILogger<GetTrayMap> _logger;

        public GetTrayMap(ILogger<GetTrayMap> log)
        {
            _logger = log;
        }

        [Function("gettraymap")]
        [OpenApiOperation(operationId: "Run", tags: new[] { "logid" })]
        [OpenApiParameter(name: "logid", In = ParameterLocation.Query, Required = true, Type = typeof(string), Description = "The **logid** parameter")]
        [OpenApiParameter(name: "trayindex", In = ParameterLocation.Query, Required = true, Type = typeof(int), Description = "The **trayindex** parameter")]
        [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "image/png", bodyType: typeof(string), Description = "The OK response")]
        public async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = "gettraymap.html")] HttpRequest req)
        {
            _logger.LogInformation("C# HTTP trigger function processed a request.");

            string logid = req.Query["logid"];
            string trayindexstr = req.Query["trayindex"];
            int trayindex = 0;
            int trayindextmp;
            if (int.TryParse(trayindexstr, out trayindextmp)) trayindex = trayindextmp;

            string sqlcon = Environment.GetEnvironmentVariable("SqlConnectionString", EnvironmentVariableTarget.Process);
            if (string.IsNullOrEmpty(sqlcon)) throw new Exception("SqlConnectionString environment variable is not set.");

            using (SqlConnection connection = new SqlConnection(sqlcon))
            {
                var logdetails = NVCLDSDataAccess.getLogDetails(connection, logid);
                var sectionlist = NVCLDSDataAccess.getTraySections(connection, logdetails.DatasetID, trayindex);

                if (sectionlist.Count <= 0)
                {
                    throw new Exception("couldnt determine tray section info.  This is likely a bad request or bad data.");
                }
                int startsampleno = sectionlist[0].startsampleno;
                int endsampleno = sectionlist[sectionlist.Count - 1].endsampleno;

                int imgmaxwidth = 0;
                int imgheight = sectionlist.Count;

                for (int i = 0; i < sectionlist.Count; i++)
                {
                    imgmaxwidth = Math.Max(sectionlist[i].endsampleno - sectionlist[i].startsampleno + 1, imgmaxwidth);
                }

                SKBitmap bitmap = new SKBitmap(imgmaxwidth, sectionlist.Count);

                using (SKCanvas bitmapCanvas = new SKCanvas(bitmap))
                {
                    bitmapCanvas.Clear(new SKColor(136, 136, 136));

                    if (logdetails.logType == 1)
                    {
                        List<ClassLogData> classdata = NVCLDSDataAccess.getClassLogData(connection, logid, startsampleno, endsampleno);
                        int previoussectionlengthsum = 0;
                        for (int i = 0; i < sectionlist.Count; i++)
                        {
                            int sectionlength = sectionlist[i].endsampleno - sectionlist[i].startsampleno + 1;
                            for (int j = 0; j < sectionlength; j++)
                            {
                                int bgr = classdata[previoussectionlengthsum + j].colour;
                                byte red = (byte)((bgr >> 16) & 0xFF);
                                byte green = (byte)((bgr >> 8) & 0xFF);
                                byte blue = (byte)((bgr >> 0) & 0xFF);
                                bitmap.SetPixel(j, i, new SKColor(blue, green, red));

                            }
                            previoussectionlengthsum += sectionlength;
                        }
                    }
                    else if (logdetails.logType == 2)
                    {
                        List<FloatLogData> floatdata = NVCLDSDataAccess.getFloatLogData(connection, logid, startsampleno, endsampleno);

                        float minvalue = float.MaxValue, maxvalue = 0;

                        DecimalValueRange logextents = NVCLDSDataAccess.getDecimalLogRange(connection, logid);

                        minvalue = logextents.minvalue;
                        maxvalue = logextents.maxvalue;

                        int previoussectionlengthsum = 0;
                        for (int i = 0; i < sectionlist.Count; i++)
                        {
                            int sectionlength = sectionlist[i].endsampleno - sectionlist[i].startsampleno + 1;
                            for (int j = 0; j < sectionlength; j++)
                            {
                                if (floatdata[previoussectionlengthsum + j].value == null)
                                    continue;
                                int palletindex = (int)((floatdata[previoussectionlengthsum + j].value - minvalue) / (maxvalue - minvalue) * 255);
                                if (palletindex < 0 || palletindex > 255)
                                    continue;
                                int bgr = NVCLDSDataAccess.pallet[palletindex];
                                bitmap.SetPixel(j, i, new SKColor(0xFF000000u | (uint)NVCLDSDataAccess.pallet[palletindex]));
                            }
                            previoussectionlengthsum += sectionlength;
                        }
                    }
                    else if (logdetails.logType == 6)
                    {
                        List<MaskLogData> maskdata = NVCLDSDataAccess.getMaskLogData(connection, logid, startsampleno, endsampleno);

                        int previoussectionlengthsum = 0;
                        for (int i = 0; i < sectionlist.Count; i++)
                        {
                            int sectionlength = sectionlist[i].endsampleno - sectionlist[i].startsampleno + 1;
                            for (int j = 0; j < sectionlength; j++)
                            {
                                //if (maskdata[previoussectionlengthsum + j].value == null)
                                //    continue;
                                if (maskdata[previoussectionlengthsum + j].value == true)
                                    bitmap.SetPixel(j, i, new SKColor(0,255,0));
                                else
                                    bitmap.SetPixel(j, i, new SKColor(255, 0, 0));
                            }
                            previoussectionlengthsum += sectionlength;
                        }
                    }
                    else
                    {
                        throw new Exception("unsupported log type");
                    }

                    SKImage image = SKImage.FromPixels(bitmap.PeekPixels());
                    SKData encoded = image.Encode();
                    // get a stream over the encoded data
                    Stream stream = encoded.AsStream();
                    return new FileStreamResult(stream, "image/png");

                }
            }
        }
    }
}


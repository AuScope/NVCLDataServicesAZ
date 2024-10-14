using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using MetadataExtractor;
using MetadataExtractor.Formats.Exif;
using MetadataExtractor.Formats.Jpeg;
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
    public class trayImageSampleLocate
    {
        private readonly ILogger<trayImageSampleLocate> _logger;

        public trayImageSampleLocate(ILogger<trayImageSampleLocate> log)
        {
            _logger = log;
        }

        [FunctionName("trayImageSampleLocate")]
        [OpenApiOperation(operationId: "trayImageSampleLocate", tags: new[] { "logid" })]
        [OpenApiSecurity("function_key", SecuritySchemeType.ApiKey, Name = "code", In = OpenApiSecurityLocationType.Query)]
        [OpenApiParameter(name: "logid", In = ParameterLocation.Query, Required = true, Type = typeof(string), Description = "The **logid** parameter")]
        [OpenApiParameter(name: "sampleno", In = ParameterLocation.Query, Required = true, Type = typeof(string), Description = "The **sampleno** parameter")]
        [OpenApiParameter(name: "pixelx", In = ParameterLocation.Query, Required = true, Type = typeof(string), Description = "The **pixelx** parameter")]
        [OpenApiParameter(name: "pixely", In = ParameterLocation.Query, Required = true, Type = typeof(string), Description = "The **pixely** parameter")]
        [OpenApiParameter(name: "imgwidth", In = ParameterLocation.Query, Required = true, Type = typeof(string), Description = "The **imgwidth** parameter")]
        [OpenApiParameter(name: "imgheight", In = ParameterLocation.Query, Required = true, Type = typeof(string), Description = "The **imgheight** parameter")]
        [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(string), Description = "The OK response")]
        public async Task<IActionResult> Run(
            // regex to allow 2 call urls {x:regex(^(trayImageSampleLocate.html|trayImageSampleLocate)$)}
            [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = "trayImageSampleLocate.html")] HttpRequest req)
        {
            _logger.LogInformation("C# HTTP trigger function processed a request.");

            string logid = req.Query["logid"];
            int sampleno = 0;
            int.TryParse(req.Query["sampleno"], out sampleno);
            int pixelx = 0;
            int.TryParse(req.Query["pixelx"], out pixelx);
            int pixely = 0;
            int.TryParse(req.Query["pixely"], out pixely);
            int imgwidth = 0;
            int.TryParse(req.Query["imgwidth"], out imgwidth);
            int imgheight = 0;
            int.TryParse(req.Query["imgheight"], out imgheight);

            string sqlcon = Environment.GetEnvironmentVariable("SqlConnectionString", EnvironmentVariableTarget.Process);
            if (string.IsNullOrEmpty(sqlcon)) throw new Exception("SqlConnectionString environment variable is not set.");
            using (SqlConnection connection = new SqlConnection(sqlcon))
            {
                byte[] imgbytes = NVCLDSDataAccess.getImageData(connection, logid, sampleno);

                var metadata = ImageMetadataReader.ReadMetadata(new MemoryStream(imgbytes));

                var jpegDirectory = metadata.OfType<MetadataExtractor.Formats.Jpeg.JpegDirectory>().FirstOrDefault();
                
                var actualimgwidth=jpegDirectory.GetImageWidth();
                var actualimgheight=jpegDirectory.GetImageHeight();

                if (imgwidth == 0 || imgheight == 0)
                {
                    imgwidth = actualimgwidth;
                    imgheight = actualimgheight;
                }
                else if (imgwidth < actualimgwidth || imgheight < actualimgheight)
                {
                    pixelx = (int)((double)pixelx * ((double)actualimgwidth / (double)imgwidth));
                    pixely = (int)((double)pixely * ((double)actualimgheight / (double)imgheight));
                }

                var jpegCommecntsDirectory = metadata.OfType<MetadataExtractor.Formats.Jpeg.JpegCommentDirectory>().FirstOrDefault();

                //foreach (var directory in subIfdDirectory)
                foreach (var tag in jpegCommecntsDirectory.Tags)
                {
                    if (string.IsNullOrEmpty(tag.Description) || !tag.Description.StartsWith("IOL34_JID1")) throw new Exception("image dimensions not found in jpeg comment as expected.");

                    var commenttext = tag.Description.Substring(11);
                    String[] values = commenttext.Trim().Split(",");
                    int sections = int.Parse(values[1]);
                    int borderwidth = int.Parse(values[6]) + 1;
                    int borderheight = int.Parse(values[7]);
                    int totalheight = 0;
                    List<int> sectionstartheights = new List<int>();
                    sectionstartheights.Add(borderheight);
                    int maxwidth = 0;
                    for (int i = 0; i < sections; i++)
                    {
                        sectionstartheights.Add(sectionstartheights.ElementAt(sectionstartheights.Count - 1) + int.Parse(values[8 + (3 * i)]));
                        totalheight += int.Parse(values[8 + (3 * i)]);
                        maxwidth = Math.Max(int.Parse(values[10 + (3 * i)]) - int.Parse(values[9 + (3 * i)]), maxwidth);
                    }
                    pixely = Math.Max(pixely, sectionstartheights.ElementAt(0));
                    pixely = Math.Min(pixely, sectionstartheights.ElementAt(sectionstartheights.Count - 1) - 1);
                    pixelx = Math.Max(pixelx, borderwidth);
                    int selectedsection = 0;
                    float distanceallongsectionpct = 0.0F;
                    for (int i = 0; i < sectionstartheights.Count - 1; i++)
                    {
                        if (pixely >= sectionstartheights.ElementAt(i) && pixely < sectionstartheights.ElementAt(i + 1))
                        {
                            selectedsection = i;
                            int sectionwidth = int.Parse(values[10 + (3 * i)]) - int.Parse(values[9 + (3 * i)]);
                            distanceallongsectionpct = ((float)pixelx - borderwidth) / sectionwidth;
                        }
                    }

                    var datasetid = NVCLDSDataAccess.getLogDetails(connection, logid).DatasetID;

                    var secs = NVCLDSDataAccess.getTraySections(connection, datasetid, sampleno);

                    int endsampno = secs.ElementAt(selectedsection).endsampleno;
                    int startsampno = secs.ElementAt(selectedsection).startsampleno;
                    int rawsampleno = (int)(((endsampno - startsampno) * distanceallongsectionpct) + startsampno);
                    return new OkObjectResult(new { sampleno = rawsampleno });

                }

            }

            return new OkObjectResult("");
        }
    }
}


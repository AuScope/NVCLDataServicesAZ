using System;
using System.Data;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Azure.Storage.Blobs;
using Dapper;
using EllipticCurve.Utils;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Enums;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Newtonsoft.Json;
using SkiaSharp;

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
        [OpenApiOperation(operationId: "Run", tags: new[] { "logid" })]
        [OpenApiSecurity("function_key", SecuritySchemeType.ApiKey, Name = "code", In = OpenApiSecurityLocationType.Query)]
        [OpenApiParameter(name: "logid", In = ParameterLocation.Query, Required = false, Type = typeof(string), Description = "The **logid** parameter")]
        [OpenApiParameter(name: "datasetid", In = ParameterLocation.Query, Required = false, Type = typeof(string), Description = "The **datasetid** parameter")]
        [OpenApiParameter(name: "sampleno", In = ParameterLocation.Query, Required = false, Type = typeof(int), Description = "The **sampleno** parameter")]
        [OpenApiParameter(name: "uncorrected", In = ParameterLocation.Query, Required = false, Type = typeof(string), Description = "The **uncorrected** parameter")]
        [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "image/jpeg", bodyType: typeof(byte[]), Description = "The OK response")]
        public async Task<IActionResult> Run(
            // "{ x: regex(^(getImage.html | getImage)$)}"
            [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = "getImage.html")] HttpRequest req)
        {
            _logger.LogInformation("C# HTTP trigger function processed a request.");

            string logid = req.Query["logid"];
            int sampleno = 0;
            int.TryParse(req.Query["sampleno"], out sampleno);
            string uncorrected = req.Query["uncorrected"];

            string sqlcon = Environment.GetEnvironmentVariable("SqlConnectionString", EnvironmentVariableTarget.Process);
            if (string.IsNullOrEmpty(sqlcon)) throw new Exception("SqlConnectionString environment variable is not set.");
            using (SqlConnection connection = new SqlConnection(sqlcon))
            {
                byte[] imgbytes=NVCLDSDataAccess.getImageData(connection, logid, sampleno);
                if (imgbytes == null || imgbytes.Length <=0) throw new Exception("No image found");


                if (string.IsNullOrEmpty(uncorrected) || !uncorrected.Equals("yes"))
                {
                    try
                    {
                        byte[] histogramLUT = NVCLDSDataAccess.getImageHistogramLUT(connection,logid);

                        if (histogramLUT == null || histogramLUT.Length <= 0) return new FileStreamResult(new MemoryStream(imgbytes), "image/jpeg");

                        using (var ms = new MemoryStream(imgbytes))
                        {
                            SKBitmap img = SKBitmap.Decode(ms);

                            byte[] data = img.Bytes;
                           
                            unsafe
                            {
                                fixed (byte* p = data) {
                                    var ptr = (int*)p;

                                    for (int j = 0; j < img.Height; j++)
                                    {
                                        int* scanPtr = ptr + (j * img.Width);
                                        for (int i = 0; i < img.Width; i++)
                                        {
                                            uint r = (uint)(((scanPtr[i]) >> 16) & 0xff);  //shift 3rd byte to first byte location
                                            uint g = (uint)(((scanPtr[i]) >> 8) & 0xff);   //shift 2nd byte to first byte location
                                            uint b = (uint)((scanPtr[i]) & 0xff);          //it is already at first byte location

                                            r = 0xff & (uint)histogramLUT[r];
                                            g = 0xff & (uint)histogramLUT[g];
                                            b = 0xff & (uint)histogramLUT[b];
                                            scanPtr[i] = (int)(0xff000000 | (r << 16) | (g << 8) | b);
                                        }
                                    }
                                }
                            }

                            var newimg = new SKBitmap();

                            // pin the managed array so that the GC doesn't move it
                            var gcHandle = GCHandle.Alloc(data, GCHandleType.Pinned);

                            // install the pixels with the color type of the pixel data
                            var info = new SKImageInfo(img.Width, img.Height, SKImageInfo.PlatformColorType, SKAlphaType.Unpremul);
                            newimg.InstallPixels(info, gcHandle.AddrOfPinnedObject(), info.RowBytes, delegate { gcHandle.Free(); }, null);

                            using var outms = new MemoryStream();
                            newimg.Encode(SKEncodedImageFormat.Jpeg,80).SaveTo(outms);
                            return new FileStreamResult(new MemoryStream(outms.ToArray()), "image/jpeg");


                        }
                    }
                    catch (Exception ex)
                    {
                        // historgram correction failed. just return the raw image
                        return new FileStreamResult(new MemoryStream(imgbytes), "image/jpeg");
                    }
                }
                return new FileStreamResult(new MemoryStream(imgbytes), "image/jpeg");

            }

        }
    }
}


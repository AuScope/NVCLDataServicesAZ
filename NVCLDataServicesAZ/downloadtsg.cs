using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs.Specialized;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using SendGrid;
using SendGrid.Helpers.Mail;

namespace NVCLDataServicesAZ
{
    public class MyLockEntity
    {
        [FunctionName(nameof(MyLockEntity))]
        public static Task Run([EntityTrigger] IDurableEntityContext ctx) => ctx.DispatchAsync<MyLockEntity>();
    }

    public static class downloadtsg
    {
        static HttpClient client = new HttpClient();

        [FunctionName("downloadtsgOrchestrator")]
        public static async Task RunOrchestrator(
            [OrchestrationTrigger] IDurableOrchestrationContext context)
        {

            DownloadRequestParams downloadTSGParams = context.GetInput<DownloadRequestParams>();

            var lockId = new EntityId(nameof(MyLockEntity), "MyLockIdentifier");
            using (await context.LockAsync(lockId))
            {
                downloadTSGParams.downloadURL = await context.CallActivityAsync<string>(nameof(isTSGFileCached), downloadTSGParams);
                if (string.IsNullOrEmpty(downloadTSGParams.downloadURL))
                {
                    Boolean tsgresult = await context.CallActivityAsync<Boolean>(nameof(BuildTSGFiles), downloadTSGParams);
                    if ( !tsgresult) { throw new Exception("tsg download process failed for dataset with ID : " + downloadTSGParams.datasetid); }
                    await context.CallActivityAsync<string>(nameof(CompressTSGFiles), downloadTSGParams);
                    downloadTSGParams.downloadURL = await context.CallActivityAsync<string>(nameof(CopyCompressedTSGFilesToStorage), downloadTSGParams);
                }
                //if(!string.IsNullOrEmpty(downloadTSGParams.downloadURL)) await context.CallActivityAsync<string>(nameof(EmailLinkToUser), downloadTSGParams);
            }

        }

        [FunctionName(nameof(isTSGFileCached))]
        public static string isTSGFileCached([ActivityTrigger] DownloadRequestParams downloadTSGParams, ILogger log, ExecutionContext context)
        {
            string TSGFileCachePublicUrl = Environment.GetEnvironmentVariable("TSGFileCachePublicUrl", EnvironmentVariableTarget.Process);
            if (string.IsNullOrEmpty(TSGFileCachePublicUrl)) throw new Exception("TSGFileCachePublicUrl environment variable is not set.");

            string NVCLCacheUrl = Environment.GetEnvironmentVariable("NVCLCacheUrl", EnvironmentVariableTarget.Process);

            using HttpResponseMessage response = client.Send(new HttpRequestMessage(HttpMethod.Head, TSGFileCachePublicUrl + "/" + downloadTSGParams.datasetid + ".zip"));
            if (response.StatusCode == HttpStatusCode.OK)
            {
                return TSGFileCachePublicUrl + "/" + downloadTSGParams.datasetid + ".zip";
            }

            if (!string.IsNullOrEmpty( NVCLCacheUrl))
            {
                using HttpResponseMessage responseNVCL = client.Send(new HttpRequestMessage(HttpMethod.Head, NVCLCacheUrl + "/" + downloadTSGParams.datasetid + ".zip"));
                if (responseNVCL.StatusCode == HttpStatusCode.OK)
                {
                    return NVCLCacheUrl + "/" + downloadTSGParams.datasetid + ".zip";
                }
                else
                {
                    string sqlcon = Environment.GetEnvironmentVariable("SqlConnectionString", EnvironmentVariableTarget.Process);
                    if (string.IsNullOrEmpty(sqlcon)) throw new Exception("SqlConnectionString environment variable is not set.");

                    using (SqlConnection connection = new SqlConnection(sqlcon))
                    {
                        List<Dataset> dslist= NVCLDSDataAccess.getdatasets(connection, null, downloadTSGParams.datasetid, true);
                        if (dslist.Count ==1 ) 
                        {
                            using HttpResponseMessage responseNVCLbyName = client.Send(new HttpRequestMessage(HttpMethod.Head, NVCLCacheUrl + "/" + dslist[0].datasetname + ".zip"));
                            if (responseNVCL.StatusCode == HttpStatusCode.OK)
                            {
                                return NVCLCacheUrl + "/" + dslist[0].datasetname + ".zip";
                            }
                        }
                    }
                }
            }


            return null;
        }

            [FunctionName(nameof(BuildTSGFiles))]
        public static Boolean BuildTSGFiles([ActivityTrigger] DownloadRequestParams downloadTSGParams, ILogger log, ExecutionContext context)
        {
            string Connection_string = Environment.GetEnvironmentVariable("DBConStrTSGFromat", EnvironmentVariableTarget.Process);
            if (string.IsNullOrEmpty(Connection_string)) throw new Exception("DBConStrTSGFromat environment variable is not set.");

            string AzureBlobStore = Environment.GetEnvironmentVariable("BinaryDataBlobStoreConStr", EnvironmentVariableTarget.Process);
            if (string.IsNullOrEmpty(AzureBlobStore)) AzureBlobStore = "";

            string AzureContainerName = Environment.GetEnvironmentVariable("BinaryDataBlobStoreContainerName", EnvironmentVariableTarget.Process);
            if (string.IsNullOrEmpty(AzureContainerName)) AzureContainerName = "";

            string Database_type = Environment.GetEnvironmentVariable("Database_typeTSGFromat", EnvironmentVariableTarget.Process);
            if (string.IsNullOrEmpty(Database_type)) Database_type = "sqlserver";

            string Username = Environment.GetEnvironmentVariable("DBUsernameTSGFromat", EnvironmentVariableTarget.Process);
            if (string.IsNullOrEmpty(Username)) throw new Exception("DBUsernameTSGFromat environment variable is not set.");

            string Password = Environment.GetEnvironmentVariable("DBPasswordTSGFromat", EnvironmentVariableTarget.Process);
            if (string.IsNullOrEmpty(Password)) throw new Exception("DBPasswordTSGFromat string environment variable is not set.");

            string home = Environment.GetEnvironmentVariable("HOME", EnvironmentVariableTarget.Process);
            if (string.IsNullOrEmpty(home)) throw new Exception("home environment variable is not set.  This should be set by Azure");


            System.Diagnostics.Process process = new System.Diagnostics.Process();
            var exepath = Path.GetFullPath(Path.Combine(context.FunctionDirectory, $"..{Path.DirectorySeparatorChar}tsgeol8.exe"));
            process.StartInfo.FileName = exepath;

            string tempspace = Path.Combine(home, "TSGFiles");
            string scriptspace = Path.Combine(home, "TSGscripts");
            if (!Directory.Exists(scriptspace)) Directory.CreateDirectory(scriptspace);
            if (!Directory.Exists(tempspace)) Directory.CreateDirectory(tempspace);
            string workingfolder = Path.Combine(tempspace, downloadTSGParams.datasetid);
            Directory.CreateDirectory(workingfolder);
            foreach (string file in Directory.GetFiles(workingfolder))
            {
                File.Delete(file);
            }

            string script = Path.Combine(scriptspace, downloadTSGParams.datasetid + ".txt");

            using (StreamWriter scriptfile = new StreamWriter(script, false))
            {
                scriptfile.WriteLine("task_begin");
                scriptfile.WriteLine("operation download ");
                scriptfile.WriteLine("Connection_string " + Connection_string);
                scriptfile.WriteLine("AzureBlobStore " + AzureBlobStore);
                scriptfile.WriteLine("AzureContainerName " + AzureContainerName);
                scriptfile.WriteLine("Database_type " + Database_type);
                scriptfile.WriteLine("Username " + Username);
                scriptfile.WriteLine("Password " + Password);
                scriptfile.WriteLine("output_dir " + workingfolder);
                scriptfile.WriteLine("Uuid " + downloadTSGParams.datasetid);
                scriptfile.WriteLine("task_end");

            }

            if (!string.IsNullOrEmpty(downloadTSGParams.datasetid))
            {
                process.StartInfo.Arguments = "/SCRIPT=" + script;
            }

            process.EnableRaisingEvents = true;
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.WorkingDirectory = workingfolder;
            log.LogInformation("TSG Process started");
            process.Start();
            process.WaitForExit();
            log.LogInformation("TSG Process finished");
            if (process.ExitCode != 0) return false;
            else return true;
        }

        [FunctionName(nameof(CompressTSGFiles))]
        public static string CompressTSGFiles([ActivityTrigger] DownloadRequestParams downloadTSGParams, ILogger log)
        {
            log.LogInformation("Compressing files");

            string home = Environment.GetEnvironmentVariable("HOME", EnvironmentVariableTarget.Process);
            if (string.IsNullOrEmpty(home)) throw new Exception("home environment variable is not set.  This should be set by Azure");

            string zipfilesdir = Path.Combine(home, "TSGZipFiles");
            Directory.CreateDirectory(zipfilesdir);
            string tempspace = Path.Combine(home, "TSGFiles");
            string workingfolder = Path.Combine(tempspace, downloadTSGParams.datasetid);
            string zipfile = Path.Combine(zipfilesdir, downloadTSGParams.datasetid + ".zip");
            if (File.Exists(zipfile)) File.Delete(zipfile);
            ZipFile.CreateFromDirectory(workingfolder, zipfile, System.IO.Compression.CompressionLevel.Optimal, true);

            log.LogInformation("finished handling TSG process result");
            return "ok";
        }

        [FunctionName(nameof(CopyCompressedTSGFilesToStorage))]
        public static string CopyCompressedTSGFilesToStorage([ActivityTrigger] DownloadRequestParams downloadTSGParams, ILogger log)
        {
            string blobStoreConStr = Environment.GetEnvironmentVariable("TSGFileCacheBlobStoreConStr", EnvironmentVariableTarget.Process);

            string blobStoreContainerName = Environment.GetEnvironmentVariable("TSGFileCacheBlobStoreContainerName", EnvironmentVariableTarget.Process);

            string TSGFileCachePublicUrl = Environment.GetEnvironmentVariable("TSGFileCachePublicUrl", EnvironmentVariableTarget.Process);
            if (string.IsNullOrEmpty(TSGFileCachePublicUrl)) throw new Exception("TSGFileCachePublicUrl environment variable is not set.");

            string home = Environment.GetEnvironmentVariable("HOME", EnvironmentVariableTarget.Process);
            if (string.IsNullOrEmpty(home)) throw new Exception("home environment variable is not set.  This should be set by Azure");

            string zipfilesdir = Path.Combine(home, "TSGZipFiles");
            string zipfile = Path.Combine(zipfilesdir, downloadTSGParams.datasetid + ".zip");


            if (!string.IsNullOrEmpty(blobStoreConStr) && !string.IsNullOrEmpty(blobStoreContainerName))
            {
                log.LogInformation("copying zip file to Azure blob store");
                var zipBlobClient = new BlockBlobClient(blobStoreConStr, blobStoreContainerName, downloadTSGParams.datasetid + ".zip");

                using (Stream blobstream = zipBlobClient.OpenWrite(true, options: new BlockBlobOpenWriteOptions
                {
                    HttpHeaders = new BlobHttpHeaders
                    {
                        ContentType = "application/zip"
                    }
                }))
                {
                    using (var fileStream = File.OpenRead(zipfile))
                    {
                        fileStream.CopyTo(blobstream);
                    }
                }
            }
            return TSGFileCachePublicUrl + "/" + downloadTSGParams.datasetid + ".zip"; ;
        }

        [FunctionName(nameof(EmailLinkToUser))]
        public static string EmailLinkToUser([ActivityTrigger] DownloadRequestParams downloadTSGParams, ILogger log)
        {

            string TSGFileCachePublicUrl = Environment.GetEnvironmentVariable("TSGFileCachePublicUrl", EnvironmentVariableTarget.Process);
            if (string.IsNullOrEmpty(TSGFileCachePublicUrl)) throw new Exception("TSGFileCachePublicUrl environment variable is not set.");

            string adminEmail = Environment.GetEnvironmentVariable("adminEmail", EnvironmentVariableTarget.Process);
            if (string.IsNullOrEmpty(adminEmail)) throw new Exception("adminEmail environment variable is not set.");

            var apiKey = Environment.GetEnvironmentVariable("SENDGRID_KEY");
            if (!string.IsNullOrEmpty(apiKey))
            {
                var client = new SendGridClient(apiKey);
                var from = new EmailAddress(adminEmail);
                var to = new EmailAddress(downloadTSGParams.email);
                var htmlContent = "";
                var content = "This is an automated email from the National Virtual Core Library Download Service.\r\n\r\nThe TSG dataset you requested is ready for download.  "+ downloadTSGParams.downloadURL + " .\r\n\r\nTo view the content of these files you will need \"The Spectral Geologist Viewer\" available at http://www.thespectralgeologist.com \r\n\r\nIf you have any comments, suggestions or issues with the download please reply to this email.\r\n";
                var msg = MailHelper.CreateSingleEmail(from, to, "NVCL Download ready", content, htmlContent);
                client.SendEmailAsync(msg);
            }

            return "ok";
        }



        [FunctionName("downloadtsg_HttpStart")]
        public static async Task<HttpResponseMessage> HttpStart(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequestMessage req,
            [DurableClient] IDurableOrchestrationClient starter,
            ILogger log)
        {

            var query = HttpUtility.ParseQueryString(req.RequestUri.Query);
            string datasetid = query.Get("datasetid");
            string email = query.Get("email");

            // Function input comes from the request content.
            string instanceId = await starter.StartNewAsync("downloadtsgOrchestrator", new DownloadRequestParams { datasetid=datasetid,email=email } );

            log.LogInformation("Started orchestration with ID = '{instanceId}'.", instanceId);

            return starter.CreateCheckStatusResponse(req, instanceId);
        }

        [FunctionName("TriggerDownloadJob")]
        public static async Task RunAsync(
            [QueueTrigger("TSGDownloadRequests")] DownloadRequestParams myQueueItem,
            [DurableClient] IDurableOrchestrationClient starter,
            ILogger log)
        {
            string instanceId = await starter.StartNewAsync("downloadtsgOrchestrator", myQueueItem);

            log.LogInformation("Queue triger Started orchestration with ID = '{instanceId}'.", instanceId);

        }

        [FunctionName("EnqueueDownloadtsgJob")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = "downloadtsg.html")] HttpRequest req,
            [Queue("TSGDownloadRequests")] ICollector<DownloadRequestParams> msg,
            ILogger log)
        {
            string datasetid = req.Query["datasetid"];

            if (!string.IsNullOrEmpty(datasetid))
            {
                msg.Add(new DownloadRequestParams { datasetid = datasetid, email = "" });
            }

            return new RedirectResult("checktsgstatus.html");
        }
    }
}
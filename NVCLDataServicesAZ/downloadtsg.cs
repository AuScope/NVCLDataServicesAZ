using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs.Specialized;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;

namespace NVCLDataServicesAZ
{
    public static class downloadtsg
    {
        [FunctionName("downloadtsg")]
        public static async Task RunOrchestrator(
            [OrchestrationTrigger] IDurableOrchestrationContext context)
        {

            DownloadRequestParams downloadTSGParams = context.GetInput<DownloadRequestParams>();


            await context.CallActivityAsync<string>(nameof(BuildTSGFiles), downloadTSGParams);
            await context.CallActivityAsync<string>(nameof(CompressTSGFiles), downloadTSGParams);
            await context.CallActivityAsync<string>(nameof(CopyCompressedTSGFilesToStorage), downloadTSGParams);

        }

        [FunctionName(nameof(BuildTSGFiles))]
        public static string BuildTSGFiles([ActivityTrigger] DownloadRequestParams downloadTSGParams, ILogger log)
        {
            string Connection_string = Environment.GetEnvironmentVariable("Connection_string", EnvironmentVariableTarget.Process);
            if (string.IsNullOrEmpty(Connection_string)) throw new Exception("Connection_string environment variable is not set.");

            string AzureBlobStore = Environment.GetEnvironmentVariable("AzureBlobStore", EnvironmentVariableTarget.Process);

            string AzureContainerName = Environment.GetEnvironmentVariable("AzureContainerName", EnvironmentVariableTarget.Process);

            string Database_type = Environment.GetEnvironmentVariable("Database_type", EnvironmentVariableTarget.Process);
            if (string.IsNullOrEmpty(Database_type)) throw new Exception("Database_type environment variable is not set.");

            string Username = Environment.GetEnvironmentVariable("DBUsername", EnvironmentVariableTarget.Process);
            if (string.IsNullOrEmpty(Username)) throw new Exception("DBUsername environment variable is not set.");

            string Password = Environment.GetEnvironmentVariable("DBPassword", EnvironmentVariableTarget.Process);
            if (string.IsNullOrEmpty(Password)) throw new Exception("DBPassword string environment variable is not set.");

            string home = Environment.GetEnvironmentVariable("HOME", EnvironmentVariableTarget.Process);
            if (string.IsNullOrEmpty(home)) throw new Exception("home environment variable is not set.  This should be set by Azure");


            System.Diagnostics.Process process = new System.Diagnostics.Process();
            process.StartInfo.FileName = Path.Combine(home, "site", "wwwroot", "tsgeol8.exe");

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
                if (!string.IsNullOrEmpty(AzureBlobStore)) scriptfile.WriteLine("AzureBlobStore " + AzureBlobStore);
                if (!string.IsNullOrEmpty(AzureContainerName)) scriptfile.WriteLine("AzureContainerName " + AzureContainerName);
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
            if (process.ExitCode != 0) return "fail";
            else return "ok";
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
            ZipFile.CreateFromDirectory(workingfolder, zipfile, CompressionLevel.Optimal, true);

            log.LogInformation("finished handling TSG process result");
            return "ok";
        }

        [FunctionName(nameof(CopyCompressedTSGFilesToStorage))]
        public static string CopyCompressedTSGFilesToStorage([ActivityTrigger] DownloadRequestParams downloadTSGParams, ILogger log)
        {
            string outputAzureBlobStore = Environment.GetEnvironmentVariable("outputAzureBlobStore", EnvironmentVariableTarget.Process);

            string outputAzureContainerName = Environment.GetEnvironmentVariable("outputAzureContainerName", EnvironmentVariableTarget.Process);

            string home = Environment.GetEnvironmentVariable("HOME", EnvironmentVariableTarget.Process);
            if (string.IsNullOrEmpty(home)) throw new Exception("home environment variable is not set.  This should be set by Azure");

            string zipfilesdir = Path.Combine(home, "TSGZipFiles");
            string zipfile = Path.Combine(zipfilesdir, downloadTSGParams.datasetid + ".zip");


            if (!string.IsNullOrEmpty(outputAzureBlobStore) && !string.IsNullOrEmpty(outputAzureContainerName))
            {
                log.LogInformation("copying zip file to Azure blob store");
                var zipBlobClient = new BlockBlobClient(outputAzureBlobStore, outputAzureContainerName, downloadTSGParams.datasetid + "zip");

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
            string instanceId = await starter.StartNewAsync("downloadtsg", new DownloadRequestParams { datasetid=datasetid,email=email } );

            log.LogInformation("Started orchestration with ID = '{instanceId}'.", instanceId);

            return starter.CreateCheckStatusResponse(req, instanceId);
        }
    }
}
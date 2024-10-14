using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs.Specialized;
using Dapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace NVCLDataServicesAZ
{
    internal static class NVCLDSDataAccess
    {
        static readonly HttpClient client = new();
        public static int[] pallet = { 255, 767, 1279, 1791, 2303, 3071, 3583, 4095, 4863, 5375, 5887, 6655, 7167, 7935, 8447, 9215, 9727, 10495, 11007, 11775, 12543, 13055, 13823, 14591, 15103, 15871, 16639, 17407, 18175, 18943, 19711, 20223, 20991, 21759, 22527, 23295, 24319, 25087, 25855, 26623, 27391, 28159, 29183, 29951, 30719, 31743, 32511, 33279, 34303, 35071, 36095, 37119, 37887, 38911, 39679, 40703, 41727, 42751, 43519, 44543, 45567, 46591, 47615, 48639, 49663, 50687, 51711, 52735, 53759, 54783, 56063, 57087, 58111, 59391, 60415, 61439, 62719, 63743, 65023, 65532, 65527, 65523, 65518, 65513, 65509, 65504, 65499, 65494, 65489, 65485, 65480, 65475, 65470, 65465, 65460, 65455, 65450, 65445, 65439, 65434, 65429, 65424, 65419, 65413, 65408, 65403, 65398, 65392, 65387, 65381, 65376, 65371, 65365, 65360, 65354, 65349, 65343, 65338, 65332, 65327, 65321, 65316, 65310, 65305, 65299, 65293, 65288, 65282, 196352, 589568, 917248, 1310464, 1703680, 2031360, 2424576, 2752256, 3145472, 3473152, 3866368, 4194048, 4587264, 4914944, 5308160, 5635840, 6029056, 6356736, 6684416, 7077632, 7405312, 7798528, 8126208, 8453888, 8781568, 9174784, 9502464, 9830144, 10157824, 10485504, 10878720, 11206400, 11534080, 11861760, 12189440, 12517120, 12844800, 13172480, 13500160, 13762304, 14089984, 14417664, 14745344, 15073024, 15335168, 15662848, 15990528, 16252672, 16580352, 16776448, 16775168, 16774144, 16772864, 16771840, 16770816, 16769536, 16768512, 16767488, 16766208, 16765184, 16764160, 16763136, 16762112, 16761088, 16760064, 16759040, 16758016, 16756992, 16755968, 16754944, 16754176, 16753152, 16752128, 16751104, 16750336, 16749312, 16748544, 16747520, 16746496, 16745728, 16744704, 16743936, 16743168, 16742144, 16741376, 16740608, 16739584, 16738816, 16738048, 16737280, 16736512, 16735744, 16734720, 16733952, 16733184, 16732416, 16731648, 16731136, 16730368, 16729600, 16728832, 16728064, 16727296, 16726528, 16726016, 16725248, 16724480, 16723968, 16723200, 16722432, 16721920, 16721152, 16720640, 16719872, 16719360, 16718592, 16718080, 16717312, 16716800, 16716288, 16715520, 16715008, 16714496, 16713728, 16713216, 16712704, 16712192, 16711680 };
        public static Log getLogDetails(SqlConnection connection, string logid)
        {
            string sql = @"select log_id logid, logname, dbo.GETDATAPOINTS(logs.DOMAINLOG_ID) as samplecount, logs.DOMAINLOG_ID DOMAINLOGID, dataset_id datasetid,logtype from logs where log_id = @logid";
            return connection.Query<Log>(sql, new { logid = logid }).Single();

        }

        public static List<DomainLogData> getDomainDataPoints(SqlConnection connection, string domainlogId)
        {
            string domaindatasql = @"GETDOMAINDATA";
            return connection.Query<DomainLogData>(domaindatasql, new
            {
                v_domainlog_id = domainlogId
            }, commandType: CommandType.StoredProcedure).ToList();
        }
        public static byte[] getImageData(SqlConnection connection, string imageid, int sampleno)
        {
            string blobStoreConStr = System.Environment.GetEnvironmentVariable("BinaryDataBlobStoreConStr", EnvironmentVariableTarget.Process);
            string blobStoreContainerName = System.Environment.GetEnvironmentVariable("BinaryDataBlobStoreContainerName", EnvironmentVariableTarget.Process);

            if (string.IsNullOrEmpty(blobStoreConStr))
            {
                string sql = @"select publishedimagelogdata.imagedata, imagelogs.imghistogram, imagelogs.imgclippercent from publishedimagelogdata inner join imagelogs on publishedimagelogdata.log_id = imagelogs.log_id where imagelogs.log_id = @logid and publishedimagelogdata.samplenumber = @sampleno";
                return connection.Query<byte[]>(sql, new { logid = imageid, sampleno = sampleno }).Single();
            }
            else
            {
                var imgdetails = NVCLDSDataAccess.getLogDetails(connection, imageid);

                if (!string.IsNullOrEmpty(imgdetails.DatasetID) && !string.IsNullOrEmpty(imageid))
                {

                    String blobName = "tsgdataset-" + imgdetails.DatasetID + "/imageLogData/" + imageid + "/" + sampleno + ".jpg";

                    var blobServiceClient = new BlobServiceClient(blobStoreConStr);

                    var blobClient = blobServiceClient.GetBlobContainerClient(blobStoreContainerName).GetBlobClient(blobName);

                    if (blobClient.ExistsAsync().Result)
                    {
                        using var ms = new MemoryStream();
                        blobClient.DownloadTo(ms);
                        return ms.ToArray();
                    }
                }
                return null;

            }

        }

        public static List<ClassLogData> getClassLogData(SqlConnection connection, String logid, int startsampleno, int endsampleno)
        {
            String sql = "select DOMAINLOGDATA.samplenumber sampleno,DOMAINLOGDATA.STARTVALUE depth, coalesce (CLASSSPECIFICCLASSIFICATIONS.CLASSTEXT, CLASSIFICATIONS.CLASSTEXT) as classtext,coalesce (CLASSSPECIFICCLASSIFICATIONS.COLOUR, CLASSIFICATIONS.COLOUR) as colour from domainlogdata inner join CLASSLOGDATA on CLASSLOGDATA.SAMPLENUMBER=DOMAINLOGDATA.SAMPLENUMBER LEFT OUTER JOIN CLASSSPECIFICCLASSIFICATIONS on CLASSLOGDATA.CLASSLOGVALUE = CLASSSPECIFICCLASSIFICATIONS.INTINDEX and CLASSSPECIFICCLASSIFICATIONS.LOG_ID=CLASSLOGDATA.LOG_ID LEFT OUTER JOIN LOGS on CLASSLOGDATA.log_id=LOGS.log_id LEFT OUTER JOIN CLASSIFICATIONS ON CLASSLOGDATA.CLASSLOGVALUE = CLASSIFICATIONS.INTINDEX and CLASSIFICATIONS.ALGORITHMOUTPUT_ID=LOGS.algorithmoutput_id WHERE CLASSLOGDATA.LOG_ID=@logid and Domainlogdata.log_id = logs.domainlog_id AND Domainlogdata.samplenumber between @startsampleno and @endsampleno order by Domainlogdata.samplenumber";
            return connection.Query<ClassLogData>(sql, new { logid = logid, startsampleno=startsampleno, endsampleno=endsampleno }).ToList();
        }

        public static List<FloatLogData> getFloatLogData(SqlConnection connection, string logid, int startsampleno, int endsampleno)
        {
            String sql = "select DOMAINLOGDATA.samplenumber sampleno,DOMAINLOGDATA.STARTVALUE depth, DECIMALLOGDATA.DECIMALVALUE value from domainlogdata inner join DECIMALLOGDATA on DECIMALLOGDATA.SAMPLENUMBER=DOMAINLOGDATA.SAMPLENUMBER INNER JOIN LOGS on DECIMALLOGDATA.log_id=LOGS.log_id WHERE DECIMALLOGDATA.LOG_ID=@logid and Domainlogdata.log_id = logs.domainlog_id AND Domainlogdata.samplenumber between @startsampleno and @endsampleno order by Domainlogdata.samplenumber";
            return connection.Query<FloatLogData>(sql, new { logid = logid, startsampleno = startsampleno, endsampleno = endsampleno }).ToList();
        }

        public static List<MaskLogData> getMaskLogData(SqlConnection connection, string logid, int startsampleno, int endsampleno)
        {
            String sql = "select DOMAINLOGDATA.samplenumber sampleno,DOMAINLOGDATA.STARTVALUE depth, MASKLOGDATA.MASKVALUE value from domainlogdata inner join MASKLOGDATA on MASKLOGDATA.SAMPLENUMBER=DOMAINLOGDATA.SAMPLENUMBER INNER JOIN LOGS on MASKLOGDATA.log_id=LOGS.log_id WHERE MASKLOGDATA.LOG_ID=@logid and Domainlogdata.log_id = logs.domainlog_id AND Domainlogdata.samplenumber between @startsampleno and @endsampleno order by Domainlogdata.samplenumber";
            return connection.Query<MaskLogData>(sql, new { logid = logid, startsampleno = startsampleno, endsampleno = endsampleno }).ToList();
        }

        public static DecimalValueRange getDecimalLogRange(SqlConnection connection, string logid)
        {
            string sql = @"SELECT min(DECIMALLOGDATA.DECIMALVALUE) minvalue, max(DECIMALLOGDATA.DECIMALVALUE) maxvalue FROM DECIMALLOGDATA WHERE DECIMALLOGDATA.LOG_ID = @logid";
            var parameters = new DynamicParameters();
            parameters.Add("@logid", logid, DbType.AnsiString);
            return connection.Query<DecimalValueRange>(sql, parameters).Single();
        }

        public static byte[] getImageHistogramLUT(SqlConnection connection, string imageid)
        {

            string sql = @"select imghistogram from imagelogs where log_id = @logid";
            byte[] LUTBytes = connection.Query<byte[]>(sql, new { logid = imageid }).Single();

            if (LUTBytes == null || LUTBytes.Length <= 0) return null;

            int previousvalue = 0;
            for (int i = 0; i < 256; i++)
            {
                if ((LUTBytes[i] & 0xFF) < previousvalue) return null;
                previousvalue = LUTBytes[i] & 0xFF;
            }
            if (previousvalue > 0) return LUTBytes;
            else return null;
        }

        public static List<TraySection> getTraySections(SqlConnection connection, string datasetid, int sampleno)
        {
            string sql = @"SELECT sec.samplenumber sectionnumber, sec.startvalue startsampleno,sec.ENDVALUE endsampleno FROM Domainlogdata sec left outer join domainlogdata on sec.STARTVALUE >= domainlogdata.startvalue AND sec.ENDVALUE<= domainlogdata.ENDVALUE inner join domainlogs on Domainlogdata.LOG_ID = domainlogs.log_id inner join logs on domainlogs.log_id=logs.DOMAINLOG_ID inner join logs logs1 on sec.log_id=logs1.DOMAINLOG_ID inner join datasets on datasets.DATASET_ID=logs.dataset_id where logs.DATASET_ID=@datasetid AND logs.LOG_ID=datasets.TRAYLOG_ID AND domainlogdata.samplenumber=@sampleno AND logs1.LOG_ID = datasets.SECTIONLOG_ID order by sec.samplenumber";
            var parameters = new DynamicParameters();
            parameters.Add("datasetid",  datasetid ,DbType.AnsiString);
            parameters.Add("sampleno", sampleno, DbType.Int32);
            return connection.Query<TraySection>(sql, parameters).ToList();
        }

        internal static string getSpectralLogSamplingPoints(SqlConnection connection, string speclogid)
        {
            string sql = @"select SPECTRALSAMPLINGPOINTS wavelengthsbytes From SpectralLogs where log_id = @speclogid";
            return connection.Query<SpectralLog>(sql, new { speclogid = speclogid }).Single().wavelengths;
        }

        internal static List<SpectralData> getSpectralData(SqlConnection connection, string speclogid, int startsampleno, int endsampleno)
        {
            string blobStoreConStr = System.Environment.GetEnvironmentVariable("BinaryDataBlobStoreConStr", EnvironmentVariableTarget.Process);
            string blobStoreContainerName = System.Environment.GetEnvironmentVariable("BinaryDataBlobStoreContainerName", EnvironmentVariableTarget.Process);

            if (string.IsNullOrEmpty(blobStoreConStr))
            {
                string sql = @"select samplenumber sampleno, spectralvalues floatspectraldata from spectrallogdata where log_id = @speclogid and samplenumber between @startsampleno and @endsampleno order by samplenumber";
                return connection.Query<SpectralData>(sql, new { speclogid = speclogid, startsampleno = startsampleno, endsampleno = endsampleno }).ToList();
            }
            else
            {
                var speclogdetails = NVCLDSDataAccess.getLogDetails(connection, speclogid);

                List<SpectralData> spectralist = new List<SpectralData>();

                if (!string.IsNullOrEmpty(speclogdetails.DatasetID) && !string.IsNullOrEmpty(speclogid))
                {

                    for (int i = startsampleno; i <= endsampleno; i++)
                    {

                        String blobName = "tsgdataset-" + speclogdetails.DatasetID + "/spectralLogData/" + speclogid + "/" + i + ".bin";

                        var blobServiceClient = new BlobServiceClient(blobStoreConStr);

                        BlobClient blobClient = blobServiceClient.GetBlobContainerClient(blobStoreContainerName).GetBlobClient(blobName);

                        SpectralData spectralData = new SpectralData();
                        spectralData.sampleNo = i;
                        if (blobClient.ExistsAsync().Result)
                        {
                            using var ms = new MemoryStream();
                            blobClient.DownloadTo(ms);
                            spectralData.floatspectraldata = ms.ToArray();
                        }
                        spectralist.Add(spectralData);
                    }

                }
                return spectralist;

            }
        }

        public static List<Dataset> getdatasets(SqlConnection connection, string holeid, string datasetid, Boolean headersonly)
        {
            IEnumerable<Dataset> datasets = new List<Dataset>();
            List<Dataset> DatasetCollection = new List<Dataset>();
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

                    using (var multi = connection.QueryMultiple(sql, new { logid = ds.domain_id, datasetid = ds.DatasetID }))
                    {
                        ds.DepthRange = multi.Read<DepthRange>().Single();
                        ds.SpectralLogs = multi.Read<SpectralLog>().ToList();
                        ds.ImageLogs = multi.Read<Log>().ToList();
                        ds.Logs = multi.Read<Log>().ToList();
                        ds.ProfilometerLogs = multi.Read<ProfLog>().ToList();
                    }

                }
            }
            return DatasetCollection;
        }

        public static string isdatasetsLocallyCached(Dataset ds)
        {
            string cacheUrl = System.Environment.GetEnvironmentVariable("TSGFileCachePublicUrl", EnvironmentVariableTarget.Process);
            string blobstoreConStr = System.Environment.GetEnvironmentVariable("TSGFileCacheBlobStoreConStr", EnvironmentVariableTarget.Process);
            string blobstoreConStrContainerName = System.Environment.GetEnvironmentVariable("TSGFileCacheBlobStoreContainerName", EnvironmentVariableTarget.Process);

            if (!string.IsNullOrEmpty(blobstoreConStr) && !string.IsNullOrEmpty(blobstoreConStrContainerName) && !string.IsNullOrEmpty(ds.datasetname))
            {
                String blobName = ds.datasetname + ".zip";

                var blobServiceClient = new BlobServiceClient(blobstoreConStr);

                var blobClient = blobServiceClient.GetBlobContainerClient(blobstoreConStrContainerName).GetBlobClient(blobName);

                if (blobClient.ExistsAsync().Result && blobClient.GetProperties().Value.LastModified >= ds.modifieddate)
                {
                    return cacheUrl + blobName;
                }
                else
                {
                    String blobNamedsid = ds.DatasetID + ".zip";
                    var blobClientdsid = blobServiceClient.GetBlobContainerClient(blobstoreConStrContainerName).GetBlobClient(blobNamedsid);
                    if (blobClientdsid.ExistsAsync().Result && blobClientdsid.GetProperties().Value.LastModified >= ds.modifieddate)
                    {
                        return cacheUrl + blobNamedsid;
                    }

                }
            }
            return null;
        }

        public static string isdatasetsCachedbyNVCL(Dataset ds)
        {
            string cacheUrl = System.Environment.GetEnvironmentVariable("NVCLCacheUrl", EnvironmentVariableTarget.Process);
            if (!string.IsNullOrEmpty(cacheUrl))
            {
                try
                {
                    using HttpResponseMessage response = client.Send(new HttpRequestMessage(HttpMethod.Head, cacheUrl + ds.datasetname + ".zip"));
                    if (response.StatusCode == HttpStatusCode.OK && response.Content.Headers.LastModified >= ds.modifieddate)
                    {
                        return cacheUrl + ds.datasetname + ".zip";
                    }
                }
                catch { }
            }
            return null;
        }

        public static string getDownloadLink(Dataset dataset)
        {
            string cacheurl = NVCLDSDataAccess.isdatasetsCachedbyNVCL(dataset);
            if (!string.IsNullOrEmpty(cacheurl))
            {
            return cacheurl;
            }
            cacheurl = NVCLDSDataAccess.isdatasetsLocallyCached(dataset);
            if (!string.IsNullOrEmpty(cacheurl))
            {
            return cacheurl;
            }
            return null;
        }

        internal static bool validateLogId(SqlConnection connection, List<string> scalarids)
        {
            string sql = @"select count(*) validlogids from logs where log_id in @logids ";
            //var parameters = new DynamicParameters();
            //parameters.Add("@logids", scalarids, DbType.AnsiString);
            return (connection.Query<int>(sql, new { logids = scalarids }).Single()==scalarids.Count);
        }

        internal static bool validateDomainlogId(SqlConnection connection, List<string> scalarids)
        {
            string sql = @"select count(distinct domainlog_id) distlogids from logs where log_id in @logids ";
            return (connection.Query<int>(sql, new { logids = scalarids }).Single() == 1);
        }
    }

}

using Dapper;
using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace NVCLDataServicesAZ
{
    internal static class NVCLDSDataAccess
    {
        public static Log getImageLogDetails(SqlConnection connection, string logid)
        {
            string sql = @"select log_id logid, logname, dbo.GETDATAPOINTS(logs.DOMAINLOG_ID) as samplecount, logs.DOMAINLOG_ID DOMAINLOGID, dataset_id datasetid from logs where log_id = @logid and logtype=3";
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
            string sql = @"select publishedimagelogdata.imagedata, imagelogs.imghistogram, imagelogs.imgclippercent from publishedimagelogdata inner join imagelogs on publishedimagelogdata.log_id = imagelogs.log_id where imagelogs.log_id = @logid and publishedimagelogdata.samplenumber = @sampleno";
            return connection.Query<byte[]>(sql, new { logid = imageid, sampleno = sampleno }).Single();
        }
        public static List<TraySection> getTraySections(SqlConnection connection, string datasetid, int sampleno)
        {
            string sql = @"SELECT sec.samplenumber sectionnumber, sec.startvalue startsampleno,sec.ENDVALUE endsampleno FROM Domainlogdata sec left outer join domainlogdata on sec.STARTVALUE >= domainlogdata.startvalue AND sec.ENDVALUE<= domainlogdata.ENDVALUE inner join domainlogs on Domainlogdata.LOG_ID = domainlogs.log_id inner join logs on domainlogs.log_id=logs.DOMAINLOG_ID inner join logs logs1 on sec.log_id=logs1.DOMAINLOG_ID inner join datasets on datasets.DATASET_ID=logs.dataset_id where logs.DATASET_ID=@datasetid AND logs.LOG_ID=datasets.TRAYLOG_ID AND domainlogdata.samplenumber=@sampleno AND logs1.LOG_ID = datasets.SECTIONLOG_ID order by sec.samplenumber";
            return connection.Query<TraySection>(sql, new { datasetid = datasetid,sampleno = sampleno }).ToList();
        }

        internal static string getSpectralLogSamplingPoints(SqlConnection connection, string speclogid)
        {
            string sql = @"select SPECTRALSAMPLINGPOINTS wavelengthsbytes From SpectralLogs where log_id = @speclogid";
            return connection.Query<SpectralLog>(sql, new { speclogid = speclogid }).Single().wavelengths;
        }

        internal static List<SpectralData> getSpectralData(SqlConnection connection, string speclogid, int startsampleno, int endsampleno)
        {
            string sql = @"select samplenumber sampleno, spectralvalues floatspectraldata from spectrallogdata where log_id = @speclogid and samplenumber between @startsampleno and @endsampleno order by samplenumber";
            return connection.Query<SpectralData>(sql, new { speclogid = speclogid, startsampleno= startsampleno, endsampleno = endsampleno }).ToList();
        }
    }
}

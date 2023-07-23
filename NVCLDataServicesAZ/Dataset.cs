using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Security.Policy;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace NVCLDataServicesAZ
{
    public class Dataset
    {
        [XmlElement("DatasetID")]
        public String DatasetID { get; set; }
        private string _boreholeURI;
        public String boreholeURI { get => _holedatasourcename + holeidentifier; set => _boreholeURI = _holedatasourcename + holeidentifier; }
        [XmlElement("DatasetName")]
        public String datasetname { get; set; }
        public String description { get; set; }
        [XmlElement("createdDate")]
        public DateTimeOffset createddate { get; set; }
        [XmlElement("modifiedDate")]
        public DateTimeOffset modifieddate { get; set; }
        [XmlElement("trayID")]
        public String traylog_id { get; set; }
        [XmlElement("sectionID")]
        public String sectionlog_id { get; set; }
        [XmlElement("domainID")]
        public String domain_id { get; set; }
        private String _holedatasourcename;
        public String holedatasourcename { get => null; set => _holedatasourcename = value; }
        private String _holeidentifier;
        public String holeidentifier { get => null; set => _holeidentifier = value; }
        public DepthRange DepthRange { get; set; }
        public List<SpectralLog> SpectralLogs { get; set; }
        public List<Log> ImageLogs { get; set; }
        public List<Log> Logs { get; set; }
        public List<ProfLog> ProfilometerLogs { get; set; }
        public String downloadLink { get; set; }
    }

    public class DepthRange
    {
        public float start { get; set; }
        public float end { get; set; }
    }

    public class SpectralLog
    {
        public String logID { get; set; }
        public String logName { get; set; }
        public String wavelengthUnits { get; set; }
        public String sampleCount { get; set; }
        public String script { get; set; }
        public byte[] wavelengthsbytes
        {
            get => null; set
            {
                var wavelengthstmp = new float[value.Length / 4];
                Buffer.BlockCopy(value, 0, wavelengthstmp, 0, value.Length);
                wavelengths = string.Join(",", wavelengthstmp);
            }
        }

        public String wavelengths { get; set; }

    }

    public class Log
    {
        public String LogID { get; set; }
        public String LogName { get; set; }
        public String SampleCount { get; set; }
        public bool? ispublic { get; set; }
        [System.Xml.Serialization.XmlIgnore]
        public bool ispublicSpecified { get { return this.ispublic != null; } }
        public int? logType { get; set; }
        [System.Xml.Serialization.XmlIgnore]
        public bool logTypeSpecified { get { return this.logType != null; } }
        public int? algorithmoutID { get; set; }
        [System.Xml.Serialization.XmlIgnore]
        public bool algorithmoutIDSpecified { get { return this.algorithmoutID != null; } }
        public String maskLogID { get; set; }
        public String DomainLogID { get; set; }
        public String DatasetID { get; set; }

    }

    public class ProfLog : Log
    {
        public int floatsPerSample { get; set; }
        public float minVal { get; set; }
        public float maxVal { get; set; }
    }

    public class DomainLogData
    {
        public int SAMPLENUMBER { get; set; }
        public float STARTVALUE { get; set; }
        public float ENDVALUE { get; set; }
    }

    public class ImageDataUrl
    {
        public string url { get; set; }
        public int sampleno { get; set; }
    }

    public class TraySection
    {
        public int startsampleno { get; set; }
        public int endsampleno { get; set; }
        public int sectionnumber { get; set; }
    }

    public class SpectralData
    {
        public int sampleNo { get; set; }
        public byte[] floatspectraldata { get; set; }
    }

    public class DownloadRequestParams
    {
        public string datasetid { get; set; }
        public string email { get; set; }
    }
}

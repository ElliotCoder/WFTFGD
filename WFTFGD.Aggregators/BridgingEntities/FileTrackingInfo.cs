using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Security.Permissions;
using System.Text;
using System.Threading.Tasks;

namespace WFTFGD.Aggregators.BridgingEntities
{
    [Serializable]
    public class FileTrackingInfo
    {
        public String MIMEType          { get; set; }
        public String UserPCLocation    { get; set; }
        public Boolean AutomaticTrackingEnabled { get; set; }

        public FileTrackingInfo(String userPCLocation, String mimeType)
        {
            MIMEType        = mimeType;
            UserPCLocation  = userPCLocation;
            AutomaticTrackingEnabled = false;
        }

        public FileTrackingInfo()
        {
        }
    }
}

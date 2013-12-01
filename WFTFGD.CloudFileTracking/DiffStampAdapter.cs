using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WFTFGD.Adapters
{
    [Serializable]
    public class DiffStampAdapter
    {
        private Byte[] _data;
        private String _description;
        private DateTime _dateTimeCreated;

        public DiffStampAdapter(Byte[] data, String description, DateTime dateTimeCreated)
        {
            _data = data;
            _description = description;
            _dateTimeCreated = dateTimeCreated;
        }

        public Byte[] Data
        {
            get { return _data; }
        }

        public String Description
        {
            get { return _description; }
            set { _description = value; }
        }

        public DateTime DateTimeCreated
        {
            get { return _dateTimeCreated; }
            set { _dateTimeCreated = value; }
        }

        public Stream OpenMemoryStreamForPatching()
        {
            return new MemoryStream(_data);
        }
    }
}

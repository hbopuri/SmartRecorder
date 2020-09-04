using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SmartRecorder
{
    public enum SmartRecorderPosition
    {
        TopLeft,
        TopCenter,
        TopRight,
        BottomLeft,
        BottomCenter,
        BottomRight
    }
    public enum SmartVideoQuality
    {
        Best,
        Medium,
        Low
    }
    public class SmartRecorderSettings
    {
        public Guid ProjectUid { get; set; }
        public SmartRecorderPosition Position { get; set; }
        public string Camera { get; set; }
        public bool CaptureAudio { get; set; }
        public bool PrintTimeStamp { get; set; }
        public SmartVideoQuality Quality { get; set; }
        public string OutputPath { get; set; }
        public Guid SessionKey { get; set; }
    }
}

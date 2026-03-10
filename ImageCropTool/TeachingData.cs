using System.Collections.Generic;
using System.Drawing;

namespace ImageCropTool
{
    class TeachingData
    {
        public string ImagePath { get; set; }
        public List<BaseLineData> Lines { get; set; } = new List<BaseLineData>();
    }

    class BaseLineData
    {
        public PointF StartPt { get; set; }
        public PointF EndPt { get; set; }
        public int CropSize { get; set; }
        public CropAnchor Anchor { get; set; }
    }
}

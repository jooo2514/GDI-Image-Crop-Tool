using System.Collections.Generic;
using System.Drawing;

namespace ImageCropTool
{
    class BaseLineInfo
    {
        // 기준선
        public PointF StartPt;     // original 좌표
        public PointF EndPt;

        // 크롭 설정
        public int CropSize;
        public CropAnchor Anchor;

        // 이 기준선에서 생성된 크롭박스들
        public List<CropBoxInfo> CropBoxes = new List<CropBoxInfo>();
    }
}

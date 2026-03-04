using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Drawing;

namespace ImageCropTool
{
    class CropBoxInfo
    {
        public Rectangle EffectiveRect;  // 실제 crop 영역(original 좌표)
        public bool IsHovered;           // 마우스가 위에 올라가 있는지
        public BaseLineInfo OwnerLine;   // 어떤 기준선에서 생성되었는지 저장
    }
}

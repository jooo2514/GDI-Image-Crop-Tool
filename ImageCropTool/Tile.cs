using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Drawing;


namespace ImageCropTool
{
    class Tile
    {
        public Bitmap Bitmap;
        public Rectangle Rect;
        public long LastUsed;   // 최근 사용 시간
    }
}

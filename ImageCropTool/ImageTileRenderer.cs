using OpenCvSharp;
using OpenCvSharp.Extensions;
using System;
using System.Collections.Generic;
using System.Drawing;

using DPoint = System.Drawing.Point;
using DSize = System.Drawing.Size;

namespace ImageCropTool
{
    class ImageTileRenderer
    {
        const int TileSize = 512;

        private List<Mat> pyramidLevels;
        private Dictionary<(int level, int x, int y), Tile> tileCache
            = new Dictionary<(int, int, int), Tile>();

        public float ViewScale;
        public float BaseScale;
        public PointF ViewOffset;

        public ImageTileRenderer(List<Mat> pyramid)
        {
            pyramidLevels = pyramid;
        }

        Tile GetTile(int level, int tileX, int tileY)
        {
            var key = (level, tileX, tileY);

            if (tileCache.TryGetValue(key, out Tile tile))
                return tile;

            Mat src = pyramidLevels[level];

            int x = tileX * TileSize;
            int y = tileY * TileSize;

            if (x < 0 || y < 0 || x >= src.Width || y >= src.Height)
                return null;

            int w = Math.Min(TileSize, src.Width - x);
            int h = Math.Min(TileSize, src.Height - y);

            if (w <= 0 || h <= 0)
                return null;

            var roi = new OpenCvSharp.Rect(x, y, w, h);

            using (Mat cropped = new Mat(src, roi))
            {
                Bitmap bmp = BitmapConverter.ToBitmap(cropped);

                tile = new Tile
                {
                    Bitmap = bmp,
                    Rect = new Rectangle(x, y, w, h)
                };

                tileCache[key] = tile;
                return tile;
            }
        }

        public void Draw(Graphics g, DSize viewport, int level,
                   Func<PointF, PointF> OriginalToScreen,
                   Func<DPoint, PointF> ScreenToOriginal)
        {
            float levelScale = (float)Math.Pow(2, level);

            PointF tl = ScreenToOriginal(new DPoint(0, 0));
            PointF br = ScreenToOriginal(new DPoint(viewport.Width, viewport.Height));

            int startX = (int)Math.Floor(tl.X / (TileSize * levelScale));
            int endX = (int)Math.Floor(br.X / (TileSize * levelScale));

            int startY = (int)Math.Floor(tl.Y / (TileSize * levelScale));
            int endY = (int)Math.Floor(br.Y / (TileSize * levelScale));

            for (int ty = startY; ty <= endY; ty++)
            {
                for (int tx = startX; tx <= endX; tx++)
                {
                    Tile tile = GetTile(level, tx, ty);
                    if (tile == null) continue;

                    float ox = tile.Rect.Left * levelScale;
                    float oy = tile.Rect.Top * levelScale;
                    float ow = tile.Rect.Width * levelScale;
                    float oh = tile.Rect.Height * levelScale;

                    PointF s = OriginalToScreen(new PointF(ox, oy));
                    PointF e = OriginalToScreen(new PointF(ox + ow, oy + oh));

                    g.DrawImage(tile.Bitmap,
                        s.X - 0.5f,
                        s.Y - 0.5f,
                        (e.X - s.X) + 1,
                        (e.Y - s.Y) + 1);
                }
            }
        }
        public void Dispose()
        {
            if (tileCache == null) return;

            foreach (var tile in tileCache.Values)
            {
                // tile 객체 안에 있는 'Bitmap' 변수를 직접 찾아 Dispose 합니다.
                if (tile != null && tile.Bitmap != null)
                {
                    tile.Bitmap.Dispose();
                }
            }
            tileCache.Clear();
        }

    }
}
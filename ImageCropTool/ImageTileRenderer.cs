using OpenCvSharp;
using OpenCvSharp.Extensions;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;          // OrderBy
using System.Diagnostics;   // Stopwatch

using DPoint = System.Drawing.Point;
using DSize = System.Drawing.Size;

namespace ImageCropTool
{
    class ImageTileRenderer
    {
        const int TileSize = 512;
        const int MAX_TILE_CACHE = 300;  // 캐시 크기 제한

        private List<Mat> pyramidLevels;

        private Dictionary<(int level, int x, int y), Tile> tileCache   // 타일 캐시
            = new Dictionary<(int, int, int), Tile>();

        public float ViewScale;     // 줌배율
        public PointF ViewOffset;   // 이미지 이동 좌표

        public ImageTileRenderer(List<Mat> pyramid)  // 참조보관
        {
            pyramidLevels = pyramid;
        }

        Tile GetTile(int level, int tileX, int tileY)   // 타일 캐시 불러오기 및 저장
        {
            var key = (level, tileX, tileY);  // key 생성

            if (tileCache.TryGetValue(key, out Tile tile))   // 타일 찾기
            {
                tile.LastUsed = Stopwatch.GetTimestamp();
                return tile;
            }

            Mat src = pyramidLevels[level];   // 피라미드 이미지 선택

            int x = tileX * TileSize;   // 타일 좌표 계산
            int y = tileY * TileSize;

            if (x < 0 || y < 0 || x >= src.Width || y >= src.Height)   // 범위 체크
                return null;

            int w = Math.Min(TileSize, src.Width - x);    // 실제 타일 크기 계산
            int h = Math.Min(TileSize, src.Height - y);

            if (w <= 0 || h <= 0)
                return null;

            var roi = new OpenCvSharp.Rect(x, y, w, h);   // ROI 생성

            using (Mat cropped = new Mat(src, roi))   // 타일 이미지 생성
            {
                Bitmap bmp = BitmapConverter.ToBitmap(cropped);

                tile = new Tile    // 타일 객체 생성
                {
                    Bitmap = bmp,
                    Rect = new Rectangle(x, y, w, h),
                    LastUsed = Stopwatch.GetTimestamp()
            };

                tileCache[key] = tile;   // 캐시에 저장
            }

            CleanupTileCache();

            return tile;
        }

        void CleanupTileCache()
        {
            if (tileCache.Count <= MAX_TILE_CACHE)
                return;

            var ordered = tileCache
                .OrderBy(t => t.Value.LastUsed)   // 마지막으로 사용된 시간 기준으로 정렬
                .Take(tileCache.Count - MAX_TILE_CACHE)   // 삭제 대상 리스트 만듬+
                .ToList();

            foreach (var item in ordered)
            {
                item.Value.Bitmap.Dispose();    // 자원해제
                tileCache.Remove(item.Key);     // 제거
            }
        }

        public void Draw(Graphics g, DSize viewport, int level,
                   Func<PointF, PointF> OriginalToScreen,
                   Func<DPoint, PointF> ScreenToOriginal)
        {
            float levelScale = (float)Math.Pow(2, level);       // 현재 피라미드 층이 원본보다 몇 배 작은지 계산 (1, 2, 4, 8...)

            PointF tl = ScreenToOriginal(new DPoint(0, 0)); ;   // 화면 왼쪽 위가 원본의 어디인지 계산
            PointF br = ScreenToOriginal(new DPoint(viewport.Width, viewport.Height));  // 화면 오른쪽 아래가 어디인지 계산

            int startX = (int)Math.Floor(tl.X / (TileSize * levelScale)); // 그 범위에 포함되는 첫 번째 타일 번호
            int endX = (int)Math.Floor(br.X / (TileSize * levelScale));  // 마지막 타일 번호

            int startY = (int)Math.Floor(tl.Y / (TileSize * levelScale));
            int endY = (int)Math.Floor(br.Y / (TileSize * levelScale));

            for (int ty = startY; ty <= endY; ty++)
            {
                for (int tx = startX; tx <= endX; tx++)
                {
                    Tile tile = GetTile(level, tx, ty);     // 필요한 조각 하나씩 가져오기
                    if (tile == null) continue;

                    // 원본 좌표 계산
                    float ox = tile.Rect.Left * levelScale;   // pyramid 이미지 좌표 → 원본 좌표 변환
                    float oy = tile.Rect.Top * levelScale;
                    float ow = tile.Rect.Width * levelScale;
                    float oh = tile.Rect.Height * levelScale;

                    PointF s = OriginalToScreen(new PointF(ox, oy));
                    PointF e = OriginalToScreen(new PointF(ox + ow, oy + oh));

                    g.DrawImage(tile.Bitmap,    // 화면에 그리기
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
                if (tile != null && tile.Bitmap != null)  // 비트맵 자원 해제
                {
                    tile.Bitmap.Dispose();
                }
            }
            tileCache.Clear();  // 보관함 비우기
        }
    }
}
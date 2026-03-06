using OpenCvSharp;
using OpenCvSharp.Extensions;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Diagnostics;

// 충돌 방지 별칭
using DPoint = System.Drawing.Point;

namespace ImageCropTool
{
    public partial class MainForm : Form
    {
        private List<Mat> pyramidLevels = new List<Mat>();
        private List<Bitmap> pyramidBitmaps = new List<Bitmap>();   // 레벨별 Bitmap 캐시 (Format32bppPArgb)
        private List<IntPtr> pyramidHBitmaps = new List<IntPtr>(); // GDI StretchBlt용 HBITMAP 캐시
        private int currentPyramidLevel = 0;


        /* ========= Context Menu ========= */
        private ContextMenuStrip lineContextMenu;
        private BaseLineInfo contextTargetLine = null;  // 우클릭 대상

        private ToolStripMenuItem deleteLineItem;
        private ToolStripMenuItem resetviewItem;
        private ToolStripMenuItem lineResetItem;

        /* ======== Line / Crop Info ======== */
        private const int DefaultCropSize = 512;
        private List<BaseLineInfo> baseLines = new List<BaseLineInfo>();   // 모든 기준선 목록
        private BaseLineInfo currentLine = null;                           // 현재 그리고 있는 기준선
        private CropBoxInfo hoveredBox = null;                             // hover된 크롭박스 (모든 기준선 통합)

        /* ======== Image ========== */
        private Bitmap displayBitmap = null;
        private Mat originalMat;
        private string imageColorInfoText = string.Empty;

        private Bitmap highZoomCache = null;
        private Rectangle lastHighZoomSrcRect;

        /* ======== Image ========== */
        private Bitmap miniMapBitmap;
        private float miniMapScale;

        /* ========== Loading Spinner ============ */
        private bool isImageLoading = false;
        private Timer loadingTimer;
        private float spinnerAngle = 0f;

        /* ========= Drag / Click State ========== */
        private enum ClickState { None, OnePoint }
        private ClickState clickState = ClickState.None;
        private const int HitRadius = 8;

        /* ========== Drag ========== */
        private BaseLineInfo draggingLine = null;
        private enum DragTarget
        {
            None,
            StartPoint,
            EndPoint
        }
        private DragTarget dragTarget = DragTarget.None;

        /* ========= Mouse Position Display =========== */
        private PointF mouseOriginalPt;                  // 표시할 이미지 좌표
        private DPoint mouseScreenPt;                    // 텍스트를 그릴 화면 위치

        /* ========= Crop Anchor ========== */
        private CropAnchor cropAnchor = CropAnchor.Center;

        /* ========== View Transform (Zoom & Pan) ============ */
        private float viewScale = 1.0f;                 // 줌 배율
        private float displayBaseScale = 1.0f;          // 화면에 맞추기 위한 기본 축소 비율

        private PointF viewOffset = new PointF(0, 0);   // 이미지 시작 위치

        private const float ZoomStep = 1.1f;            // 휠 한칸에 10%씩 변화
        private const float MinZoom = 0.2f;
        private const float MaxZoom = 100.0f;
        private Bitmap zoomOutCacheBitmap = null;


        private bool isPanning = false;
        private DPoint lastMousePt;

        /* ========= GDI 렌더링 (StretchBlt) ========== */
        private const uint SRCCOPY = 0x00CC0020;   // 비트맵 복사방식옵션 : 원본 그대로 복사

        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateCompatibleDC(IntPtr hdc);   // 메모리용 DC 생성
        [DllImport("gdi32.dll")]
        private static extern IntPtr SelectObject(IntPtr hdc, IntPtr hgdiobj);   // DC와 비트맵 연결
        [DllImport("gdi32.dll")]
        private static extern bool DeleteDC(IntPtr hdc);   // DC 메모리 해제
        [DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr hObject);   // HBITMAP 제거
        [DllImport("gdi32.dll")]
        private static extern bool StretchBlt(IntPtr hdcDest, int xDest, int yDest, int wDest, int hDest,
            IntPtr hdcSrc, int xSrc, int ySrc, int wSrc, int hSrc, uint rop);   // 원본이미지 크기 수정 후 화면 그림
        [DllImport("gdi32.dll")]
        private static extern int SetStretchBltMode(IntPtr hdc, int mode);    // 이미지 확대/축소 품질 설정
        [DllImport("gdi32.dll")]
        private static extern bool SetBrushOrgEx(IntPtr hdc, int x, int y, IntPtr pt);  // HALFTONE 쓸 때 반드시 같이 써야 하는 설정

        private const int STRETCH_HALFTONE = 4;   // 최고 품질 (부드럽게 확대됨)


        /* ========= 생성자 =========== */
        public MainForm()
        {
            InitializeComponent();

            loadingTimer = new Timer { Interval = 50 };
            loadingTimer.Tick += (s, e) =>
            {
                spinnerAngle = (spinnerAngle + 20) % 360;
                pictureBoxImage.Invalidate();
            };

            pictureBoxImage.SizeMode = PictureBoxSizeMode.Normal;
            pictureBoxImage.Paint += PictureBoxImage_Paint;
            pictureBoxImage.MouseDown += PictureBoxImage_MouseDown;
            pictureBoxImage.MouseMove += PictureBoxImage_MouseMove;
            pictureBoxImage.MouseUp += PictureBoxImage_MouseUp;
            pictureBoxImage.MouseWheel += PictureBoxImage_MouseWheel;

            pictureBoxPreview.SizeMode = PictureBoxSizeMode.Zoom;
            numCropSize.Value = DefaultCropSize;

            pictureBoxMiniMap.Paint += PictureBoxMiniMap_Paint;

            this.FormClosing += (s, e) => DisposeResources();

            // 우클릭 메뉴 객체 생성
            lineContextMenu = new ContextMenuStrip();

            deleteLineItem = new ToolStripMenuItem("라인 삭제");
            lineContextMenu.Items.Add(deleteLineItem);

            resetviewItem = new ToolStripMenuItem("줌핏");
            lineContextMenu.Items.Add(resetviewItem);


            lineResetItem = new ToolStripMenuItem("라인 초기화");
            lineContextMenu.Items.Add(lineResetItem);

            deleteLineItem.Click += (s, e) => DeleteLine(contextTargetLine);
            resetviewItem.Click += (s, e) => ResetView();
            lineResetItem.Click += (s, e) => LineReset();
        }


        private string GetMemoryInfo()
        {
            Process p = Process.GetCurrentProcess();

            long workingSet = p.WorkingSet64;               // RAM 사용량
            long privateBytes = p.PrivateMemorySize64;      // 실제 Commit
            long managed = GC.GetTotalMemory(false);        // Managed heap
            //long inPageFile = privateBytes - workingSet;     // 가상메모리 데이터                                           // 

            return $"WS: {workingSet / 1024 / 1024} MB | " +
                   $"Private: {privateBytes / 1024 / 1024} MB";
                   //$"Managed: {managed / 1024 / 1024} MB";
        }


        /* =========================================================
         *  Reset
         * ========================================================= */
        private void BtnReset_Click(object sender, EventArgs e) => DataReset();

        private void DisposeResources()  // 이미지 리소스 전부 해제
        {
            originalMat?.Dispose();
            originalMat = null;

            displayBitmap?.Dispose();
            displayBitmap = null;

            highZoomCache?.Dispose();
            highZoomCache = null;

            zoomOutCacheBitmap?.Dispose();
            zoomOutCacheBitmap = null;

            DisposePyramidCaches();

            pictureBoxImage.Image?.Dispose();
            pictureBoxImage.Image = null;

            pictureBoxPreview.Image?.Dispose();
            pictureBoxPreview.Image = null;

            pictureBoxMiniMap.Image?.Dispose();
            pictureBoxMiniMap.Image = null;

        }

        private void DataReset()
        {
            isImageLoading = true;
            LineReset();
            DisposeResources();
            isImageLoading = false;
        }

        private void LineReset()
        {
            // 기준선/점/크롭 전체 제거
            baseLines.Clear();
            clickState = ClickState.None;
            currentLine = null;

            // 드래그/hover 상태 초기화
            draggingLine = null;
            dragTarget = DragTarget.None;
            hoveredBox = null;

            // UI 초기화
            ClearPreview();

            // Line info 초기화
            lblLineIndex.Text = "Line Index: -";
            lblLineLength.Text = "Line Length: -";
            lblCropSize.Text = "Crop Size: -";
            lblCropCount.Text = "Crop Count: -";

            // Crop size는 기본값으로
            numCropSize.Value = DefaultCropSize;
            zoomOutCacheBitmap?.Dispose();

            pictureBoxMiniMap.Invalidate();
            pictureBoxImage.Invalidate();
        }

        private void ResetView()    // 중앙정렬
        {
            if (displayBitmap == null)
                return;

            viewScale = 1.0f;

            viewOffset = new PointF(
                (pictureBoxImage.Width - displayBitmap.Width) / 2f,
                (pictureBoxImage.Height - displayBitmap.Height) / 2f
            );
            pictureBoxMiniMap.Invalidate();
        }

        private void NumCropSize_ValueChanged(object sender, EventArgs e)
        {
            if (currentLine != null)
            {
                currentLine.CropSize = (int)numCropSize.Value;
            };
            pictureBoxImage.Invalidate();
        }

        private void DeleteLine(BaseLineInfo line)
        {
            if (line == null)
                return;

            baseLines.Remove(line);   // TargetLine 제거

            // hover / drag 상태 정리
            if (hoveredBox != null && hoveredBox.OwnerLine == line)
                hoveredBox = null;
            if (draggingLine == line)
            {
                draggingLine = null;
                dragTarget = DragTarget.None;
            }
            UpdateLineInfo(null);
            pictureBoxMiniMap.Invalidate();
            pictureBoxImage.Invalidate();
        }


        /* =========================================================
         *  Image Load
         * ========================================================= */
        private async void BtnLoadImage_Click(object sender, EventArgs e)
        {
            OpenFileDialog dlg = new OpenFileDialog
            {
                Filter = "Image Files|*.bmp;*.jpg;*.png"
            };

            if (dlg.ShowDialog() != DialogResult.OK)
                return;

            isImageLoading = true;
            loadingTimer.Start();

            pictureBoxImage.Enabled = false;
            btnCropSave.Enabled = false;
            btnLoadImage.Enabled = false;
            btnReset.Enabled = false;

            try
            {
                await Task.Run(() =>
                {
                    originalMat?.Dispose();

                    //originalBitmap = new Bitmap(dlg.FileName);
                    //originalMat = BitmapConverter.ToMat(originalBitmap);  // 연산용

                    originalMat = Cv2.ImRead(dlg.FileName, ImreadModes.Unchanged);


                    // 이미지 타입 판별
                    if (originalMat.Channels() == 1)
                        imageColorInfoText = "Grayscale (CV_8UC1)";
                    else if (originalMat.Channels() == 3)
                        imageColorInfoText = "Color (CV_8UC3)";
                    else
                        imageColorInfoText = $"Channels: {originalMat.Channels()}";

                });
                CreateDisplayBitmap();
                LineReset();
                BuildPyramid();
                CreateMiniMap();
            }
            catch (Exception ex)
            {
                MessageBox.Show("이미지 로드 실패: " + ex.Message);
            }
            finally
            {
                isImageLoading = false;
                loadingTimer.Stop();

                pictureBoxImage.Enabled = true;
                btnCropSave.Enabled = true;
                btnLoadImage.Enabled = true;
                btnReset.Enabled = true;

                pictureBoxMiniMap.Invalidate();
                pictureBoxImage.Invalidate();
            }
        }

        private void CreateDisplayBitmap()   // 렌더링용 기준 비트맵 만들기
        {
            if (originalMat == null)
                return;

            // 원본 비율 계산
            float scaleX = (float)pictureBoxImage.Width / originalMat.Width;
            float scaleY = (float)pictureBoxImage.Height / originalMat.Height;

            displayBaseScale = Math.Min(scaleX, scaleY);   // 작은값 선택 비율유지

            int w = (int)(originalMat.Width * displayBaseScale);  // 화면에 그릴 크기 계산
            int h = (int)(originalMat.Height * displayBaseScale);

            using (Mat resized = new Mat())
            {
                Cv2.Resize(originalMat, resized,
                    new OpenCvSharp.Size(w, h),
                    0, 0,
                    InterpolationFlags.Area);

                if (displayBitmap != null)
                {
                    displayBitmap.Dispose(); // 기존 비트맵 해제
                }

                displayBitmap?.Dispose();
                displayBitmap = BitmapConverter.ToBitmap(resized);   // 비트맵으로 변환
            }
            ResetView();  // 중앙정렬
        }

        private void CreateMiniMap()
        {
            if (originalMat == null)
                return;

            int maxSize = 200;   // 최대크기

            float scaleX = (float)maxSize / originalMat.Width;
            float scaleY = (float)maxSize / originalMat.Height;

            miniMapScale = Math.Min(scaleX, scaleY);

            int w = (int)(originalMat.Width * miniMapScale);
            int h = (int)(originalMat.Height * miniMapScale);

            using (Mat resized = new Mat())
            {
                Cv2.Resize(
                    originalMat,
                    resized,
                    new OpenCvSharp.Size(w, h),
                    0,
                    0,
                    InterpolationFlags.Area
                );

                miniMapBitmap?.Dispose();
                miniMapBitmap = BitmapConverter.ToBitmap(resized);
            }
        }

        /* =========================================================
         *  pyramid
         * ========================================================= */
        private void DisposePyramidCaches()
        {
            // Mat 먼저 정리
            foreach (var mat in pyramidLevels) mat?.Dispose();
            pyramidLevels.Clear();

            // Bitmap 정리
            foreach (var bmp in pyramidBitmaps) bmp?.Dispose();
            pyramidBitmaps.Clear();

            // HBITMAP 정리
            foreach (var h in pyramidHBitmaps)
            {
                if (h != IntPtr.Zero) DeleteObject(h);
            }
            pyramidHBitmaps.Clear();
        }

        private static Bitmap ToBitmap32bppPArgb(Mat mat)   // 빠르게 그릴 수 있는 bitmap 생성
        {
            using (Bitmap temp = BitmapConverter.ToBitmap(mat))  // Mat → Bitmap
            {
                var bmp = new Bitmap(temp.Width, temp.Height, PixelFormat.Format32bppPArgb);   // 도화지 생성
                using (Graphics gr = Graphics.FromImage(bmp))    // 그릴 준비
                    gr.DrawImage(temp, 0, 0);   // temp 이미지 그리기
                return bmp;
            }
        }

        private void BuildPyramid()    // 원본 이미지를 여러 해상도로 미리 만들기
        {
            DisposePyramidCaches();
            pyramidLevels.Clear();

            Mat current = originalMat.Clone();   // 원본 복사
            pyramidLevels.Add(current);          // 피라미드 0단계 : 원본

            // 축소
            while (current.Width > 512 && current.Height > 512)
            {
                Mat down = new Mat();
                Cv2.PyrDown(current, down);  // 가우시안 필터 + 1/2 다운샘플링
                pyramidLevels.Add(down);
                current = down;             // 다음 층을 위해 현재 이미지 갱신
            }

            // 레벨별 Bitmap 캐시 (Format32bppPArgb) 및 HBITMAP 캐시 생성
            for (int i = 0; i < pyramidLevels.Count; i++)
            {
                Bitmap bmp = ToBitmap32bppPArgb(pyramidLevels[i]);   // Mat → Bitmap 변환
                pyramidBitmaps.Add(bmp);
                IntPtr hBmp = bmp.GetHbitmap(); // Bitmap → Win32용 HBITMAP으로 변환
                pyramidHBitmaps.Add(hBmp);
            }
        }

        private Mat GetBestLevel(out float levelScale)  // 상황에 맞는 층 고르기
        {
            float totalScale = viewScale * displayBaseScale;   // 실제 화면 배율 계산

            // 초기값
            int level = 0;
            float scale = totalScale;

            // 현재 배율이 절반(0.5)보다 작다면 한 단계 더 작은 이미지(피라미드 다음 층)를 선택
            while (scale < 0.5f && level < pyramidLevels.Count - 1)
            {
                scale *= 2;  // 이미지가 반토막 났으니 그릴 때 스케일은 2배로 보정
                level++;
            }

            levelScale = scale;         // 실제로 화면에 그릴 크기
            currentPyramidLevel = level; // 현재 몇 번째 층을 쓰는지 저장

            return pyramidLevels[level];
        }
        private void UpdateHighZoomCache()
        {
            // 원본 비트맵이 없거나 컨트롤 크기가 정상이 아닐 때 즉시 리턴
            if (originalMat == null || pictureBoxImage.Width <= 0 || pictureBoxImage.Height <= 0)
                return;

            if (viewScale <= 2.0f)   // 확대 2배 이상일때만
                return;

            try
            {
                // 화면에 실제로 보이는 원본이미지 영역 계산
                PointF topLeft = ScreenToOriginal(new DPoint(0, 0));
                PointF bottomRight = ScreenToOriginal(
                    new DPoint(pictureBoxImage.Width, pictureBoxImage.Height)
                );

                int x = (int)Math.Max(0, Math.Min(originalMat.Width - 1, Math.Floor(topLeft.X)));
                int y = (int)Math.Max(0, Math.Min(originalMat.Height - 1, Math.Floor(topLeft.Y)));

                // 폭과 높이가 0보다 큰지 확인
                int w = (int)Math.Min(originalMat.Width - x, Math.Ceiling(bottomRight.X - topLeft.X));
                int h = (int)Math.Min(originalMat.Height - y, Math.Ceiling(bottomRight.Y - topLeft.Y));

                if (w <= 0 || h <= 0) return;

                Rectangle srcRect = new Rectangle(x, y, w, h);

                // 이전 캐시 해제
                highZoomCache?.Dispose();

                // roi 생성
                var roi = new OpenCvSharp.Rect(srcRect.X, srcRect.Y, srcRect.Width, srcRect.Height);
                using (Mat cropped = new Mat(originalMat, roi))    // 원본 Mat 참조
                {
                    highZoomCache?.Dispose();
                    highZoomCache = BitmapConverter.ToBitmap(cropped);
                }
                lastHighZoomSrcRect = srcRect;  // 같은 영역이면 재사용
            }
            catch (Exception ex)
            {
                Debug.WriteLine("HighZoomCache 생성 실패: " + ex.Message);
                highZoomCache = null;
            }
        }

        /* =========================================================
         *  Mouse Down
         * ========================================================= */
        private void PictureBoxImage_MouseDown(object sender, MouseEventArgs e)
        {
            if (displayBitmap == null || originalMat == null)
                return;

            PointF originalPt = ScreenToOriginal(e.Location);

            switch (e.Button)
            {
                case MouseButtons.Right:
                    {
                        bool hitBox = false;

                        // 크롭박스 위에서 우클릭했는지 검사
                        foreach (var line in baseLines)
                        {
                            foreach (var box in line.CropBoxes)
                            {
                                if (box.EffectiveRect.Contains(
                                    (int)originalPt.X,
                                    (int)originalPt.Y))
                                {
                                    contextTargetLine = line;
                                    deleteLineItem.Enabled = true;
                                    lineContextMenu.Show(
                                        pictureBoxImage,
                                        e.Location
                                    );
                                    hitBox = true;
                                    break;
                                }
                            }
                            if (hitBox)
                                break;
                        }
                        if (!hitBox)
                        {
                            contextTargetLine = null;
                            deleteLineItem.Enabled = false;
                            lineContextMenu.Show(pictureBoxImage, e.Location);
                        }

                        isPanning = true;
                        lastMousePt = e.Location;
                        return;
                    }

                case MouseButtons.Left:
                    {

                        if (!IsInsideImageScreen(e.Location))
                            return;

                        // 기존 기준선 점 드래그 검사
                        foreach (var line in baseLines)
                        {
                            if (IsHitOriginal(e.Location, line.StartPt))
                            {
                                draggingLine = line;
                                dragTarget = DragTarget.StartPoint;
                                return;
                            }

                            if (IsHitOriginal(e.Location, line.EndPt))
                            {
                                draggingLine = line;
                                dragTarget = DragTarget.EndPoint;
                                return;
                            }
                        }

                        // 새 기준선 시작/완성
                        if (clickState == ClickState.None)
                        {
                            currentLine = new BaseLineInfo
                            {
                                StartPt = originalPt,
                                CropSize = (int)numCropSize.Value,
                                Anchor = cropAnchor
                            };

                            clickState = ClickState.OnePoint;
                        }
                        else if (clickState == ClickState.OnePoint)
                        {
                            currentLine.EndPt = originalPt;

                            CalculateCropBoxes(currentLine);
                            baseLines.Add(currentLine);

                            currentLine = null;
                            clickState = ClickState.None;
                        }
                        pictureBoxMiniMap.Invalidate();
                        pictureBoxImage.Invalidate();
                        break;
                    }

                case MouseButtons.Middle:
                    {
                        isPanning = true;
                        lastMousePt = e.Location;
                        pictureBoxImage.Cursor = Cursors.Hand;
                        return;
                    }
            }
        }

        /* =========================================================
         *  Mouse Move
         * ========================================================= */
        private void PictureBoxImage_MouseMove(object sender, MouseEventArgs e)
        {
            if (isPanning || draggingLine != null)
            {
                highZoomCache?.Dispose();
                highZoomCache = null;
            }

            // 1️ 패닝
            if (isPanning)
            {
                viewOffset.X += e.X - lastMousePt.X;
                viewOffset.Y += e.Y - lastMousePt.Y;

                lastMousePt = e.Location;

                pictureBoxMiniMap.Invalidate();
                pictureBoxImage.Invalidate();
                return;
            }

            // 2️ 기준선 점 드래그
            if (draggingLine != null)
            {
                PointF originalPt = ScreenToOriginal(e.Location);

                if (dragTarget == DragTarget.StartPoint)
                    draggingLine.StartPt = originalPt;
                else if (dragTarget == DragTarget.EndPoint)
                    draggingLine.EndPt = originalPt;

                CalculateCropBoxes(draggingLine);
                pictureBoxImage.Invalidate();
                return;
            }

            // 3️ 이미지 영역 밖이면 중단
            if (!IsInsideImageScreen(e.Location))
            {
                isBusy = false;
                return;
            }

            // 화면 좌표 저장 (Overlay용)
            mouseScreenPt = e.Location;

            // Screen → Original 직접 변환
            mouseOriginalPt = ScreenToOriginal(e.Location);

            UpdateHoverCropBox(mouseOriginalPt);

            pictureBoxImage.Invalidate();
        }

        private void PictureBoxImage_MouseUp(object sender, MouseEventArgs e)
        {

            isPanning = false;
            pictureBoxImage.Cursor = Cursors.Default;
            draggingLine = null;
            dragTarget = DragTarget.None;
            UpdateHighZoomCache();

        }

        private void PictureBoxImage_MouseWheel(object sender, MouseEventArgs e)
        {
            float oldScale = viewScale;

            if (e.Delta > 0)
                viewScale *= 1.1f;
            else
                viewScale /= 1.1f;

            viewScale = Math.Max(MinZoom, Math.Min(MaxZoom, viewScale));

            viewOffset.X = e.X - (e.X - viewOffset.X) * (viewScale / oldScale);
            viewOffset.Y = e.Y - (e.Y - viewOffset.Y) * (viewScale / oldScale);

            pictureBoxMiniMap.Invalidate();
            pictureBoxImage.Invalidate();
        }

        /* =========================================================
         *  Paint
         * ========================================================= */

        bool isBusy = false;
        private void PictureBoxImage_Paint(object sender, PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(Color.Black);

            DrawMemoryOverlay(g);

            if (isImageLoading)
            {
                DrawLoadingSpinner(g);
                return;
            }

            if (isBusy) return;
            isBusy = true;

            try
            {
                if (originalMat == null || pyramidLevels.Count == 0)
                {
                    isBusy = false;
                    return;
                }

                float levelScale;
                GetBestLevel(out levelScale);   // currentPyramidLevel 설정
                if (currentPyramidLevel >= pyramidBitmaps.Count || currentPyramidLevel >= pyramidHBitmaps.Count)
                    return;

                Bitmap cachedBmp = pyramidBitmaps[currentPyramidLevel];     // GDI+ fallback용
                IntPtr hBmp = pyramidHBitmaps[currentPyramidLevel];         // GDI StretchBlt용
                float drawWidth = cachedBmp.Width * levelScale;             // 실제 그릴 크기 계산 
                float drawHeight = cachedBmp.Height * levelScale;
                int srcW = cachedBmp.Width;
                int srcH = cachedBmp.Height;

                bool drawn = false;
                if (hBmp != IntPtr.Zero)
                {
                    try
                    {
                        IntPtr hdc = g.GetHdc();        // Graphics → Win32 HDC 변환
                        try
                        {
                            SetStretchBltMode(hdc, STRETCH_HALFTONE);    // StretchBlt의 보간 방식 설정
                            SetBrushOrgEx(hdc, 0, 0, IntPtr.Zero);

                            IntPtr memDC = CreateCompatibleDC(hdc);     // 메모리 DC 생성
                            IntPtr old = SelectObject(memDC, hBmp);     // HBITMAP을 DC에 연결
                            bool ok = StretchBlt(hdc,
                                (int)viewOffset.X, (int)viewOffset.Y, (int)drawWidth, (int)drawHeight,
                                memDC, 0, 0, srcW, srcH, SRCCOPY);
                            SelectObject(memDC, old);
                            DeleteDC(memDC);            // 리소스 정리
                            drawn = ok;
                        }
                        finally
                        {
                            g.ReleaseHdc(hdc);   // GDI+로 
                        }
                    }
                    catch { /* GDI 실패 시 폴백 */ }
                }

                if (!drawn)   // GDI 실패시 GDI+ 사용
                {
                    g.PixelOffsetMode = PixelOffsetMode.HighSpeed;
                    g.SmoothingMode = SmoothingMode.None;
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;

                    g.DrawImage(cachedBmp,
                        viewOffset.X, viewOffset.Y,
                        drawWidth, drawHeight);
                }

                DrawPointsAndLine(g);
                DrawGuideBoxes(g);
                DrawMousePositionOverlay(g);
                DrawImageTypeOverlay(g);
                DrawMemoryOverlay(g);
            }
            finally
            {
                isBusy = false;
            }
        }

        private void PictureBoxMiniMap_Paint(object sender, PaintEventArgs e)
        {
            if (miniMapBitmap == null || originalMat == null)
                return;

            Graphics g = e.Graphics;

            g.Clear(Color.Black);

            float scale = miniMapScale;

            int offsetX = (pictureBoxMiniMap.Width - miniMapBitmap.Width) / 2;     // 중앙정렬
            int offsetY = (pictureBoxMiniMap.Height - miniMapBitmap.Height) / 2;

            g.DrawImage(miniMapBitmap, offsetX, offsetY);
            DrawMiniMapViewport(g, scale, offsetX, offsetY);    // 현재 보고있는곳
            DrawMiniMapGuideBoxes(g, scale, offsetX, offsetY);
        }

        /* =========================================================
         *  Draw Helpers
         * ========================================================= */
        private void DrawMousePositionOverlay(Graphics g)   // 마우스 포지션 점좌표 그리기
        {
            string text = $"({(int)mouseOriginalPt.X}, {(int)mouseOriginalPt.Y})";

            using (Font font = new Font("맑은 고딕", 9, FontStyle.Bold))
            {
                SizeF size = g.MeasureString(text, font);
                float x = mouseScreenPt.X + 12;      // 마우스 커서랑 겹치지 않게
                float y = mouseScreenPt.Y + 12;

                RectangleF bg = new RectangleF(     // 배경 사각형 크기
                    x, y,
                    size.Width + 8,
                    size.Height + 8
                );
                // 반투명 배경
                //using (Brush b = new SolidBrush(Color.FromArgb(180, 0, 0, 0)))
                //    g.FillRectangle(b, bg);

                g.DrawString(text, font, Brushes.DeepSkyBlue, x + 4, y + 4);    // 텍스트
            }
        }

        private void DrawImageTypeOverlay(Graphics g)
        {
            if (string.IsNullOrEmpty(imageColorInfoText))
                return;

            using (Font font = new Font("맑은 고딕", 9, FontStyle.Bold))
            {
                SizeF size = g.MeasureString(imageColorInfoText, font);

                float x = 8;
                float y = 8;

                RectangleF bg = new RectangleF(
                    x,
                    y,
                    size.Width + 8,
                    size.Height + 8
                    );

                // 반투명 배경
                using (Brush bgBrush = new SolidBrush(Color.FromArgb(160, 0, 0, 0)))
                    g.FillRectangle(bgBrush, bg);

                g.DrawString(
                    imageColorInfoText,
                    font,
                    Brushes.Orange,
                    x + 4,
                    y + 3
                    );
            }
        }
        private void DrawPointsAndLine(Graphics g)
        {
            using (Pen pen = new Pen(Color.Red, 2))
            {
                foreach (var line in baseLines)
                {
                    PointF s = OriginalToScreen(line.StartPt);
                    PointF e = OriginalToScreen(line.EndPt);

                    g.DrawLine(pen, s, e);

                    DrawPoint(g, line.StartPt);
                    DrawPoint(g, line.EndPt);
                }

                if (currentLine != null)
                    DrawPoint(g, currentLine.StartPt);
            }
        }

        private void DrawPoint(Graphics g, PointF originalPt)
        {
            PointF screenPt = OriginalToScreen(originalPt);

            float r = 4f;

            g.FillEllipse(
                Brushes.Red,
                screenPt.X - r,
                screenPt.Y - r,
                r * 2,
                r * 2
            );
        }
        private void DrawGuideBoxes(Graphics g)
        {
            foreach (var line in baseLines)
            {
                foreach (var box in line.CropBoxes)
                {
                    Color color = box.IsHovered ? Color.Lime : Color.Yellow;

                    using (Pen pen = new Pen(color, 2))
                    {
                        Rectangle r = box.EffectiveRect;

                        PointF tl = OriginalToScreen(new PointF(r.Left, r.Top));
                        PointF br = OriginalToScreen(new PointF(r.Right, r.Bottom));

                        g.DrawRectangle(
                            pen,
                            tl.X,
                            tl.Y,
                            br.X - tl.X,
                            br.Y - tl.Y
                        );
                    }
                }
            }
        }

        private void DrawMiniMapGuideBoxes(Graphics g, float scale, int offsetX, int offsetY)
        {
            foreach (var line in baseLines)
            {
                foreach (var box in line.CropBoxes)
                {
                  

                    using (Pen pen = new Pen(Color.Yellow, 2))
                    {
                        Rectangle r = box.EffectiveRect;

                        PointF tl = OriginalToMiniMap(new PointF(r.Left, r.Top), scale, offsetX, offsetY);
                        PointF br = OriginalToMiniMap(new PointF(r.Right, r.Bottom), scale, offsetX, offsetY);

                        g.DrawRectangle(
                            pen,
                            tl.X,
                            tl.Y,
                            br.X - tl.X,
                            br.Y - tl.Y
                        );
                    }
                }
            }
        }
        private void DrawMemoryOverlay(Graphics g)
        {
            string text = GetMemoryInfo();

            using (Font font = new Font("맑은 고딕", 9, FontStyle.Bold))
            {
                SizeF size = g.MeasureString(text, font);

                float x = 10;
                float y = pictureBoxImage.Height - size.Height - 10;

                using (Brush bg = new SolidBrush(Color.FromArgb(160, 0, 0, 0)))
                    g.FillRectangle(bg, x - 5, y - 5, size.Width + 10, size.Height + 6);

                g.DrawString(text, font, Brushes.Lime, x, y);
            }
        }

        private void DrawMiniMapViewport(Graphics g, float scale, int offsetX, int offsetY)
        {
            PointF tl = ScreenToOriginal(new DPoint(0, 0));     // 좌상단 화면 좌표 -> 원본 이미지 좌표
            PointF br = ScreenToOriginal(
                new DPoint(pictureBoxImage.Width, pictureBoxImage.Height));  // 우하단

            float rx = tl.X * scale + offsetX;
            float ry = tl.Y * scale + offsetY;  // 사각형 시작 좌표

            float rw = (br.X - tl.X) * scale;
            float rh = (br.Y - tl.Y) * scale;

            using (Pen p = new Pen(Color.Red, 2))
                g.DrawRectangle(p, rx, ry, rw, rh);
        }



        /* =========================================================
         *  크롭박스 계산 / 기준점
         * ========================================================= */
        private void CalculateCropBoxes(BaseLineInfo line)
        {
            line.CropBoxes.Clear();   // 기존 박스 제거

            float dx = line.EndPt.X - line.StartPt.X;
            float dy = line.EndPt.Y - line.StartPt.Y;
            float length = (float)Math.Sqrt(dx * dx + dy * dy);
            if (length < 1f)
                return;

            float ux = dx / length;
            float uy = dy / length;

            int cropSize = line.CropSize;

            for (float dist = 0; dist <= length + cropSize / 2f; dist += cropSize)
            {
                PointF anchor = new PointF(       // 기준점 계산
                    line.StartPt.X + ux * dist,
                    line.StartPt.Y + uy * dist
                );

                PointF tl = AnchorToBox(anchor, cropSize);  // 기준점 토대로 좌상단 어딘지

                int x = (int)Math.Max(0, Math.Min(tl.X, originalMat.Width - cropSize));    // 이미지 영역 밖 방지
                int y = (int)Math.Max(0, Math.Min(tl.Y, originalMat.Height - cropSize));

                line.CropBoxes.Add(new CropBoxInfo
                {
                    EffectiveRect = new Rectangle(x, y, cropSize, cropSize),
                    OwnerLine = line        // Hover, preview, 라인삭제, 정보표기 용
                });

            }
        }

        private PointF AnchorToBox(PointF anchor, float size)  // 기준점 계산
        {
            switch (cropAnchor)
            {
                case CropAnchor.Center:
                    return new PointF(anchor.X - size / 2f, anchor.Y - size / 2f);
                case CropAnchor.TopLeft:
                    return anchor;
                case CropAnchor.TopRight:
                    return new PointF(anchor.X - size, anchor.Y);
                case CropAnchor.BottomLeft:
                    return new PointF(anchor.X, anchor.Y - size);
                case CropAnchor.BottomRight:
                    return new PointF(anchor.X - size, anchor.Y - size);
                default:
                    return anchor;
            }
        }

        private void UpdateHoverCropBox(PointF originalPt)
        {
            hoveredBox = null;   // 마우스는 계속 움직이니깐

            // 호버된 박스 찾기
            foreach (var line in baseLines)
            {
                foreach (var box in line.CropBoxes)
                {
                    if (box.EffectiveRect.Contains(
                        (int)originalPt.X,
                        (int)originalPt.Y))
                    {
                        hoveredBox = box;
                        break;
                    }
                }
            }
            // 호버 상태 업데이트(상자 하나만 true)
            foreach (var line in baseLines)
                foreach (var box in line.CropBoxes)
                    box.IsHovered = (box == hoveredBox);

            if (hoveredBox != null)
            {
                ShowCropPreview(hoveredBox);
                UpdateLineInfo(hoveredBox.OwnerLine);
            }
            else
            {
                ClearPreview();
                UpdateLineInfo(null);
            }
        }


        /* =========================================================
         *  Preview 미리보기
         * ========================================================= */
        private void ShowCropPreview(CropBoxInfo hoverdBox)   // 박스 미리보기
        {
            if (hoverdBox == null || originalMat == null)
                return;

            Rectangle r = hoverdBox.EffectiveRect;

            var roi = new OpenCvSharp.Rect(   // ROI 생성
                r.X, r.Y, r.Width, r.Height
            );

            using (Mat cropped = new Mat(originalMat, roi))   // ROI로 Mat 잘라내기
            {
                pictureBoxPreview.Image?.Dispose();
                pictureBoxPreview.Image = BitmapConverter.ToBitmap(cropped);
            }
        }

        private void ClearPreview()
        {
            pictureBoxPreview.Image?.Dispose();
            pictureBoxPreview.Image = null;
        }

        /* =========================================================
         *  크롭박스 저장
         * ========================================================= */
        private void BtnCropSave_Click(object sender, EventArgs e) => CropAndSaveAll();

        private void CropAndSaveAll()
        {
            if (baseLines.Count == 0)
                return;

            string folder = Path.Combine(
                Application.StartupPath,
                "Crops",
                DateTime.Now.ToString("yyyyMMdd_HHmmss")
            );
            Directory.CreateDirectory(folder);

            int lineIndex = 1;
            int cropIndex = 1;

            foreach (var line in baseLines)
            {
                foreach (var box in line.CropBoxes)
                {
                    Rectangle r = box.EffectiveRect;

                    var roi = new OpenCvSharp.Rect(
                        r.X, r.Y, r.Width, r.Height
                    );

                    string path = Path.Combine(
                        folder,
                        $"L{lineIndex}_C{cropIndex:D3}.png"
                    );

                    using (Mat cropped = new Mat(originalMat, roi))
                    {
                        Cv2.ImWrite(path, cropped);
                    }

                    cropIndex++;
                }
                lineIndex++;
            }

            MessageBox.Show("크롭 이미지 저장 완료");
        }


        /* =========================================================
         *  좌표 계산
         * ========================================================= */
        private PointF ScreenToOriginal(DPoint screenPt)
        {
            if (displayBitmap == null || displayBaseScale == 0)
                return PointF.Empty;

            float x = (screenPt.X - viewOffset.X) / viewScale;
            float y = (screenPt.Y - viewOffset.Y) / viewScale;

            return new PointF(
                x / displayBaseScale,
                y / displayBaseScale
            );
        }

        private PointF OriginalToScreen(PointF originalPt)
        {
            if (displayBitmap == null)
                return PointF.Empty;

            float x = originalPt.X * displayBaseScale * viewScale + viewOffset.X;
            float y = originalPt.Y * displayBaseScale * viewScale + viewOffset.Y;

            return new PointF(x, y);
        }

        private PointF OriginalToMiniMap(PointF original, float scale, int offsetX, int offsetY)
        {
            return new PointF(
                original.X * scale + offsetX,
                original.Y * scale + offsetY
                );
        }

        private bool IsHitOriginal(DPoint mouseScreenPt, PointF targetOriginalPt)
        {
            PointF targetScreen = OriginalToScreen(targetOriginalPt);

            return Math.Abs(mouseScreenPt.X - targetScreen.X) <= HitRadius &&
                   Math.Abs(mouseScreenPt.Y - targetScreen.Y) <= HitRadius;
        }

        private bool IsInsideImageScreen(DPoint screenPt)
        {
            if (displayBitmap == null)
                return false;

            RectangleF rect = new RectangleF(
                viewOffset.X,
                viewOffset.Y,
                displayBitmap.Width * viewScale,
                displayBitmap.Height * viewScale
            );

            return rect.Contains(screenPt);
        }


        /* =========================================================
         *  UI
         * ========================================================= */
        private void UpdateLineInfo(BaseLineInfo line)
        {
            if (line == null)
            {
                lblLineIndex.Text = "Line Index: -";
                lblLineLength.Text = "Line Length: -";
                lblCropCount.Text = "Crop Count: -";
                lblCropSize.Text = "Crop Size: -";
                return;
            }

            // Line Index
            int lineIndex = baseLines.IndexOf(line) + 1;

            // Line Length
            float dx = line.EndPt.X - line.StartPt.X;
            float dy = line.EndPt.Y - line.StartPt.Y;
            float length = (float)Math.Sqrt(dx * dx + dy * dy);

            lblLineIndex.Text = $"Line Index: {lineIndex}";
            lblLineLength.Text = $"Line Length: {length:F1}px";
            lblCropCount.Text = $"Crop Count: {line.CropBoxes.Count}";
            lblCropSize.Text = $"Crop Size: {line.CropSize}";
        }

        private void DrawLoadingSpinner(Graphics g)
        {
            int size = 50;
            int x = (pictureBoxImage.Width - size) / 2;
            int y = (pictureBoxImage.Height - size) / 2;

            using (Pen bg = new Pen(Color.FromArgb(50, Color.Gray), 6))
            using (Pen fg = new Pen(Color.DeepSkyBlue, 6))
            {
                g.DrawEllipse(bg, x, y, size, size);
                g.DrawArc(fg, x, y, size, size, spinnerAngle, 100);
            }

            using (Font font = new Font("맑은 고딕", 10, FontStyle.Bold))
            {
                string msg = "Loading...";
                SizeF ts = g.MeasureString(msg, font);
                g.DrawString(
                    msg,
                    font,
                    Brushes.DimGray,
                    (pictureBoxImage.Width - ts.Width) / 2,
                    y + size + 10
                );
            }
        }
    }
}
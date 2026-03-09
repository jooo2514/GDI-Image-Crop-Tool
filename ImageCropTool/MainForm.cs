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
        private ImageTileRenderer renderer;

        private List<Mat> pyramidLevels = new List<Mat>();
        //private List<Bitmap> pyramidBitmaps = new List<Bitmap>();   // 레벨별 Bitmap 캐시 (Format32bppPArgb)
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
        private Mat originalMat;
        private string imageColorInfoText = string.Empty;

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
        private DPoint lastMousePt;

        /* ========= Crop Anchor ========== */
        private CropAnchor cropAnchor = CropAnchor.Center;

        /* ========== View Transform (Zoom & Pan) ============ */
        private float viewScale = 1.0f;                 // 줌 배율
        private float displayBaseScale = 1.0f;          // 화면에 맞추기 위한 기본 축소 비율

        private PointF viewOffset = new PointF(0, 0);   // 이미지 시작 위치

        private const float ZoomStep = 1.1f;            // 휠 한칸에 10%씩 변화
        private const float MinZoom = 0.2f;
        private const float MaxZoom = 100.0f;

        private bool isPanning = false;


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

            renderer?.Dispose();
            renderer = null;

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

            pictureBoxMiniMap.Invalidate();
            pictureBoxImage.Invalidate();
        }

        private void ResetView()
        {
            if (originalMat == null)
                return;

            float scaleX = (float)pictureBoxImage.Width / originalMat.Width;
            float scaleY = (float)pictureBoxImage.Height / originalMat.Height;

            viewScale = Math.Min(scaleX, scaleY);

            viewOffset = new PointF(
                (pictureBoxImage.Width - originalMat.Width * viewScale) / 2f,
                (pictureBoxImage.Height - originalMat.Height * viewScale) / 2f
            );

            pictureBoxMiniMap.Invalidate();
            pictureBoxImage.Invalidate();
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

            pictureBoxImage.Image = null;

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

                LineReset();
                BuildPyramid();
                CreateMiniMap();
                ResetView();

                renderer = new ImageTileRenderer(pyramidLevels);
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

                // miniMapBitmap 자원을 PictureBox와 동기화
                pictureBoxMiniMap.Image?.Dispose();
                miniMapBitmap = BitmapConverter.ToBitmap(resized);
                pictureBoxMiniMap.Image = miniMapBitmap;
            }
        }

        /* =========================================================
         *  pyramid
         * ========================================================= */
        private void DisposePyramidCaches()
        {
            if (pyramidLevels == null) return;

            // 0번은 originalMat이므로 1번부터 Dispose
            for (int i = 1; i < pyramidLevels.Count; i++)
            {
                pyramidLevels[i]?.Dispose();
            }
            pyramidLevels.Clear();
        }

        private void BuildPyramid()    // 원본 이미지를 여러 해상도로 미리 만들기
        {
            DisposePyramidCaches();
   
            pyramidLevels.Add(originalMat);          // 피라미드 0단계 : 원본
            Mat current = originalMat;

            // 축소
            while (current.Width > 512 && current.Height > 512)
            {
                Mat down = new Mat();
                Cv2.PyrDown(current, down);  // 가우시안 필터 + 1/2 다운샘플링
                pyramidLevels.Add(down);
                current = down;             // 다음 층을 위해 현재 이미지 갱신
            }

        }

        private Mat GetBestLevel(out float levelScale)
        {
            levelScale = viewScale * displayBaseScale;

            if (pyramidLevels == null || pyramidLevels.Count == 0)
                return null;

            int level = 0;

            float scale = levelScale;

            while (scale < 0.5f && level < pyramidLevels.Count - 1)
            {
                scale *= 2f;
                level++;
            }

            currentPyramidLevel = level;
            levelScale = scale;

            return pyramidLevels[level];
        }

        /* =========================================================
         *  Mouse Down
         * ========================================================= */
        private void PictureBoxImage_MouseDown(object sender, MouseEventArgs e)
        {
            if (originalMat == null)
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
                pictureBoxMiniMap.Invalidate();
                pictureBoxImage.Invalidate();
                return;
            }

            // 3️ 이미지 영역 밖이면 중단
            if (!IsInsideImageScreen(e.Location))
            {
                
                return;
            }

            // 화면 좌표 저장 (Overlay용)
            mouseScreenPt = e.Location;

            // Screen → Original 직접 변환
            mouseOriginalPt = ScreenToOriginal(e.Location);

            UpdateHoverCropBox(mouseOriginalPt);

            pictureBoxMiniMap.Invalidate();
            pictureBoxImage.Invalidate();
        }

        private void PictureBoxImage_MouseUp(object sender, MouseEventArgs e)
        {

            isPanning = false;
            pictureBoxImage.Cursor = Cursors.Default;
            draggingLine = null;
            dragTarget = DragTarget.None;
        }

        private void PictureBoxImage_MouseWheel(object sender, MouseEventArgs e)
        {
            float oldScale = viewScale;
            float ratio = 1.1f;

            if (e.Delta > 0) viewScale *= ratio;       // 줌인
            else viewScale /= ratio;                  // 줌아웃 (0.909배)

            // 하한선 설정 (이미지가 점이 되어 사라지는 것 방지)
            if (viewScale < 0.001f) viewScale = 0.001f;

            // 마우스 커서 지점을 고정하고 확대/축소 (중요!)
            // 이 계산이 없으면 줌아웃 시 이미지가 엉뚱한 방향으로 날아갑니다.
            PointF mousePos = e.Location;
            viewOffset.X = mousePos.X - (mousePos.X - viewOffset.X) * (viewScale / oldScale);
            viewOffset.Y = mousePos.Y - (mousePos.Y - viewOffset.Y) * (viewScale / oldScale);

            pictureBoxMiniMap.Invalidate();
            pictureBoxImage.Invalidate();
        }

        /* =========================================================
         *  Paint
         * ========================================================= */
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
            
            if (renderer == null || pyramidLevels.Count == 0)
                return;

            g.InterpolationMode = InterpolationMode.NearestNeighbor;
            g.PixelOffsetMode = PixelOffsetMode.Half;
            g.SmoothingMode = SmoothingMode.None;

            float levelScale;
            GetBestLevel(out levelScale);

            renderer.ViewScale = viewScale * levelScale;
            renderer.BaseScale = 1.0f;
            renderer.ViewOffset = viewOffset;

            renderer.Draw(
                g,
                pictureBoxImage.Size,
                currentPyramidLevel,
                OriginalToScreen,
                ScreenToOriginal
            );
            DrawImageTypeOverlay(g);
            DrawMousePositionOverlay(g);
            DrawPointsAndLine(g);
            DrawGuideBoxes(g);
            DrawMemoryOverlay(g);

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
            float x = (screenPt.X - viewOffset.X) / viewScale;
            float y = (screenPt.Y - viewOffset.Y) / viewScale;

            return new PointF(x, y);
        }

        private PointF OriginalToScreen(PointF originalPt)
        {
            float x = originalPt.X * viewScale + viewOffset.X;
            float y = originalPt.Y * viewScale + viewOffset.Y;

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
            if (originalMat == null)
                return false;

            RectangleF rect = new RectangleF(
                viewOffset.X,
                viewOffset.Y,
                originalMat.Width * viewScale,
                originalMat.Height * viewScale
            ); return rect.Contains(screenPt);

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
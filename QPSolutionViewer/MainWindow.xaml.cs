using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using SWShapes = System.Windows.Shapes;

namespace QPSolutionViewer
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private double Delta;
        private double preWidth;
        private double preHeight;

        bool boardWidthChanged = false;
        bool boardHeightChanged = false;

        private IList<int[]> PositionsList;

        private double Gauge = 3; // defines Queens rectangles sizes
        private double boardWidth;

        private bool IsBuiltPathGeo;
        private bool IsBuiltStreamGeo;

        // --- individual path for each item ---
        SWShapes.Path AllQueens;
        SWShapes.Path Rows;
        SWShapes.Path Columns;
        SWShapes.Path[] DiagsPositive;
        SWShapes.Path[] DiagsNegative;

        // --- combined items ---
        SWShapes.Path[] Queens;
        SWShapes.Path[] Diags;
        SWShapes.Path SquareNet;

        // --- color group ---
        SolidColorBrush OrtsBrush = new SolidColorBrush(Color.FromArgb(15, 255, 255, 255));
        SolidColorBrush QueenBrush = new SolidColorBrush(Color.FromRgb(33, 185, 255));
        Color SelectedColor = Color.FromRgb(200, 230, 255);
        Color tmpColor;
        int SelectedIndex = -1;
        private double hue, sat, hueStep, satStep;

        public MainWindow()
        {
            InitializeComponent();
            int renderingTier = (RenderCapability.Tier >> 16);
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            preWidth = ActualWidth;
            preHeight = ActualHeight;
            Delta = preWidth - preHeight;

            SizeChanged += Window_SizeChanged;
            BoardCanvas.SizeChanged += BoardCanvas_SizeChanged;
        }

        private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            double W = e.NewSize.Width;
            double H = e.NewSize.Height;

            if (W-H != Delta)
            {
                double dw = W - preWidth;
                double dh = H - preHeight;

                if (dw != dh)
                {
                    double absChange = Math.Abs(dw) + Math.Abs(dh);
                    double pw = dw / absChange;
                    double ph = dh / absChange;
                    double d = (Delta - W + H) / (ph - pw);

                    Width = W + ph * d;
                    Height = H + pw * d;
                }
            }

            preWidth = Width;
            preHeight = Height;
        }

        private void bttOpen_Click(object sender, RoutedEventArgs e)
        {
            Microsoft.Win32.OpenFileDialog dlgOpenFile = new Microsoft.Win32.OpenFileDialog();
            dlgOpenFile.Multiselect = false;
            dlgOpenFile.DefaultExt = ".qps";
            dlgOpenFile.Filter = "Application extension (.qps)|*.qps";

            FileStream fiStream = null;
            string file = "";
            bool binOk = true;

            try
            {
                if (dlgOpenFile.ShowDialog() == true)
                {
                    // --- file Loading ---
                    file = dlgOpenFile.FileName;

                    fiStream = new FileStream(file, FileMode.Open);

                    BinaryFormatter binForm = new BinaryFormatter();

                    PositionsList = (IList<int[]>)binForm.Deserialize(fiStream);
                }
                else binOk = false; // nothing was selected

            }
            catch (Exception exc)
            {
                binOk = false;
                MessageBox.Show(exc.ToString(), "Issues during loading", MessageBoxButton.OK, MessageBoxImage.Exclamation);
            }
            finally
            {
                if (fiStream != null) fiStream.Close();
            }

            if (binOk) // then Positions Rendering
            {
                file = System.IO.Path.GetFileNameWithoutExtension(file);

                int xMin = PositionsList.Min(p => p[0]);
                int yMin = PositionsList.Min(p => p[1]);

                int posWidth = Math.Max(PositionsList.Max(p => p[0]) - xMin,
                                        PositionsList.Max(p => p[1]) - yMin);

                txtInfo.Text = string.Format("{0} | count: {1} | width: {2} | shift: {3},{4}", file, PositionsList.Count, ++posWidth, xMin, yMin);

                //BuildIndividualPathGeos(); // It does not implement all facilities (WIP)
                BuildComboStreamGeos();

                PosListBox.ItemsSource = PositionsList;

                tggBttRows.IsChecked = false;
                tggBttClms.IsChecked = false;
                tggBttBPos.IsChecked = false;
                tggBttBNeg.IsChecked = false;
            }
        }

        private void BoardCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (e.WidthChanged) boardWidthChanged = true;
            if (e.HeightChanged) boardHeightChanged = true;

            if ((IsBuiltPathGeo || IsBuiltStreamGeo) && boardWidthChanged && boardHeightChanged)
            {
                boardWidthChanged = false;
                boardHeightChanged = false;

                Fitin(Math.Min(BoardCanvas.ActualHeight, BoardCanvas.ActualWidth), IsBuiltStreamGeo);
            }
        }

        private void BuildIndividualPathGeos()
        {
            IsBuiltStreamGeo = false;
            IsBuiltPathGeo = false;

            int xMax = PositionsList.Max(p => p[0]);
            int yMax = PositionsList.Max(p => p[1]);

            boardWidth = Gauge * Math.Max(xMax, yMax);
            double Width = boardWidth;

            int Length = PositionsList.Count;

            double StrokeThickness = Gauge / 3;
            double offset = 0.5 * Gauge;
            double ending = Width - offset;

            OrtsBrush.Freeze();
            QueenBrush.Freeze();

            Rows = new SWShapes.Path();
            Rows.Stroke = OrtsBrush;
            Rows.StrokeThickness = StrokeThickness;
            Panel.SetZIndex(Rows, -10);

            Columns = new SWShapes.Path();
            Columns.Stroke = OrtsBrush;
            Columns.StrokeThickness = StrokeThickness;
            Panel.SetZIndex(Columns, -10);

            AllQueens = new SWShapes.Path();
            AllQueens.Fill = QueenBrush;
            Panel.SetZIndex(AllQueens, 10);

            LineSegment[] uLines = new LineSegment[2 * Length];
            LineSegment[] vLines = new LineSegment[2 * Length];

            GeometryGroup QGroup = new GeometryGroup();

            double u, v;
            int index = 0;

            foreach (int[] Pos in PositionsList)
            {
                u = Pos[0] * Gauge;
                v = Pos[1] * Gauge;

                QGroup.Children.Add(new RectangleGeometry(new Rect(u, v, Gauge, Gauge)));

                u += offset;
                v += offset;

                uLines[index] = new LineSegment(new Point(offset, v), false);
                vLines[index] = new LineSegment(new Point(u, offset), false);

                index++;

                uLines[index] = new LineSegment(new Point(ending, v), true);
                vLines[index] = new LineSegment(new Point(u, ending), true);

                index++;
            }

            Point StartPoint = new Point(offset, offset);

            AllQueens.Data = QGroup;
            Rows.Data = new PathGeometry(new PathFigure[1] { new PathFigure(StartPoint, uLines, false) });
            Columns.Data = new PathGeometry(new PathFigure[1] { new PathFigure(StartPoint, vLines, false) });

            AllQueens.Data.Freeze();
            Rows.Data.Freeze();
            Columns.Data.Freeze();

            BoardCanvas.Children.Clear();
            BoardCanvas.Children.Add(AllQueens);
            BoardCanvas.Children.Add(Rows);
            BoardCanvas.Children.Add(Columns);

            IsBuiltPathGeo = true;

            Fitin(Math.Min(BoardCanvas.ActualHeight, BoardCanvas.ActualWidth), false);
        }
        
        private void BuildComboStreamGeos()
        {
            IsBuiltPathGeo = false;
            IsBuiltStreamGeo = false;

            BoardCanvas.Children.Clear();
            
            int xMax = PositionsList.Max(p => p[0]);
            int yMax = PositionsList.Max(p => p[1]);

            int xMin = PositionsList.Min(p => p[0]);
            int yMin = PositionsList.Min(p => p[1]);

            int bPosShift = -yMin + xMin;
            int bNegShift = -yMin - xMin;

            double Width = Gauge * (Math.Max(xMax - xMin, yMax - yMin) + 1);
            boardWidth = Width;

            int Length = PositionsList.Count;

            double StrokeThickness = Gauge / 3;
            double offset = 0.5 * Gauge;
            double ending = Width - offset;

            OrtsBrush.Freeze();

            // --- ini color group ---
            hue = 0;
            sat = 0;
            hueStep = 360 * (Math.Floor(Length / 6.0) + 1) / Length;
            satStep = (Math.Floor(Length / 2.0) + 1) / Length;

            // --- ini paths ---
            Queens = new SWShapes.Path[Length];
            Diags = new SWShapes.Path[Length];

            SquareNet = new SWShapes.Path();
            SquareNet.Stroke = OrtsBrush;
            SquareNet.StrokeThickness = StrokeThickness;
            Panel.SetZIndex(SquareNet, -10);

            StrokeThickness /= 1.5;

            StreamGeometry tmpQueenGeo, tmpDiagGeo;
            StreamGeometry netGeo = new StreamGeometry();

            StreamGeometryContext ctx_tmpQueen = null, ctx_tmpDiag = null, ctx_netGeo = null;

            SWShapes.Path tmpPath;
            Color queenColor;

            double u0, v0, u, v;
            int index = 0, tmpB;

            try
            {
                ctx_netGeo = netGeo.Open();
                ctx_netGeo.BeginFigure(new Point(0, 0), false, false);

                foreach (int[] Pos in PositionsList)
                {
                    u0 = Gauge * (Pos[0] - xMin);
                    v0 = Gauge * (Pos[1] - yMin);
                    u = u0 + Gauge;
                    v = v0 + Gauge;

                    queenColor = GetColor();

                    // --- drawing Queen rectangle ---------------------------------------
                    tmpQueenGeo = new StreamGeometry();
                    ctx_tmpQueen = tmpQueenGeo.Open();

                    ctx_tmpQueen.BeginFigure(new Point(u0, v0), true, true);
                    ctx_tmpQueen.LineTo(new Point(u, v0), true, false);
                    ctx_tmpQueen.LineTo(new Point(u, v), true, false);
                    ctx_tmpQueen.LineTo(new Point(u0, v), true, false);

                    ((IDisposable)ctx_tmpQueen).Dispose();
                    tmpQueenGeo.Freeze();

                    tmpPath = new SWShapes.Path();
                    tmpPath.Data = tmpQueenGeo;
                    tmpPath.Fill = new SolidColorBrush(queenColor);
                    Panel.SetZIndex(tmpPath, 10);

                    BoardCanvas.Children.Add(tmpPath);
                    Queens[index] = tmpPath;

                    // --- drawing net lines ---------------------------------------
                    u0 += offset;
                    v0 += offset;

                    ctx_netGeo.LineTo(new Point(offset, v0), false, false); // u-start
                    ctx_netGeo.LineTo(new Point(ending, v0), true, false); // u-end
                    ctx_netGeo.LineTo(new Point(u0, offset), false, false); // v-start
                    ctx_netGeo.LineTo(new Point(u0, ending), true, false); // v-end

                    // --- drawing Diags ---------------------------------------
                    tmpDiagGeo = new StreamGeometry();
                    ctx_tmpDiag = tmpDiagGeo.Open();

                    // positive diagonal
                    tmpB = Pos[2] + bPosShift;

                    if (tmpB >= 0)
                    {
                        u0 = 0;
                        v0 = Gauge * tmpB;

                        u = Width - v0;
                        v = Width;
                    }
                    else // tmpB < 0
                    {
                        u0 = -Gauge * tmpB;
                        v0 = 0;

                        u = Width;
                        v = Width - u0;
                    }

                    ctx_tmpDiag.BeginFigure(new Point(u0, v0), false, false);
                    ctx_tmpDiag.LineTo(new Point(u, v), true, false);

                    // negative diagonal
                    tmpB = Pos[3] + bNegShift;

                    if (tmpB >= Length)
                    {
                        tmpB++;

                        u0 = Width;
                        v0 = tmpB * Gauge - Width;

                        u = v0;
                        v = Width;
                    }
                    else // tmpb < Length
                    {
                        tmpB++;

                        u0 = tmpB * Gauge;
                        v0 = 0;

                        u = 0;
                        v = u0;
                    }

                    ctx_tmpDiag.LineTo(new Point(u0, v0), false, false);
                    ctx_tmpDiag.LineTo(new Point(u, v), true, false);

                    ((IDisposable)ctx_tmpDiag).Dispose();
                    tmpDiagGeo.Freeze();

                    queenColor.A = 100;

                    tmpPath = new SWShapes.Path();
                    tmpPath.Data = tmpDiagGeo;
                    tmpPath.Stroke = new SolidColorBrush(queenColor);
                    tmpPath.StrokeThickness = StrokeThickness;

                    BoardCanvas.Children.Add(tmpPath);
                    Diags[index] = tmpPath;
                    
                    index++;
                }
            }
            finally
            {
                if (ctx_tmpQueen != null) ((IDisposable)ctx_tmpQueen).Dispose();
                if (ctx_tmpDiag != null) ((IDisposable)ctx_tmpDiag).Dispose();
                if (ctx_netGeo != null) ((IDisposable)ctx_netGeo).Dispose();
            }

            netGeo.Freeze();
            SquareNet.Data = netGeo;

            BoardCanvas.Children.Add(SquareNet);

            IsBuiltStreamGeo = true;

            Fitin(Math.Min(BoardCanvas.ActualHeight, BoardCanvas.ActualWidth), true);
        }
        
        private void Fitin(double length, bool fitStreamGeo)
        {
            double factor = length / boardWidth;

            if (fitStreamGeo)
            {
                for (int i = 0; i < Queens.Length; i++)
                {
                    Queens[i].RenderTransform = new ScaleTransform(factor, factor);
                    Diags[i].RenderTransform = new ScaleTransform(factor, factor);
                }
                
                SquareNet.RenderTransform = new ScaleTransform(factor, factor);
            }
            else
            {
                AllQueens.RenderTransform = new ScaleTransform(factor, factor);
                Rows.RenderTransform = new ScaleTransform(factor, factor);
                Columns.RenderTransform = new ScaleTransform(factor, factor);
            }
        }

        private Color GetColor()
        {
            Color theColor = ConvertHsvToRgb(hue, 0.75 * sat + 0.25, 1);

            if (220 < hue && hue < 260)
            {
                theColor.R = 127;
                theColor.G = 127;
            }

            hue += hueStep;
            hue = hue % 360;

            sat += satStep;
            sat = sat % 1;

            return theColor;
        }

        // Algorithm taken from Wikipedia https://en.wikipedia.org/wiki/HSL_and_HSV
        private static Color ConvertHsvToRgb(double hue, double saturation, double value)
        {
            double chroma = value * saturation;

            if (hue == 360) hue = 0;

            double hueTag = hue / 60;
            double x = chroma * (1 - Math.Abs(hueTag % 2 - 1));
            double m = value - chroma;

            double R, G, B;

            switch ((int)hueTag)
            {
                case 0:
                    R = chroma; G = x; B = 0;
                    break;
                case 1:
                    R = x; G = chroma; B = 0;
                    break;
                case 2:
                    R = 0; G = chroma; B = x;
                    break;
                case 3:
                    R = 0; G = x; B = chroma;
                    break;
                case 4:
                    R = x; G = 0; B = chroma;
                    break;
                default:
                    R = chroma; G = 0; B = x;
                    break;
            }

            R += m; G += m; B += m;
            R *= 255; G *= 255; B *= 255;

            return Color.FromRgb((byte)R, (byte)G, (byte)B);
        }
        
        private void SortOverlaps(int index)
        {
            IEnumerable<IGrouping<int, int[]>> iGrp = PositionsList.GroupBy(p => p[index]);

            iGrp = iGrp.OrderByDescending(g => g.Count());

            List<int[]> gList = new List<int[]>(PositionsList.Count);

            foreach (IGrouping<int, int[]> g in iGrp) gList.AddRange(g);

            PosListBox.ItemsSource = gList;
        }

        private void tggBttRows_Click(object sender, RoutedEventArgs e)
        {
            tggBttClms.IsChecked = false;
            tggBttBPos.IsChecked = false;
            tggBttBNeg.IsChecked = false;

            if (PositionsList == null) return;

            if (tggBttRows.IsChecked == true) SortOverlaps(0);
            else PosListBox.ItemsSource = PositionsList.OrderBy(p => p[0]);
        }

        private void tggBttClms_Click(object sender, RoutedEventArgs e)
        {
            tggBttRows.IsChecked = false;
            tggBttBPos.IsChecked = false;
            tggBttBNeg.IsChecked = false;

            if (PositionsList == null) return;

            if (tggBttClms.IsChecked == true) SortOverlaps(1);
            else PosListBox.ItemsSource = PositionsList.OrderBy(p => p[1]);
        }

        private void tggBttBPos_Click(object sender, RoutedEventArgs e)
        {
            tggBttRows.IsChecked = false;
            tggBttClms.IsChecked = false;
            tggBttBNeg.IsChecked = false;

            if (PositionsList == null) return;

            if (tggBttBPos.IsChecked == true) SortOverlaps(2);
            else PosListBox.ItemsSource = PositionsList.OrderBy(p => p[2]);
        }

        private void tggBttBNeg_Click(object sender, RoutedEventArgs e)
        {
            tggBttRows.IsChecked = false;
            tggBttBPos.IsChecked = false;
            tggBttBPos.IsChecked = false;

            if (PositionsList == null) return;

            if (tggBttBNeg.IsChecked == true) SortOverlaps(3);
            else PosListBox.ItemsSource = PositionsList.OrderBy(p => p[3]);
        }

        private void PosListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            int[] Pos = (int[])(PosListBox.SelectedItem);

            int index = PositionsList.IndexOf(Pos);

            if (index < 0) return;

            if (IsBuiltStreamGeo)
            {
                tmpColor = (Diags[index].Stroke as SolidColorBrush).Color;

                (Diags[index].Stroke as SolidColorBrush).Color = SelectedColor;
                (Queens[index].Fill as SolidColorBrush).Color = SelectedColor;
            }

            SelectedIndex = index;
        }

        private void PosListBox_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            if (SelectedIndex < 0) return;

            if (IsBuiltStreamGeo)
            {
                (Diags[SelectedIndex].Stroke as SolidColorBrush).Color = tmpColor;

                tmpColor.A = 255;
                (Queens[SelectedIndex].Fill as SolidColorBrush).Color = tmpColor;
            }

            SelectedIndex = -1;
        }
    }
}

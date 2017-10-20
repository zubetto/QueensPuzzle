using System;
using System.Collections.Generic;
using DRW = System.Drawing;
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
using System.Windows.Shapes;
using System.Threading;

namespace QPSRangeViewer
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private List<int[]> Solutions; // Rows by columns
        private List<int> SolutionsSpreading = new List<int>();
        private List<double> DensitySpreading = new List<double>();
        private int Length;
        private double NumTotal;

        private DomainComparer DoComparer = new DomainComparer();

        private DRW.Bitmap bmSolutionsSpreading;
        private BitmapImage biSolutionsSpreading;

        private void bttOpen_Click(object sender, RoutedEventArgs e)
        {
            Microsoft.Win32.OpenFileDialog dlgOpenFile = new Microsoft.Win32.OpenFileDialog();
            dlgOpenFile.Multiselect = false;
            dlgOpenFile.DefaultExt = ".lqps";
            dlgOpenFile.Filter = "Application extension (.lqps)|*.lqps";

            FileStream fiStream = null;
            BinaryReader BR = null;
            List<int[]> Sloaded = null;
            string file = "";
            bool binOk = true;

            try
            {
                if (dlgOpenFile.ShowDialog() == true)
                {
                    // --- file Loading ---
                    file = dlgOpenFile.FileName;

                    fiStream = new FileStream(file, FileMode.Open);

                    BR = new BinaryReader(fiStream);
                    
                    int SolNum = BR.ReadInt32();
                    int length = BR.ReadInt32();

                    Sloaded = new List<int[]>(SolNum);
                    int[] Solution;

                    for (int i = 0; i < SolNum; i++)
                    {
                        Solution = new int[length];

                        for (int j = 0; j < length; j++)
                        {
                            Solution[j] = BR.ReadInt32();
                        }

                        Sloaded.Add(Solution);
                    }
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
                if (BR != null) BR.Close();
                if (fiStream != null) fiStream.Close();
            }

            if (binOk)
            {
                file = System.IO.Path.GetFileNameWithoutExtension(file);

                Solutions = Sloaded;

                Length = Solutions[0].Length;
                NumTotal = Math.Pow(Length, Length);
                
                txtInfo.Text = string.Format("{0}", file);

                DrawRangeSpreading();
            }
        }
        
        private void DrawRangeSpreading()
        {
            int vpWidth = (int)rectViewport.ActualWidth;
            int vpHeight = (int)rectViewport.ActualHeight;

            if (NumTotal > vpWidth) // --- Join solutions into domains ------------------
            {
                int rankNum = Length;
                double domLength = 1;
                int domNum = Length;
                int domWidth = 1;
                int Elw = 1; // is defined from vpWidth = Length^Elw
                int rLimit = Length;

                IEnumerable<IGrouping<IEnumerable<int>, int[]>> iGrp = null;

                if (Length > vpWidth) // domain consist of the Length-1 ranks plus fraction of the Length-th rank
                {
                    // define fraction of the Length-th rank
                }
                else // domain consist of integer number of ranks less than Length
                {
                    //Elw = (int)Math.Floor(Math.Log(vpWidth) / Math.Log(Length)); // Elw >= 1 should be true here
                    Elw = 3; // ### TODO: separate option ###
                    rankNum = Length - Elw;

                    //domLength = Math.Pow(Length, rankNum);
                    domLength = Factorial(Length) / Factorial(Elw); // ### TODO: separate option ###
                    domNum = (int)(Math.Pow(Length, Elw)); // domNum <= vpWidth should be true here

                    domWidth = vpWidth / domNum;
                    
                    iGrp = Solutions.GroupBy(S => S.Take(Elw), DoComparer).OrderBy(G => G.Key, DoComparer);
                }

                SolutionsSpreading.Clear();
                SolutionsSpreading.Capacity = domNum;

                DensitySpreading.Clear();
                DensitySpreading.Capacity = domNum;

                IEnumerator<IGrouping<IEnumerable<int>, int[]>> ieGrp = iGrp.GetEnumerator();
                ieGrp.MoveNext();

                IEqualityComparer<IEnumerable<int>> iEqual = DoComparer;

                int[] domIterator = new int[Elw];
                int rank;
                int count;
                bool run = true;

                while (run)
                {
                    if (IsOverlapsFree(domIterator)) // ### TODO: create separate option for this ###
                    {
                        if (iEqual.Equals(ieGrp.Current.Key, domIterator))
                        {
                            count = ieGrp.Current.Count();
                            ieGrp.MoveNext();
                        }
                        else count = 0;

                        SolutionsSpreading.Add(count);
                        DensitySpreading.Add(count / domLength);
                    }
                    else if (iEqual.Equals(ieGrp.Current.Key, domIterator)) ieGrp.MoveNext();

                    // Iterating through domains
                    rank = Elw - 1;
                    
                    while (rank >= 0)
                    {
                        if (++domIterator[rank] == Length)
                        {
                            if (rank == 0) run = false;

                            domIterator[rank] = 0;
                            rank--;
                        }
                        else break;
                    }
                }

                bmSolutionsSpreading = new DRW.Bitmap(vpWidth, vpHeight);
            }
            else // --- Draw solutions as separate lines -------------------------------
            {

            }
        }

        private bool IsOverlapsFree(int[] combi)
        {
            if (combi.Distinct().Count() == combi.Length &&
                combi.Select((x, i) => i - x).Distinct().Count() == combi.Length &&
                combi.Select((x, i) => i + x).Distinct().Count() == combi.Length)
            {
                return true;
            }
            else return false;
        }

        private class DomainComparer : IComparer<int[]>, IComparer<IEnumerable<int>>, IEqualityComparer<int[]>, IEqualityComparer<IEnumerable<int>>
        {
            int IComparer<int[]>.Compare(int[] Da, int[] Db)
            {
                if (Da.Length < Db.Length) return 10;
                else if (Da.Length > Db.Length) return -10;
                else
                {
                    for (int i = 0; i < Da.Length; i++)
                    {
                        if (Da[i] < Db[i]) return -1;
                        else if (Da[i] > Db[i]) return 1;
                    }

                    return 0;
                }
            }

            int IComparer<IEnumerable<int>>.Compare(IEnumerable<int> Da, IEnumerable<int> Db)
            {
                bool go = true;
                bool goDa, goDb;

                IEnumerator<int> iDa = Da.GetEnumerator();
                IEnumerator<int> iDb = Db.GetEnumerator();
                
                while (go)
                {
                    goDa = iDa.MoveNext();
                    goDb = iDb.MoveNext();

                    if (!goDa && goDb) return 10;
                    else if (goDa && !goDb) return -10;
                    else if (goDa && goDb)
                    {
                        if (iDa.Current > iDb.Current) return 1;
                        else if (iDa.Current < iDb.Current) return -1;
                    }
                    else return 0;
                }

                return 0;
            }

            bool IEqualityComparer<int[]>.Equals(int[] Da, int[] Db)
            {
                if (Da.Length != Db.Length) return false;
                else
                {
                    for (int i = 0; i < Da.Length; i++)
                    {
                        if (Da[i] != Db[i]) return false;
                    }

                    return true;
                }
            }

            bool IEqualityComparer<IEnumerable<int>>.Equals(IEnumerable<int> Da, IEnumerable<int> Db)
            {
                bool go = true;
                bool goDa, goDb;

                IEnumerator<int> iDa = Da.GetEnumerator();
                IEnumerator<int> iDb = Db.GetEnumerator();

                while (go)
                {
                    goDa = iDa.MoveNext();
                    goDb = iDb.MoveNext();

                    if (goDa ^ goDb) return false;
                    else if (goDa && goDb)
                    {
                        if (iDa.Current != iDb.Current) return false;
                    }
                    else return true;
                }

                return true;
            }

            int IEqualityComparer<IEnumerable<int>>.GetHashCode(IEnumerable<int> D)
            {
                return D.Sum();
            }

            int IEqualityComparer<int[]>.GetHashCode(int[] D)
            {
                return D.Sum();
            }
        }

        private void DrawDomain(int xo, int width, DRW.Color color)
        {

        }

        private DRW.Color NumberToColor(int number)
        {
            DRW.Color color = DRW.Color.FromArgb(255, 255, 255, 255);

            return color;
        }

        // Algorithm taken from Wikipedia https://en.wikipedia.org/wiki/HSL_and_HSV
        private DRW.Color ConvertHsvToRgb(double hue, double saturation, double value)
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

            return DRW.Color.FromArgb(255, (byte)R, (byte)G, (byte)B);
        }

        private double Factorial(int n)
        {
            double nF = 1;

            for (int i = 1; i <= n; i++) nF *= i;

            return nF;
        }
    }
}

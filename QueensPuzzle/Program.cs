using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QueensPuzzle
{
    class Program
    {
        static int Length;
        static int Endex;
        static int Skips;

        static string Header;

        static string PosVisual;
        static string PosString;

        static void StatsHandler(object sender, VariantsCounter.StatsUpdatedEventArgs e)
        {
            Console.Clear();
            Console.WriteLine(Header);

            int endex = Endex;
            int[] posSlice;
            int xj;

            PosString = "";
            PosVisual = " ";

            // Board will be rotated 90 degrees clockwise
            for (int i = 0; i < Length; i++) // rows loop
            {
                posSlice = e.Positions[i];

                if (posSlice != null && posSlice[0] > -1 && posSlice[1] > -1)
                {
                    xj = posSlice[1];

                    // PosVisual 
                    for (int j = 0; j < xj; j++)
                    {
                        if (e.Occupancy[i, j]) PosVisual += "+";
                        else PosVisual += "-";
                    }
                     
                    PosVisual += "Q";

                    for (int j = xj + 1; j < Length; j++)
                    {
                        if (e.Occupancy[i, j]) PosVisual += "+";
                        else PosVisual += "-";
                    }

                    // PosString
                    PosString = string.Format("   {0,4} {1,4} {2,4} {3,4}", i, xj, posSlice[2], posSlice[3]);
                    PosVisual += PosString;
                }
                else
                {
                    for (int j = 0; j < Length; j++)
                    {
                        if (e.Occupancy[i, j]) PosVisual += "+";
                        else PosVisual += "-";
                    }
                }

                PosVisual += "\n\r ";
            }

            Console.WriteLine(PosVisual);

            if (!string.IsNullOrEmpty(e.Result))
            {
                Console.WriteLine();
                Console.WriteLine(e.Result);
            }
        }

        static void Main(string[] args)
        {
            VariantsCounter vc = new VariantsCounter();
            vc.StatsUpdated += StatsHandler;

            string input = "";

            while (input != "exit")
            {
                // Settings
                Console.Write("Enter length >> ");
                input = Console.ReadLine();

                if (!int.TryParse(input, out Length) || Length < 1) Length = 1;
                Endex = Length - 1;

                Console.Write("Enter skips >> ");
                input = Console.ReadLine();

                if (!int.TryParse(input, out Skips) || Skips < 0) Skips = Length;
                
                Header = string.Format("Length = {0}\n\rSkips = {1}\n\r", Length, Skips);

                // Main caclcs
                vc.FindPositions(Length, Skips);

                Console.Write("Show stats? y/n , or exit >> ");
                input = Console.ReadLine();
                Console.WriteLine();

                if (input == "Y" || input == "y")
                {
                    Console.WriteLine(vc.Report);
                }

                Console.WriteLine();
            }
        }
    }

    public class VariantsCounter
    {
        public class StatsUpdatedEventArgs : EventArgs
        {
            public readonly int[][] Positions;
            public readonly bool[,] Occupancy;
            public readonly string Report;
            public readonly string Result;

            public StatsUpdatedEventArgs(int[][] pos, bool[,] map, string report, string result = "")
            {
                Positions = pos;
                Occupancy = map;
                Report = report;
                Result = result;
            }
        }

        public event EventHandler<StatsUpdatedEventArgs> StatsUpdated;

        private int intLength = sizeof(int);

        private int boardSize = 8;
        private int[][] Positions;
        private bool[,] IsAttacked;
        private string VariantsByX;

        public int[][] PositionsArray { get { return Positions; } }
        public string Report { get { return VariantsByX; } }

        private void AddQueen(bool[,] assaultMap, int x, int y)
        {
            assaultMap[x, y] = true;

            int diagP = y + 1;
            int diagN = y - 1;

            for (int i = x + 1; i < boardSize; i++)
            {
                assaultMap[i, y] = true;

                if (diagP < boardSize)
                {
                    assaultMap[i, diagP] = true;
                    diagP++;
                }
                
                if (diagN > -1)
                {
                    assaultMap[i, diagN] = true;
                    diagN--;
                } 
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="length"></param>
        /// <param name="skips"></param>
        /// <returns></returns>
        public int[][] FindPositions(int length = 8, int skips = -1)
        {
            if (length < 1) length = 1;
            if (skips < 0) skips = length;

            boardSize = length;

            int endex = length - 1;
            int skipIter = skips;

            Positions = new int[length][]; // Positions[i] = { PosX, PosY, DiagPos, DiagNeg };
            int[] PosSlice = new int[4];
            //int PosX;
            //int PosY;
            int DiagPos; // parameter b = yi-xi for the positive slope diagonal
            int DiagNeg; // parameter b = yi+xi for the negative slope diagonal

            IsAttacked = new bool[length, length];

            // variables to store values for the each next iteration
            int[] freeYvariants = new int[length]; // the array of available free y values for the next xi
            int[] freeY = new int[length];
            int[] freeYcopy;
            int freeYnumTmp = 0;
            int freeYnum = 0;
            int nextInd;
            for (int i = 0; i < length; i++) freeY[i] = i; // the first column is entirely free

            // variables to store totals
            int variantsMax; // variantsMax = Max(variantsMax, variantsTotal)
            int variantsTotal;
            int variantsCurrent;

            VariantsByX = "";
            
            for (int i = 0; i < endex; i++) // --- x-values iteration MAIN LOOP -------------------------------------
            {
                if (i == 4)
                { }

                VariantsByX += string.Format("x = {0}\n\n\ry:\n\r", i);
                Positions[i] = new int[4] { -1, -1, -1, -1 };
                
                // reset values
                variantsMax = -1;
                freeYnum = 0;

                freeYcopy = new int[freeY.Length];
                Buffer.BlockCopy(freeY, 0, freeYcopy, 0, freeY.Length * intLength);

                foreach (int y in freeYcopy) // --- y-values iteration MAIN LOOP -------------------------------------
                {
                    // reset values
                    variantsTotal = 0;
                    variantsCurrent = 0;
                    freeYnumTmp = 0;

                    // Set the next Queen to the cell {i,y}
                    // PosY = y;
                    DiagPos = y - i;
                    DiagNeg = y + i;
                    
                    nextInd = i + 1;

                    // find the number of available free cells for the next xi...
                    for (int yj = 0; yj < length; yj++) // --- y-values iteration -------
                    {
                        if (!IsAttacked[nextInd, yj] && yj != y && yj - nextInd != DiagPos && yj + nextInd != DiagNeg)
                        {
                            freeYvariants[freeYnumTmp++] = yj;
                            variantsCurrent++;
                        }
                    }

                    variantsTotal += variantsCurrent;
                    VariantsByX += string.Format("{0,-4}: {1,4} ", y, variantsCurrent);

                    // ...and total number of the available free cells
                    for (int j = i + 2; j < length; j++) // --- x-values iteration -------
                    {
                        variantsCurrent = 0;

                        for (int yj = 0; yj < length; yj++) // --- y-values iteration -------
                        {
                            if (!IsAttacked[j, yj] && yj != y && yj - j != DiagPos && yj + j != DiagNeg)
                            {
                                variantsCurrent++;
                            }
                        }

                        variantsTotal += variantsCurrent;
                        VariantsByX += string.Format("{0,4} ", variantsCurrent);
                    } // --- end x-values iteration -------

                    VariantsByX += string.Format(": {0}\n\r", variantsTotal);

                    if (variantsTotal > variantsMax ||
                    (variantsTotal == variantsMax && skipIter-- == 0))
                    {
                        variantsMax = variantsTotal;

                        if (freeYnumTmp > 0)
                        {
                            freeYnum = freeYnumTmp;
                            freeY = new int[freeYnum];
                            Buffer.BlockCopy(freeYvariants, 0, freeY, 0, freeYnum * intLength);

                            PosSlice[0] = i;
                            PosSlice[1] = y;
                            PosSlice[2] = DiagPos;
                            PosSlice[3] = DiagNeg;
                        }
                    }
                } // --- end y-values iteration MAIN LOOP -------------------------------------

                if (variantsMax < 1 || freeYnum == 0)
                {
                    VariantsByX += "\n\n\r-----------------------------------------\n\r FAILED!";

                    StatsUpdated?.Invoke(this, new StatsUpdatedEventArgs(Positions, IsAttacked, VariantsByX, "FAILED"));

                    return Positions; // >>>>>>>>> FAILED! >>>>>>>>>
                }
                else
                {
                    Buffer.BlockCopy(PosSlice, 0, Positions[i], 0, 4 * intLength);
                    AddQueen(IsAttacked, i, PosSlice[1]);

                    VariantsByX += string.Format("---\n\ry = {0}\n\n\r", PosSlice[1]);
                    VariantsByX += string.Format("Pos: {0} {1} {2} {3}", i, PosSlice[1], PosSlice[2], PosSlice[3]);
                    VariantsByX += string.Format("\n\r-----------------------------------------\n\n\r");
                    
                    skipIter = skips;

                    StatsUpdated?.Invoke(this, new StatsUpdatedEventArgs(Positions, IsAttacked, VariantsByX));
                }
            } // --- end x-values iteration MAIN LOOP ---------------------------------------------------

            Positions[endex] = new int[4] { endex, freeY[0], freeY[0] - endex, freeY[0] + endex };

            VariantsByX += string.Format("x = {0}\n\ry = {1}\n\r", endex, freeY[0]);
            VariantsByX += string.Format("Pos: {0} {1} {2} {3}", endex, Positions[endex][1], Positions[endex][2], Positions[endex][3]);
            VariantsByX += string.Format("\n\r-----------------------------------------\n\r SOLVED! \n\n\n\r");

            StatsUpdated?.Invoke(this, new StatsUpdatedEventArgs(Positions, IsAttacked, VariantsByX, "SOLVED"));

            return Positions;
        }
    }
}

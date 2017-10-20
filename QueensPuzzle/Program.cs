using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.Serialization.Formatters.Binary;
using System.Security;
using System.Security.Permissions;
using System.Text;

namespace QueensPuzzle
{
    class Program
    {
        #region QP Variants Counter
        static int Length;
        static int Endex;
        static int Skips;

        static string Header;
        static bool[] IsOccupied;

        static StringBuilder PosVisual;

        public class VariantsCounter
        {
            public class StatsUpdatedEventArgs : EventArgs
            {
                public readonly int[][] Positions;
                public readonly bool[,] Occupancy;
                public readonly StringBuilder Report;
                public readonly string Result;

                public StatsUpdatedEventArgs(int[][] pos, bool[,] map, StringBuilder report, string result = "")
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
            private StringBuilder VariantsByX;

            public int[][] PositionsArray { get { return Positions; } }
            public string Report { get { return VariantsByX.ToString(); } }

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

                VariantsByX = new StringBuilder(16 * length);

                for (int i = 0; i < endex; i++) // --- x-values iteration MAIN LOOP -------------------------------------
                {
                    if (i == 4)
                    { }

                    VariantsByX.AppendFormat("x = {0}\n\n\ry:\n\r", i);
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
                        VariantsByX.AppendFormat("{0,-4}: {1,4} ", y, variantsCurrent);

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
                            VariantsByX.AppendFormat("{0,4} ", variantsCurrent);
                        } // --- end x-values iteration -------

                        VariantsByX.AppendFormat(": {0}\n\r", variantsTotal);

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
                        VariantsByX.Append("\n\n\r-----------------------------------------\n\r FAILED!");

                        StatsUpdated?.Invoke(this, new StatsUpdatedEventArgs(Positions, IsAttacked, VariantsByX, "FAILED"));

                        return Positions; // >>>>>>>>> FAILED! >>>>>>>>>
                    }
                    else
                    {
                        Buffer.BlockCopy(PosSlice, 0, Positions[i], 0, 4 * intLength);
                        AddQueen(IsAttacked, i, PosSlice[1]);

                        VariantsByX.AppendFormat("---\n\ry = {0}\n\n\r", PosSlice[1]);
                        VariantsByX.AppendFormat("Pos: {0} {1} {2} {3}", i, PosSlice[1], PosSlice[2], PosSlice[3]);
                        VariantsByX.AppendFormat("\n\r-----------------------------------------\n\n\r");

                        skipIter = skips;

                        StatsUpdated?.Invoke(this, new StatsUpdatedEventArgs(Positions, IsAttacked, VariantsByX));
                    }
                } // --- end x-values iteration MAIN LOOP ---------------------------------------------------

                Positions[endex] = new int[4] { endex, freeY[0], freeY[0] - endex, freeY[0] + endex };

                VariantsByX.AppendFormat("x = {0}\n\ry = {1}\n\r", endex, freeY[0]);
                VariantsByX.AppendFormat("Pos: {0} {1} {2} {3}", endex, Positions[endex][1], Positions[endex][2], Positions[endex][3]);
                VariantsByX.AppendFormat("\n\r-----------------------------------------\n\r SOLVED! \n\n\n\r");

                StatsUpdated?.Invoke(this, new StatsUpdatedEventArgs(Positions, IsAttacked, VariantsByX, "SOLVED"));

                return Positions;
            }
        }

        static void StatsHandler(object sender, VariantsCounter.StatsUpdatedEventArgs e)
        {
            Console.Clear();
            Console.WriteLine(Header);

            int endex = Endex;
            int[] posSlice;
            int xj;
            int freeNum;

            PosVisual.Clear();
            PosVisual.Append(" ");

            // Board will be rotated 90 degrees clockwise
            for (int i = 0; i < Length; i++) // rows loop
            {
                posSlice = e.Positions[i];

                if (posSlice != null && posSlice[0] > -1 && posSlice[1] > -1)
                {
                    xj = posSlice[1];
                    IsOccupied[xj] = true;

                    // PosVisual 
                    for (int j = 0; j < xj; j++)
                    {
                        if (e.Occupancy[i, j]) PosVisual.Append('\u25aa');
                        else PosVisual.Append('-');
                    }
                     
                    PosVisual.Append('\u25a0');

                    for (int j = xj + 1; j < Length; j++)
                    {
                        if (e.Occupancy[i, j]) PosVisual.Append('\u25aa');
                        else PosVisual.Append('-');
                    }

                    // PosString
                    PosVisual.AppendFormat("   {0,4} {1,4} {2,4} {3,4}", i, xj, posSlice[2], posSlice[3]);
                }
                else
                {
                    freeNum = Length;

                    for (int j = 0; j < Length; j++)
                    {
                        if (e.Occupancy[i, j])
                        {
                            PosVisual.Append('\u25aa');
                            freeNum--;
                        }
                        else PosVisual.Append('-');
                    }

                    PosVisual.AppendFormat(" {0,3:N0}", freeNum);
                }

                PosVisual.Append("\n\r ");
            }

            foreach (bool x in IsOccupied) PosVisual.AppendFormat("{0}", x ? "1" : "\u208b");

            PosVisual.Append("\n\r ");

            Console.WriteLine(PosVisual);

            if (!string.IsNullOrEmpty(e.Result))
            {
                Console.WriteLine();
                Console.WriteLine(e.Result);
            }

            Console.ReadKey(); // ### For demonstration purposes ###
            //Console.WindowTop = ConsoleWindowTop;
            //Console.WindowLeft = ConsoleWindowLeft;
            //Console.SetWindowPosition(0, 0);
            //Console.WindowTop = 0;
            //Console.WindowLeft = 0;
        }

        static void QPCounter()
        {
            VariantsCounter vc = new VariantsCounter();
            vc.StatsUpdated += StatsHandler;

            string input = "";

            Console.Clear();

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
                IsOccupied = new bool[Length];

                // Main caclcs
                PosVisual = new StringBuilder(Length * (1 + Length + 2 + 20 + 2));
                Console.Clear();
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

            vc.StatsUpdated -= StatsHandler;
            vc = null;
        }
        #endregion

        #region QPDD Solver
        static List<int[]> StringToList(string input)
        {
            List<int[]> positions;

            int ind1 = -1, ind2 = 0, num = input.Length;
            int x, y;
            string inputSub = "";

            if (num > 4) positions = new List<int[]>(num / 4);
            else positions = new List<int[]>(1);
            
            bool goFlag = true;

            while (goFlag)
            {
                ind1++;
                ind2 = input.IndexOf(';', ind1);

                if (ind2 > ind1) inputSub = input.Substring(ind1, ind2 - ind1);
                else if (ind2 == ind1) continue;
                else if (ind2 < 0)
                {
                    inputSub = input.Substring(ind1);
                    goFlag = false;
                }

                ind1 = inputSub.IndexOf(',');

                if (ind1 > 0)
                {
                    if (int.TryParse(inputSub.Substring(0, ind1), out x) &&
                        int.TryParse(inputSub.Substring(++ind1), out y))
                    {
                        positions.Add(new int[4] { x, y, y - x, y + x });
                    }
                }

                ind1 = ind2;
            }

            return positions;
        }

        private static string WriteToFile(IList<int[]> Positions, string folder)
        {
            int length = Positions.Count;

            string errStr = "";

            string date = DateTime.UtcNow.ToString("yyyy-MMdd_H-mm-ss");
            string name = string.Format("{0}_{1}_{2}{3}", folder.ToLower(), length, date, ".qps");

            string DefaultPath;

            FileStream fStream = null;

            bool binOk = true;

            try
            {
                DefaultPath = Directory.GetCurrentDirectory();
                DefaultPath = Path.Combine(DefaultPath, folder);
                name = Path.Combine(DefaultPath, name);

                if (!Directory.Exists(DefaultPath)) Directory.CreateDirectory(DefaultPath);

                BinaryFormatter binaryFmt = new BinaryFormatter();

                fStream = new FileStream(name, FileMode.Create);

                binaryFmt.Serialize(fStream, Positions);
            }
            catch (Exception e)
            {
                binOk = false;
                errStr = e.ToString();
            }
            finally
            {
                if (fStream != null) fStream.Close();
            }

            // write solution to txt
            if (binOk)
            {
                name = Path.ChangeExtension(name, ".txt");
                StreamWriter txtStream = null;

                try
                {
                    txtStream = File.CreateText(name);

                    txtStream.WriteLine("   {0} placements", length);
                    txtStream.WriteLine("-----------------------------------");
                    txtStream.WriteLine("    Row     Clm    bPos    bNeg");

                    foreach (int[] pos in Positions)
                    {
                        txtStream.WriteLine("{0,7} {1,7} {2,7} {3,7}", pos[0], pos[1], pos[2], pos[3]);
                    }
                }
                catch (Exception e)
                {
                    errStr += string.Format("\n\r -------------------------\n\n\r{0}\n\r", e.ToString());
                }
                finally
                {
                    if (txtStream != null) txtStream.Close();
                }
            }

            return errStr;
        }

        static void QPDDSolver()
        {
            QPSDamDetect QPSdd = null;
            QPSDamDetect.IOConsole io;
            List<int[]> posList = null;
            StringBuilder Header = new StringBuilder(15 * 50 + 200); // rough estimate

            int[] ThreadsByLevel;

            int Length = 8;
            int ThreadsNum = 1;
            int iniNum = 1;
            int zqLevel = 1;
            double levelRatio = 0;
            bool forceShift = false;
            int SyncPeriod = 300;

            QPSDamDetect.ThreadsSpread.Law spreadLaw = QPSDamDetect.ThreadsSpread.Law.Exponential;
            QPSDamDetect.ThreadsSpread spreadData = new QPSDamDetect.ThreadsSpread(spreadLaw, ThreadsNum, iniNum, zqLevel, levelRatio);

            int wT, ioT;
            System.Threading.ThreadPool.GetMaxThreads(out wT, out ioT);

            int wTmin, ioTmin;
            System.Threading.ThreadPool.GetMinThreads(out wTmin, out ioTmin);

            int ind = 0;
            int qAdded = 0;
            bool isArmed = false;

            string input = "";

            while (input != "exit")
            {
                isArmed = QPSdd != null;
                ind = 0;

                Header.Clear();
                Header.Append("/// QPS DAM DETECT ///\n\n\r");
                Header.AppendFormat("{0,-2}Length ...............: {1}\n\r", isArmed ? "" : (ind++).ToString(), Length);
                Header.AppendFormat("  Queens initially added: {0}\n\n\r", qAdded);
                Header.AppendFormat(" ThreadPool.GetMaxThreads: {0}\n\r", wT);
                Header.AppendFormat(" ThreadPool.GetMinThreads: {0}\n\r", wTmin);
                Header.Append("\n\r THREADS SPREAD:\n\n\r");
                Header.AppendFormat("{0,-2}Spread law ...........: {1}\n\r", isArmed ? "" : (ind++).ToString(), spreadData.SpreadLaw);
                Header.AppendFormat("{0,-2}Total number .........: {1}\n\r", isArmed ? "" : (ind++).ToString(), spreadData.TotalNumber);
                Header.AppendFormat("{0,-2}Initial number .......: {1}\n\r", isArmed ? "" : (ind++).ToString(), spreadData.IniNumber);
                Header.AppendFormat("{0,-2}Zero-quantity level ..: {1}\n\r", isArmed ? "" : (ind++).ToString(), spreadData.ZeroNumberLevel);
                Header.AppendFormat("{0,-2}Level ratio ..........: {1}\n\r", isArmed ? "" : (ind++).ToString(), spreadData.LevelRatio);
                Header.AppendFormat("{0,-2}Force shift ..........: {1}\n\r", isArmed ? "" : (ind++).ToString(), spreadData.ForceShift);
                Header.AppendFormat("{0,-2}Sync period in ms. ...: {1}\n\r", isArmed ? "" : (ind++).ToString(), SyncPeriod);

                if (isArmed)
                {
                    if (posList == null)
                    {
                        Header.Append("\n\r INITIAL POSITIONS: empty\n\r");
                    }
                    else
                    {
                        Header.AppendFormat("\n\r INITIAL POSITIONS: {0} ; shift: {1},{2}\n\r", 
                                            posList.Count.ToString(), posList.Min(p => p[0]), posList.Min(p => p[1]));
                    }

                    Header.Append("\n\r---------");
                    Header.Append("\n\r Enter: \"start\" | \"reset\" | \"exit\" | \n\r");
                    Header.Append(" \"add:\" [x1,y1 ; x2,y2 ; ...] [\"inipos\"] | \n\r");
                    Header.Append(" \"inipos:\" [\"generate\" length [\"rnd\" | \"rook\"]] | [\"shift\" x,y] | \n\r");
                    Header.Append(" \"save:\" [\"inipos\" | \"added\"]\n\r");
                }
                else
                {
                    Header.Append("\n\r---------");
                    Header.Append("\n\r Enter: \"index:value\" | \"submit\" | \"exit\"\n\r");
                    Header.Append(" Enter \"?\" for the 2th,3th or 4th parameter for the auto-adjustment\n\r");
                    Header.Append(" Enter \"0\" for the 4th parameter to use the specified level ratio\n\r");
                }
                
                Console.Clear();
                Console.WriteLine(Header);
                Console.Write(">> ");

                input = Console.ReadLine();

                if (isArmed)
                {
                    int colonInd = input.IndexOf(':');
                    int value = -1;
                    string[] cmds;
                    string inputSub = colonInd > 0 ? input.Substring(0, colonInd).Trim() : input.Trim();

                    switch (inputSub)
                    {
                        case "start": // START -------------------------------------------------------------------------
                            io = new QPSDamDetect.IOConsole();
                            QPSDamDetect.SyncPoint += io.SyncHandler;

                            inputSub = input.Substring(++colonInd).Trim();

                            if (inputSub == "count" || inputSub == "jc") QPSdd.Start(true);
                            else QPSdd.Start();

                            QPSDamDetect.SyncPoint -= io.SyncHandler;
                            QPSDamDetect.Clear();

                            break;

                        case "reset": // RESET -------------------------------------------------------------------------
                            qAdded = 0;
                            posList = null;
                            QPSdd = null;
                            break;

                        case "add": // ADD -----------------------------------------------------------------------------
                            inputSub = input.Substring(++colonInd).Trim();
                            colonInd = inputSub.IndexOfAny(new char[2] { ';', ',' });

                            if (colonInd > 0)
                            {
                                posList = StringToList(inputSub);

                                if (posList.Count > 0) qAdded += QPSdd.AddQueens(posList.ToArray()); 
                            }
                            else if ((inputSub == "inipos" || inputSub == "ini") && posList != null && posList.Count > 0)
                            {
                                qAdded += QPSdd.AddQueens(posList.ToArray());
                            }

                            break;

                        case "ini":
                        case "inipos": // INIPOS -------------------------------------------------------------------------
                            inputSub = input.Substring(++colonInd).Trim();

                            cmds = inputSub.Split(new char[1] { ' ' }, 3, StringSplitOptions.RemoveEmptyEntries);

                            switch (cmds[0])
                            {
                                case "gen":
                                case "generate":
                                    if (cmds.Length == 3 && int.TryParse(cmds[1], out value) && value > 1)
                                    {
                                        switch (cmds[2])
                                        {
                                            case "rnd":
                                            case "random":
                                                posList = QPSDamDetect.GenerateRandom(value).ToList();
                                                break;

                                            case "rook":
                                            case "rooks":
                                                posList = QPSDamDetect.GenerateRooks(value).ToList();
                                                break;
                                        }
                                    }
                                    break;

                                case "shift":
                                    if (posList != null && cmds.Length > 1)
                                    {
                                        colonInd = inputSub.IndexOf(',');

                                        if (colonInd > 6)
                                        {
                                            int x, y;

                                            if (int.TryParse(inputSub.Substring(5, colonInd - 5), out x) &&
                                                int.TryParse(inputSub.Substring(++colonInd), out y))
                                            {
                                                foreach (int[] pos in posList)
                                                {
                                                    pos[0] += x;
                                                    pos[1] += y;
                                                    pos[2] += y - x;
                                                    pos[3] += y + x;
                                                }
                                            }
                                        }
                                    }

                                    break;
                            }

                            break;

                        case "save": // SAVE -------------------------------------------------------------------------
                            inputSub = input.Substring(++colonInd).Trim();

                            IList<int[]> iPos = null;
                            string str = "";

                            if (inputSub.Contains("inipos") && posList != null)
                            {
                                iPos = posList;
                                str = "Stuff";
                            }
                            else if (inputSub.Contains("added"))
                            {
                                iPos = QPSdd.GetInitialPositions();
                                str = "Initial";
                            }

                            if (iPos != null)
                            {
                                str = WriteToFile(iPos, str);

                                if (str == "") Console.WriteLine("\n\r Successfully saved");
                                else Console.WriteLine("\n\r Failed to save:\n\r{0}", str);

                                Console.WriteLine("\n\r >> Press any key to continue <<");
                                Console.ReadKey();
                            }

                            break;
                    }
                }
                else if (input == "Submit" || input == "submit")
                {
                    QPSdd = new QPSDamDetect(Length, spreadData, null, SyncPeriod);
                }
                else if (input != "Exit" && input != "exit")
                {
                    string inputSub = "";
                    int colonInd = input.IndexOf(':');
                    int index = -1;
                    double value = -1;
                    bool SetParam = false;
                    
                    if (colonInd > 0)
                    {
                        inputSub = input.Substring(0, colonInd);

                        if (int.TryParse(inputSub, out index))
                        {
                            inputSub = input.Substring(++colonInd).Trim();

                            if (double.TryParse(inputSub, out value)) SetParam = true;
                            else if (index == 1) { value = -1; SetParam = true; }
                            else if (inputSub == "?" && (index == 2 || index == 3 || index == 4)) { value = -1; SetParam = true; }
                        }
                    }
                    
                    if (SetParam)
                    {
                        switch (index)
                        {
                            case 0: // Length
                                if ((int)value > 1) Length = (int)value;
                                break;

                            case 1: // Spread law
                                if ((int)value == 0 || 
                                    inputSub == "L" || inputSub == "l" ||
                                    inputSub == "Linear" || inputSub == "linear") spreadLaw = QPSDamDetect.ThreadsSpread.Law.Linear;
                                else if ((int)value == 1 ||
                                    inputSub == "E" || inputSub == "e" ||
                                    inputSub == "Exponential" || inputSub == "exponential") spreadLaw = QPSDamDetect.ThreadsSpread.Law.Exponential;
                                break;

                            case 2: // Total number
                                if ((int)value > 0) ThreadsNum = (int)value;
                                else ThreadsNum = QPSDamDetect.ThreadsSpread.FindOne(spreadLaw, iniNum: iniNum, zeroLevel: zqLevel);
                                break;

                            case 3: // Initial number
                                if ((int)value > 0) iniNum = (int)value;
                                else iniNum = QPSDamDetect.ThreadsSpread.FindOne(spreadLaw, totNum: ThreadsNum, zeroLevel: zqLevel);
                                break;

                            case 4: // Zero-quantity level
                                if ((int)value > -1) zqLevel = (int)value;
                                else zqLevel = QPSDamDetect.ThreadsSpread.FindOne(spreadLaw, totNum: ThreadsNum, iniNum: iniNum);
                                break;

                            case 5: // Level ratio
                                levelRatio = value;
                                break;

                            case 6: // Force shift
                                forceShift = value > 0;
                                break;

                            case 7: // Sync period
                                if (value >= 10) SyncPeriod = (int)value;
                                break;

                        } // end switch (index)

                        spreadData = new QPSDamDetect.ThreadsSpread(spreadLaw, ThreadsNum, iniNum, zqLevel, levelRatio, forceShift);
                    }
                }
            } // end while loop ----------------------------------------------------------------
        }
        #endregion

        #region Solutions Spreading
        public class SolutionsSpreading
        {
            public delegate bool DomainsFilter(int[] domain);

            public static bool IsOverlapsFree(int[] combi)
            {
                if (combi.Distinct().Count() == combi.Length &&
                    combi.Select((x, i) => i - x).Distinct().Count() == combi.Length &&
                    combi.Select((x, i) => i + x).Distinct().Count() == combi.Length)
                {
                    return true;
                }
                else return false;
            }

            public int Factorial(int nMax, int nMin = 1)
            {
                int nF = nMin;

                for (int i = ++nMin; i <= nMax; i++) nF *= i;

                return nF;
            }

            public BigInteger FactorialBI(int nMax, int nMin = 1)
            {
                BigInteger nF = nMin;

                for (int i = ++nMin; i <= nMax; i++) nF *= i;

                return nF;
            }

            private DomainsFilter SetFilter(int index)
            {
                switch (index)
                {
                    case 0:
                        FilterIndex = 0;
                        return IsOverlapsFree;

                    default:
                        FilterIndex = -1;
                        return d => true;
                }
            }

            public List<ulong> BuildSolSpreading(int brdLength, int domLength, DomainsFilter domFilter)
            {
                if (brdLength < 4 || domLength < 1 || brdLength <= domLength) return null;

                int maxNum = Factorial(brdLength, brdLength - domLength + 1);

                ulong solNum;

                SpreadingList = new List<ulong>(maxNum);
                DomainsList = new List<int[]>(maxNum);
                domIterator = new int[domLength];
                //domClmsIndexes = new int[3] { boardLength / 2 - 1, boardLength / 2, boardLength / 2 + 1 }; // !!! TODO !!!

                int[] tmpArr;
                int int32arrSize = domLength * sizeof(int);
                
                QPSDamDetect qpSolver = null;

                QPSDamDetect.ThreadsSpread threadsDat =
                    new QPSDamDetect.ThreadsSpread(totNum: 8, iniNum: brdLength - domLength, zeroNumLevel: 3 * brdLength / 4);

                int rank;
                bool run = true;
                
                Timer.Restart();

                while (run && !AbortFlag)
                {
                    if (domFilter(domIterator))
                    {
                        if (Timer.ElapsedMilliseconds > 300)
                        {
                            ioHandler();
                            Timer.Restart();
                        }
                        
                        tmpArr = new int[domLength];
                        Buffer.BlockCopy(domIterator, 0, tmpArr, 0, int32arrSize);

                        DomainsList.Add(tmpArr);

                        qpSolver = new QPSDamDetect(brdLength, threadsDat, syncPeriod: 300);
                        qpSolver.AddQueens(domIterator.Select((x, i) => new int[2] { x, i }).ToArray());
                        // qpSolver.AddQueens(domIterator.Select((x, i) => new int[2] { x, domClmsIndexes[i] }).ToArray()); // !!! TODO !!!
                        qpSolver.Start(true);

                        solNum = (ulong)QPSDamDetect.SolutionsCount;

                        SpreadingList.Add(solNum);

                        sts_Progress(solNum);
                    }

                    // Iterating through domains
                    rank = domLength - 1;

                    while (rank >= 0)
                    {
                        if (++domIterator[rank] == brdLength)
                        {
                            if (rank == 0) run = false;

                            domIterator[rank] = 0;
                            rank--;
                        }
                        else break;
                    }
                }

                Timer.Stop();

                return SpreadingList;
            }

            public List<ulong> BuildSolSpreading(List<int[]> domList)
            {
                int domLength = domList[0].Length;
                int brdLength = domList.Max(d => d.Max());

                ulong solNum;

                SpreadingList = new List<ulong>(domList.Count);
                DomainsList = domList;

                QPSDamDetect qpSolver = null;

                QPSDamDetect.ThreadsSpread threadsDat =
                    new QPSDamDetect.ThreadsSpread(totNum: 8, iniNum: brdLength - domLength, zeroNumLevel: 3 * brdLength / 4);

                Timer.Restart();

                foreach (int[] domain in domList)
                {
                    domIterator = domain;

                    if (Timer.ElapsedMilliseconds > 300)
                    {
                        ioHandler();
                        Timer.Restart();
                    }

                    qpSolver = new QPSDamDetect(brdLength, threadsDat, syncPeriod: 300);
                    qpSolver.AddQueens(domain.Select((x, i) => new int[2] { x, i }).ToArray());
                    qpSolver.Start(true);

                    solNum = (ulong)QPSDamDetect.SolutionsCount;

                    SpreadingList.Add(solNum);

                    if (AbortFlag) break;

                    sts_Progress(solNum);
                }

                Timer.Stop();

                return SpreadingList;
            }

            private void ioHalted(ref bool pause, ref bool abort)
            {
                string input = "";
                string inputSub;
                int colonInd;
                
                while (input != "X")
                {
                    ioC_Progress();

                    if (pause)
                    {
                        Console.WriteLine("\n\r < < PAUSE > >\n\n\r---------\n\r");
                        Console.WriteLine("Enter: \"save\" [\":txt\" | \":ids\"] | \"resume\" | \"cancel\"\n\r");
                    }
                    else
                    {
                        Console.WriteLine("\n\n\r---------\n\r");
                        Console.WriteLine("Enter: \"save\" [\":txt\" | \":ids\"] | \"pause\"\n\r");
                    }
                    
                    Console.Write(">> ");

                    input = Console.ReadLine().Trim();

                    colonInd = input.IndexOf(':');

                    inputSub = colonInd > 0 ? input.Substring(0, colonInd).Trim() : input;

                    switch (inputSub)
                    {
                        case "save":
                            string err = "";
                            
                            if (colonInd > 0)
                            {
                                inputSub = input.Substring(++colonInd).Trim();

                                if (inputSub == "ids") err = WriteToFile(WriteMode.bin);
                                else if (inputSub == "txt") err = WriteToFile(WriteMode.txt);
                            }
                            else err = WriteToFile();

                            if (err == "") Console.WriteLine("\n\r Successfully saved");
                            else Console.WriteLine("\n\r Failed to save:\n\r{0}", err);

                            Console.WriteLine("\n\r >> Press any key to continue <<");
                            Console.ReadKey();

                            if (!pause) input = "X";
                            break;

                        case "resume":
                            pause = false;
                            input = "X";
                            break;

                        case "cancel":
                            pause = false;
                            abort = true;
                            input = "X";
                            break;

                        case "pause":
                        case "p":
                            if (!pause)
                            {
                                pause = true;
                                input = "X";
                            }
                            break;

                        default:
                            if (!pause) input = "X";
                            break;
                    }
                }
            }

            /// <summary>
            /// Handles console IO istead of the QPS_ioHandler in cases
            /// QPSDamDetect.SyncPoint event does not have time to be raised
            /// </summary>
            private void ioHandler()
            {
                if (HaltedFlag)
                {
                    ioHalted(ref HaltedFlag, ref AbortFlag);
                }
                else
                {
                    ioC_Progress();

                    if (Console.KeyAvailable)
                    {
                        ConsoleKeyInfo ki = Console.ReadKey(true);

                        switch (ki.Key)
                        {
                            case ConsoleKey.Escape:
                            case ConsoleKey.P:
                                HaltedFlag = true;
                                break;

                            case ConsoleKey.Enter:
                                HaltedFlag = true;
                                break;

                            case ConsoleKey.S:
                                string err = WriteToFile(prefix: "wip_");

                                if (err == "") Console.WriteLine("\n\r Successfully saved");
                                else Console.WriteLine("\n\r Failed to save:\n\r{0}", err);

                                Console.WriteLine("\n\r >> Press any key to continue <<");
                                Console.ReadKey();

                                break;
                        }
                    }
                }
            }

            private void QPS_ioHandler(object sender, QPSDamDetect.SyncEventArgs sea)
            {
                if (HaltedFlag)
                {
                    ioHalted(ref sea.SyncPause, ref sea.SyncAbort);

                    HaltedFlag = sea.SyncPause;
                    AbortFlag = sea.SyncAbort;
                }
                else
                {
                    ioC_Progress();

                    if (Console.KeyAvailable)
                    {
                        ConsoleKeyInfo ki = Console.ReadKey(true);

                        switch (ki.Key)
                        {
                            case ConsoleKey.Escape:
                            case ConsoleKey.P:
                                sea.SyncPause = true;
                                HaltedFlag = true;
                                break;

                            case ConsoleKey.Enter:
                                HaltedFlag = true;
                                break;

                            case ConsoleKey.S:
                                string err = WriteToFile(prefix: "wip_");

                                if (err == "") Console.WriteLine("\n\r Successfully saved");
                                else Console.WriteLine("\n\r Failed to save:\n\r{0}", err);

                                Console.WriteLine("\n\r >> Press any key to continue <<");
                                Console.ReadKey();

                                break;
                        }
                    }
                }

                // Reset the Timer to prevent redundant console output by the ioHandler()
                Timer.Restart();
            }

            private void sts_Progress(ulong snum)
            {
                domCounter++;
                solTotal += snum;

                solAvg = (ulong)(solTotal / domCounter);

                if (snum < solMin) solMin = snum;
                if (snum > solMax) solMax = snum;
            }

            private void ioC_Progress()
            {
                // --- build tmp Header ---
                tmpHeader.Clear();
                tmpHeader.Append("\n\r PROGRESS:\n\n\r");

                tmpHeader.Append("Domain ..............:");
                foreach (int i in domIterator) tmpHeader.AppendFormat(" {0,-5}", i);
                tmpHeader.Append("\n\r");
                tmpHeader.AppendFormat("Solutions count .....: {0}\n\r", QPSDamDetect.SolutionsCount.ToString("N0"));
                
                tmpHeader.Append("\n\r STATISTIC:\n\n\r");
                tmpHeader.AppendFormat("Domains count .......: {0}\n\r", domCounter);
                tmpHeader.AppendFormat("Solutions total .....: {0}\n\r", solTotal.ToString("N0"));
                tmpHeader.AppendFormat("Max in domain .......: {0}\n\r", solMax);
                tmpHeader.AppendFormat("Avg in domain .......: {0}\n\r", solAvg);
                tmpHeader.AppendFormat("Min in domain .......: {0}\n\r", solMin);

                Console.Clear();
                Console.Write(rtcHeader);
                Console.Write(tmpHeader);
            }

            private void ioSet()
            {
                // --- Define permissions for file writing -------
                try
                {
                    DefaultPath = Directory.GetCurrentDirectory();

                    FileIOPermission permitRW = new FileIOPermission(FileIOPermissionAccess.Read | FileIOPermissionAccess.Write, DefaultPath);
                    permitRW.Demand();
                }
                catch (SecurityException SE)
                {
                    IOexceptionStr = SE.ToString();
                    IOexceptionFlag = true;
                }
                catch (Exception E)
                {
                    IOexceptionStr += "\n\r-------------------------------------\n\r";
                    IOexceptionStr += E.ToString();
                    IOexceptionFlag = true;
                }

                if (IOexceptionFlag)
                {
                    Console.WriteLine("\n\r>>>>>>> System IO WARNING! <<<<<<<\r\n");
                    Console.WriteLine("You will not be able to save/load progress data and results:\n\r");
                    Console.WriteLine(IOexceptionStr);

                    Console.WriteLine("\n\n\r>>> Press any key to continue <<<");
                    Console.ReadKey();
                }
                else
                {
                    DefaultPath = Path.Combine(DefaultPath, "Spreads");

                    if (!Directory.Exists(DefaultPath))
                    {
                        Directory.CreateDirectory(DefaultPath);
                    }
                }
            }

            private enum WriteMode { bin, txt, all }

            private string SaveProgress()
            {
                if (IOexceptionFlag) return " " + IOexceptionStr;

                string errStr = "";

                int length = SpreadingList.Count;
                int[] domain;

                BinaryWriter BW = null;

                try
                {
                    BW = new BinaryWriter(new FileStream(Path.Combine(DefaultPath, progressFName), FileMode.Create));

                    // write info
                    BW.Write(length);
                    BW.Write(boardLength);
                    BW.Write(domainLength);
                    BW.Write(FilterIndex);

                    for (int i = 0; i < length; i++)
                    {
                        // write domain
                        domain = DomainsList[i];

                        for (int j = 0; j < domainLength; j++) BW.Write(domain[j]);

                        // write solutions number
                        BW.Write(SpreadingList[i]);
                    }
                }
                catch (Exception e)
                {
                    errStr = e.ToString();
                }
                finally
                {
                    if (BW != null) BW.Close();
                }

                return errStr;
            }

            private string WriteToFile(WriteMode mode = WriteMode.all, string prefix = "")
            {
                if (IOexceptionFlag) return " " + IOexceptionStr;

                string errStr = "";
                string name;
                string date = DateTime.UtcNow.ToString("yyyy-MMdd_H-mm-ss");

                StringBuilder domStr = new StringBuilder(7 * domainLength);

                int length = SpreadingList.Count;
                int[] domain;
                int x;
                ulong n;

                BinaryWriter BW = null;
                StreamWriter SWsol = null;
                StreamWriter SWdom = null;

                try
                {
                    switch (mode)
                    {
                        case WriteMode.all:
                            name = string.Format("{0}L{1}-D{2}_{3}.{4}", prefix, boardLength, domainLength, date, "ids");
                            BW = new BinaryWriter(new FileStream(Path.Combine(DefaultPath, name), FileMode.Create));

                            name = string.Format("{0}L{1}-D{2}_{3}_Sol.{4}", prefix, boardLength, domainLength, date, "txt");
                            SWsol = new StreamWriter(new FileStream(Path.Combine(DefaultPath, name), FileMode.Create));

                            name = string.Format("{0}L{1}-D{2}_{3}_Dom.{4}", prefix, boardLength, domainLength, date, "txt");
                            SWdom = new StreamWriter(new FileStream(Path.Combine(DefaultPath, name), FileMode.Create));

                            // write info
                            BW.Write(length);
                            BW.Write(boardLength);
                            BW.Write(domainLength);
                            BW.Write(FilterIndex);

                            for (int i = 0; i < length; i++)
                            {
                                domStr.Clear();

                                // --- write IDS ---
                                // write domain
                                domain = DomainsList[i];

                                for (int j = 0; j < domainLength; j++)
                                {
                                    x = domain[j];

                                    BW.Write(x);

                                    domStr.AppendFormat("{0,7}", x);
                                }

                                // write solutions number
                                n = SpreadingList[i];

                                BW.Write(n);

                                // --- write txt ---
                                SWsol.WriteLine(n);
                                SWdom.WriteLine(domStr);
                            }

                            break;

                        case WriteMode.bin:
                            name = string.Format("{0}L{1}-D{2}_{3}.{4}", prefix, boardLength, domainLength, date, "ids");
                            BW = new BinaryWriter(new FileStream(Path.Combine(DefaultPath, name), FileMode.Create));

                            // write info
                            BW.Write(length);
                            BW.Write(boardLength);
                            BW.Write(domainLength);
                            BW.Write(FilterIndex);

                            for (int i = 0; i < length; i++)
                            {
                                // write domain
                                domain = DomainsList[i];

                                for (int j = 0; j < domainLength; j++) BW.Write(domain[j]);

                                // write solutions number
                                BW.Write(SpreadingList[i]);
                            }

                            break;

                        case WriteMode.txt:
                            name = string.Format("{0}L{1}-D{2}_{3}_Sol.{4}", prefix, boardLength, domainLength, date, "txt");
                            SWsol = new StreamWriter(new FileStream(Path.Combine(DefaultPath, name), FileMode.Create));

                            name = string.Format("{0}L{1}-D{2}_{3}_Dom.{4}", prefix, boardLength, domainLength, date, "txt");
                            SWdom = new StreamWriter(new FileStream(Path.Combine(DefaultPath, name), FileMode.Create));

                            for (int i = 0; i < length; i++)
                            {
                                domStr.Clear();

                                // write domain
                                domain = DomainsList[i];

                                for (int j = 0; j < domainLength; j++) domStr.AppendFormat("{0,7}", domain[j]);

                                // write solutions number
                                SWsol.WriteLine(SpreadingList[i]);
                                SWdom.WriteLine(domStr);
                            }

                            break;
                    }
                }
                catch (Exception e)
                {
                    errStr = e.ToString();
                }
                finally
                {
                    if (BW != null) BW.Close();
                    if (SWsol != null) SWsol.Close();
                    if (SWdom != null) SWdom.Close();
                }
                
                return errStr;
            }

            int boardLength = 8;
            int domainLength = 3;

            int FilterIndex = 0;

            private List<ulong> SpreadingList;
            private List<int[]> DomainsList;
            private int[] domIterator;
            private int[] domClmsIndexes;

            private StringBuilder rtcHeader;
            private StringBuilder tmpHeader;

            private BigInteger solTotal;

            private Stopwatch Timer = new Stopwatch();
            private long AutoSavePeriod = 300 * 1000;
            private bool AutoSaveFlag = true;
            private bool AbortFlag = false;
            private bool HaltedFlag = false; 

            private int domCounter;
            private ulong solMin, solMax, solAvg;

            string DefaultPath;
            string IOexceptionStr;
            bool IOexceptionFlag;

            string progressFName;

            private void SetGlobals()
            {
                // --- auotosave file name ---
                string date = DateTime.UtcNow.ToString("yyyy-MMdd_H-mm-ss");
                progressFName = string.Format("WIP_L{0}-D{1}_LaunchT{2}.{3}", boardLength, domainLength, date, "ids");

                // --- reset sync flags ---
                HaltedFlag = false;
                AbortFlag = false;
                
                // --- reset counters ---
                solAvg = 0;
                solMin = ulong.MaxValue;
                solMax = 0;
                solTotal = 0;
                domCounter = 0;
            }

            public void Start()
            {
                ioSet();

                rtcHeader = new StringBuilder(3 * 50);
                tmpHeader = new StringBuilder(11 * 100);
                
                string input = "";
                string inputSub;
                int colonInd;

                int value;

                bool startupFlag = true;

                while (input != "exit")
                {
                    if (startupFlag)
                    {
                        rtcHeader.Clear();
                        rtcHeader.Append("/// Solutions Spreading ///\n\n\r");
                        rtcHeader.AppendFormat("0 Board length ........: {0}\n\r", boardLength);
                        rtcHeader.AppendFormat("1 Domain length .......: {0}\n\r", domainLength);

                        rtcHeader.Append("\n\n\r---------\n\r");
                        rtcHeader.Append("Enter: index:value | \"start\" | \"exit\"\n\r");

                        Console.Clear();
                        Console.Write(rtcHeader);
                        Console.Write("\n\r>> ");

                        input = Console.ReadLine().Trim();

                        colonInd = input.IndexOf(':');

                        inputSub = colonInd > 0 ? input.Substring(0, colonInd).Trim() : input;

                        switch (inputSub)
                        {
                            case "start":
                                rtcHeader.Clear();
                                rtcHeader.Append("/// Solutions Spreading ///\n\n\r");
                                rtcHeader.AppendFormat("Board length ........: {0}\n\r", boardLength);
                                rtcHeader.AppendFormat("Domain length .......: {0}\n\r", domainLength);
                                
                                QPSDamDetect.AutoExitFlag = true;
                                QPSDamDetect.SyncPoint += QPS_ioHandler;

                                SetGlobals();
                                BuildSolSpreading(boardLength, domainLength, SetFilter(0));

                                QPSDamDetect.SyncPoint -= QPS_ioHandler;
                                QPSDamDetect.Clear();

                                if (SpreadingList != null)
                                {
                                    startupFlag = false;

                                    // --- build Footer ---
                                    tmpHeader.Clear();
                                    tmpHeader.Append("\n\r STATISTIC:\n\n\r");
                                    tmpHeader.AppendFormat("Domains count .......: {0}\n\r", domCounter);
                                    tmpHeader.AppendFormat("Solutions total .....: {0}\n\r", solTotal.ToString("N0"));
                                    tmpHeader.AppendFormat("Max in domain .......: {0}\n\r", solMax);
                                    tmpHeader.AppendFormat("Avg in domain .......: {0}\n\r", solAvg);
                                    tmpHeader.AppendFormat("Min in domain .......: {0}\n\r", solMin);

                                    tmpHeader.Append("\n\r DATA SIZE:\n\n\r");
                                    tmpHeader.AppendFormat("Spreading List ......: {0:N} KB\n\r", sizeof(ulong) * SpreadingList.Count / 1024);
                                    tmpHeader.AppendFormat("Domains List ........: {0:N} KB\n\r",
                                        DomainsList.Count == 0 ? 0 :sizeof(ulong) * DomainsList.Count * DomainsList[0].Length / 1024);
                                }
                                else
                                {
                                    Console.WriteLine("\n\r FAILED \n\n\r >>> Press any key to continue <<<");
                                    Console.ReadKey();
                                }

                                break;

                            case "0":
                                inputSub = input.Substring(++colonInd);

                                if (int.TryParse(inputSub, out value) && value >= 4 && value > domainLength)
                                {
                                    BigInteger num = FactorialBI(value, value - domainLength + 1);

                                    if (num > int.MaxValue)
                                    {
                                        Console.WriteLine("\n\rThe number of domains {0}!/{1}! = {2:N0} exceeds Int32.MaxValue = {3:N0}\n\r",
                                                           value, value - domainLength + 1, num, int.MaxValue);

                                        Console.WriteLine(" >>> Press any key to continue <<<");
                                        Console.ReadKey();
                                    }
                                    else boardLength = value;
                                }

                                break;

                            case "1":
                                inputSub = input.Substring(++colonInd);

                                if (int.TryParse(inputSub, out value) && value > 0 && value < boardLength)
                                {
                                    BigInteger num = FactorialBI(boardLength, boardLength - value + 1);

                                    if (num > int.MaxValue)
                                    {
                                        Console.WriteLine("\n\rThe number of domains {0}!/{1}! = {2:N0} exceeds Int32.MaxValue = {3:N0}\n\r",
                                                           boardLength, boardLength - value + 1, num, int.MaxValue);

                                        Console.WriteLine(" >>> Press any key to continue <<<");
                                        Console.ReadKey();
                                    }
                                    else domainLength = value;
                                }

                                break;
                        }
                    }
                    else
                    {
                        Console.Clear();
                        Console.Write(rtcHeader);
                        Console.Write(tmpHeader);

                        Console.WriteLine("\n\r---------");
                        Console.WriteLine("Enter: \"save\" [\":txt\" | \":ids\"] | \"reset\"\n\r");
                        Console.Write(">> ");

                        input = Console.ReadLine();

                        if (input.Contains("save"))
                        {
                            string err = "";

                            colonInd = input.IndexOf(':');

                            if (colonInd > 0)
                            {
                                inputSub = input.Substring(++colonInd).Trim();

                                if (inputSub == "ids") err = WriteToFile(WriteMode.bin);
                                else if (inputSub == "txt") err = WriteToFile(WriteMode.txt);
                            }
                            else err = WriteToFile();

                            if (err == "") Console.WriteLine("\n\r Successfully saved");
                            else Console.WriteLine("\n\r Failed to save:\n\r{0}", err);

                            Console.WriteLine("\n\r >> Press any key to continue <<");
                            Console.ReadKey();
                        }
                        else if (input.Contains("reset"))
                        {
                            SpreadingList = null;
                            DomainsList = null;

                            GC.Collect();

                            startupFlag = true;
                        }
                    }

                } // end while (input != "exit")
            }
        }
        
        static void QPSolutionsSpreading()
        {
            SolutionsSpreading ssBuilder = new SolutionsSpreading();
            ssBuilder.Start();
        }
        #endregion

        static void Main(string[] args)
        {
            Console.OutputEncoding = Encoding.Unicode;

            string input = "";

            while (input != "exit")
            {
                Console.Clear();
                Console.WriteLine("0 - QP Dam Detect Solver");
                Console.WriteLine("1 - QP Variants Counter");
                Console.WriteLine("2 - QP Solutions Spreading");
                Console.Write("\n\r>> ");

                input = Console.ReadLine();

                switch (input)
                {
                    case "0":
                        QPDDSolver();
                        break;

                    case "1":
                        QPCounter();
                        break;

                    case "2":
                        QPSolutionsSpreading();
                        break;
                }
            }
        }
    }
}

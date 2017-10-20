/*
 n-Queens [Completion] problem solver
 Implemented as non recursive multithreaded tree traversal with dam-pruning

 zubetto85@gmail.com
 2017
 */

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
using System.Threading;

namespace QueensPuzzle
{
    public class QPSDamDetect
    {
        public class SyncEventArgs : EventArgs
        {
            public readonly int DDMinLevel;
            public readonly int DDMaxLevel;
            public readonly int DDAverageLevel;

            public bool SyncPause;
            public bool SyncAbort;

            public SyncEventArgs()
            {
                SyncPause = SyncPAUSE;
                SyncAbort = SyncABORT;

                // --- Dum Detection level statistic ---
                int sum, num;

                lock (DDLock)
                {
                    DDMinLevel = DDLmin;
                    DDMaxLevel = DDLmax;
                    sum = DDLsum;
                    num = DDLcounter;
                }

                DDAverageLevel = sum / num;
            }
        }
        
        public static event EventHandler<SyncEventArgs> SyncPoint;

        public class Solver
        {
            private readonly int ID;
            private readonly bool MasterFlag;

            private int Levels;

            private int[][] Positions;
            private IList<int>[] roPositions; // $$$ NOT FOR FLIGHT $$$
            private int PositionIndex;
            private int[] priorCell;
            
            private bool[] occupiedRows;
            private bool[] occupiedColumns;
            private bool[] occupiedPosDiagonals; // occupiedPosDiagonals[i] corresponds to bpos = i - Endex;
            private bool[] occupiedNegDiagonals; // occupiedNegDiagonals[i] corresponds to bneg = i;

            private int[] FreeColumns; // contains indexes of not occupied columns
            private int[] FreeRows; // contains indexes of not occupied rows

            private int[][] ColumnSaturation; // Saturation of each column/row at each level or levels saturation history
            private int[][] RowSaturation;

            // --- IniDat(), Run(), DamDetect() ---
            private int FreeRowsEndex;
            private int[] SelectedRowIndexes; // is used to restore the FreeRows upon a return to a higher level

            private int[] tmpClmSaturation;
            private int[] tmpRowSaturation;
            
            private int targetY; // column to fill
            private int trialX; // trial row for the given column
            private int selectedRowInd;
            private int bPos, bNeg; // positive and negative diagonals respectively

            // --- Public members --- 
            public readonly IList<IList<int>> Interims; // $$$ NOT FOR FLIGHT $$$

            public readonly IList<bool> OccupiedColumns;
            public readonly IList<bool> OccupiedRows;
            public readonly IList<bool> OccupiedPositiveDiagonals;
            public readonly IList<bool> OccupiedNegativeDiagonals;

            /// <summary>
            /// Parameters marked as JC are just copied,
            /// parameters marked as MR are copied and require certain modification before start of this solver
            /// </summary>
            /// <param name="id">JC, Solver ID</param>
            /// <param name="rellevel">MR, Relative level at which this Solver has been created</param>
            /// <param name="abslevel">JC, Absolute level at which this Solver has been created</param>
            /// <param name="positions">MR, this array should not contains the addCell yet, i.e. positions[abslevel] = null</param>
            /// <param name="addCell">JC, the cell to be occupied before the start of this solver</param>
            /// <param name="oPosDiags">MR, it will be modified according to the addCell</param>
            /// <param name="oNegDiags">MR, it will be modified according to the addCell</param>
            /// <param name="oRows">MR, it will be modified according to the addCell</param>
            /// <param name="oClms">MR, it will be modified according to the addCell</param>
            /// <param name="rowInd">JC, is used to prepare the FreeRows</param>
            /// <param name="freeRows">MR, partial copy</param>
            /// <param name="freeClms">JC,  partial copy</param>
            /// <param name="rowsSaturation">JC, it will be used to initialize the RowSaturation</param>
            /// <param name="clmsSaturation">JC, it will be used to initialize the ColumnSaturation</param>
            /// <param name="master">MasterFlag</param>
            public Solver(int id, int rellevel, int abslevel,
                          int[][] positions, int[] addCell, 
                          bool[] oPosDiags, bool[] oNegDiags, bool[] oRows, bool[] oClms,
                          int rowInd, int[] freeRows, int[] freeClms,
                          int[] rowsSaturation, int[] clmsSaturation, bool master = false)
            {
                ID = id;
                MasterFlag = master;

                rellevel++;
                Levels = freeRows.Length - rellevel;
                
                PositionIndex = abslevel;
                priorCell = addCell;

                // --- Copy the positions array ---
                Positions = new int[RTCLength][];

                roPositions = new IList<int>[RTCLength]; // $$$ NOT FOR FLIGHT $$$
                Interims = Array.AsReadOnly(roPositions); // $$$ NOT FOR FLIGHT $$$

                for (int i = 0; i < abslevel; i++)
                {
                    Positions[i] = new int[4];

                    Buffer.BlockCopy(positions[i], 0, Positions[i], 0, Int32PosArraySize);

                    roPositions[i] = Array.AsReadOnly(Positions[i]); // $$$ NOT FOR FLIGHT $$$
                }
                
                // --- Copy the occupancy arrays ---
                occupiedRows = new bool[RTCLength];
                occupiedColumns = new bool[RTCLength];
                
                Buffer.BlockCopy(oRows, 0, occupiedRows, 0, BoolArraySize);
                Buffer.BlockCopy(oClms, 0, occupiedColumns, 0, BoolArraySize);
                
                occupiedPosDiagonals = new bool[oPosDiags.Length];
                occupiedNegDiagonals = new bool[oPosDiags.Length];
                
                Buffer.BlockCopy(oPosDiags, 0, occupiedPosDiagonals, 0, BoolDiagArrsSize);
                Buffer.BlockCopy(oNegDiags, 0, occupiedNegDiagonals, 0, BoolDiagArrsSize);

                // --- ini public readonly ---
                OccupiedRows = Array.AsReadOnly(occupiedRows);
                OccupiedColumns = Array.AsReadOnly(occupiedColumns);
                OccupiedPositiveDiagonals = Array.AsReadOnly(occupiedPosDiagonals);
                OccupiedNegativeDiagonals = Array.AsReadOnly(occupiedNegDiagonals);

                // --- Copy the freeRows and freeClms arrays ---
                FreeRows = new int[Levels];
                FreeColumns = new int[Levels];

                int arrSize = Int32Size * Levels;
                Buffer.BlockCopy(freeRows, 0, FreeRows, 0, arrSize);
                Buffer.BlockCopy(freeClms, rellevel * Int32Size, FreeColumns, 0, arrSize);

                if (rowInd < Levels) FreeRows[rowInd] = freeRows[Levels];

                // --- Copy the Saturation arrays ---
                tmpRowSaturation = new int[RTCLength];
                tmpClmSaturation = new int[RTCLength];

                Buffer.BlockCopy(rowsSaturation, 0, tmpRowSaturation, 0, Int32ArraySize);
                Buffer.BlockCopy(clmsSaturation, 0, tmpClmSaturation, 0, Int32ArraySize);
            }

            private void IniData()
            {
                FreeRowsEndex = Levels - 1;
                SelectedRowIndexes = new int[FreeRowsEndex];

                // --- Create the levels saturation history arrays ---
                RowSaturation = new int[FreeRowsEndex][];
                ColumnSaturation = new int[FreeRowsEndex][];

                RowSaturation[0] = new int[RTCLength];
                ColumnSaturation[0] = new int[RTCLength];

                Buffer.BlockCopy(tmpRowSaturation, 0, RowSaturation[0], 0, Int32ArraySize);
                Buffer.BlockCopy(tmpClmSaturation, 0, ColumnSaturation[0], 0, Int32ArraySize);

                if (MasterFlag) return; // >>>>>>>>> >>>>>>>>>

                SolversData[ID] = this;

                // --- Occupy the priorCell ---
                Positions[PositionIndex] = priorCell;
                roPositions[PositionIndex] = Array.AsReadOnly(priorCell); // $$$ NOT FOR FLIGHT $$$

                occupiedRows[priorCell[0]] = true;
                occupiedColumns[priorCell[1]] = true;
                occupiedPosDiagonals[priorCell[2] + RTCEndex] = true;
                occupiedNegDiagonals[priorCell[3]] = true;

                PositionIndex++; // setting of the Absolute index of the current level
            }

            public void Run(object state)
            {
                IniData();

                Solver parallelSolver;
                int solverID;

                int[] penultPos, finalPos;
                int finalY;
                
                int level = 0; // setting of the Relative index of the current level
                int penultLevel = Levels - 2; // setting of the Relative index of the penult level

                int startRow = 0;
                bool climbFlag = true; // go to upper level after the traverse of all free rows

                if (MasterFlag)
                {
                    mainInProgress = true;
                    theStopwatch.Restart();
                }


                for (int yi = 0; yi < Levels; yi++) // --- columns-loop --------------------------------------------------------
                {
                    targetY = FreeColumns[yi];

                    for (int xj = startRow; xj <= FreeRowsEndex; xj++) // --- rows-loop -------------------------------
                    {
                        // --- ++SYNCPOINT++ ---
                        if (MasterFlag)
                        {
                            if (SyncPoint != null && theStopwatch.ElapsedMilliseconds >= SyncPeriod)
                            {
                                SyncEventArgs sea = new SyncEventArgs();

                                SyncPoint?.Invoke(this, sea);

                                SyncPAUSE = sea.SyncPause;
                                SyncABORT = sea.SyncAbort;

                                if (SyncABORT)
                                {
                                    SyncPAUSE = false;
                                    SyncPauseMRE.Set();

                                    SyncAbortMRE.WaitOne(); // Master should waits for the stopping of all worker threads
                                    return;
                                }
                                else if (SyncPAUSE) SyncPauseMRE.Reset();
                                else SyncPauseMRE.Set();

                                theStopwatch.Restart();
                            }
                        }
                        else
                        {
                            if (SyncPAUSE)
                            {
                                // Some work
                                SyncPauseMRE.WaitOne();
                            }

                            if (SyncABORT)
                            {
                                EmptyASeat(PositionIndex, ID);
                                return;
                            }
                        } // --- end ++SYNCPOINT++ ---

                        trialX = FreeRows[xj];

                        bPos = targetY - trialX;
                        bNeg = targetY + trialX;

                        if (!occupiedPosDiagonals[bPos + RTCEndex] && !occupiedNegDiagonals[bNeg])
                        {
                            // Prepare data for DamDetect
                            Buffer.BlockCopy(RowSaturation[level], 0, tmpRowSaturation, 0, Int32ArraySize);
                            Buffer.BlockCopy(ColumnSaturation[level], 0, tmpClmSaturation, 0, Int32ArraySize);

                            selectedRowInd = xj;

                            if (DamDetect())
                            {
                                if (DDLFlag) LogDamDetectionLevel(PositionIndex);

                                // CONTINUE the search with the next free row at the current level
                            }
                            else // There were no dams detected
                            {
                                if (level == penultLevel) // The NEW SOLUTION has been found!
                                {
                                    if (JustCountFlag)
                                    {
                                        Interlocked.Increment(ref GalaxyC);

                                        if (GalaxyC == long.MaxValue)
                                        {
                                            GalaxyC = 0;
                                            UniverseC += long.MaxValue;
                                        }
                                    }
                                    else
                                    {
                                        // save penult position
                                        penultPos = new int[4] { trialX, targetY, bPos, bNeg };

                                        // obtain and save final position
                                        trialX = xj == 0 ? FreeRows[1] : FreeRows[0]; // FreeRowsEndex = 1 at the penultLevel
                                        finalY = FreeColumns[Levels - 1];

                                        finalPos = new int[4] { trialX, finalY, finalY - trialX, finalY + trialX };

                                        SaveSolution(Positions, penultPos, finalPos);
                                    }
                                    
                                    // CONTINUE the search with the next free row at the current level
                                }
                                else if (PositionIndex < ZeroNumLevel &&
                                         ReserveASeat(PositionIndex, out solverID)) // then create and start the new SOLVER
                                {
                                    parallelSolver = new Solver(solverID, level, PositionIndex,
                                                                Positions, new int[4] { trialX, targetY, bPos, bNeg },
                                                                occupiedPosDiagonals, occupiedNegDiagonals, occupiedRows, occupiedColumns,
                                                                xj, FreeRows, FreeColumns,
                                                                tmpRowSaturation, tmpClmSaturation);
                                    
                                    ThreadPool.QueueUserWorkItem(parallelSolver.Run);
                                    
                                    // CONTINUE the search with the next free row at the current level
                                }
                                else // Save current state and dive to the next level
                                {
                                    Positions[PositionIndex] = new int[4] { trialX, targetY, bPos, bNeg };
                                    roPositions[PositionIndex] = Array.AsReadOnly(Positions[PositionIndex]); // $$$ NOT FOR FLIGHT $$$

                                    // Occupy the cell (trialX, targetY)
                                    occupiedRows[trialX] = true;
                                    occupiedColumns[targetY] = true;
                                    occupiedPosDiagonals[bPos + RTCEndex] = true;
                                    occupiedNegDiagonals[bNeg] = true;

                                    // Prepare the FreeRows for the next level
                                    SelectedRowIndexes[level] = xj;

                                    FreeRows[xj] = FreeRows[FreeRowsEndex];
                                    FreeRows[FreeRowsEndex] = trialX;

                                    // DIVE-ITERATION
                                    PositionIndex++;
                                    level++;
                                    FreeRowsEndex--;

                                    // Record saturation info
                                    RowSaturation[level] = new int[RTCLength];
                                    ColumnSaturation[level] = new int[RTCLength];

                                    Buffer.BlockCopy(tmpRowSaturation, 0, RowSaturation[level], 0, Int32ArraySize);
                                    Buffer.BlockCopy(tmpClmSaturation, 0, ColumnSaturation[level], 0, Int32ArraySize);

                                    startRow = 0;
                                    climbFlag = false; // DIVE to the next level
                                    break;
                                }
                            } // end if (DamDetect()) -------
                        }
                    } // --- end rows-loop -------------------------------

                    if (climbFlag)
                    {
                        if (level > 0) // then CLIMB to the upper level
                        {
                            // Erasure of the Saturation on the current level is not needed  

                            // CLIMB-ITERATION
                            PositionIndex--;
                            level--;
                            FreeRowsEndex++;

                            // Restore the FreeRows array
                            startRow = SelectedRowIndexes[level];

                            trialX = FreeRows[startRow];
                            FreeRows[startRow] = FreeRows[FreeRowsEndex];
                            FreeRows[FreeRowsEndex] = trialX;

                            // Shift the rows and columns indexes
                            startRow++;
                            yi -= 2;

                            // Empty the cell (Positions[PositionIndex][0], Positions[PositionIndex][1])
                            penultPos = Positions[PositionIndex];

                            occupiedRows[trialX] = false;
                            occupiedColumns[penultPos[1]] = false;
                            occupiedPosDiagonals[penultPos[2] + RTCEndex] = false;
                            occupiedNegDiagonals[penultPos[3]] = false;

                            penultPos = null;
                            Positions[PositionIndex] = null;
                            roPositions[PositionIndex] = null; // $$$ NOT FOR FLIGHT $$$
                        }
                        else // Entire subtree has been traversed
                        {
                            if (MasterFlag) break;
                            else
                            {
                                EmptyASeat(PositionIndex, ID);
                                return; // >>>>>>>>> Non-Master RETURN >>>>>>>>>
                            }
                        }
                    }
                    else climbFlag = true; 
                } // --- columns-loop ----------------------------------------------------------------------- 

                // Reinsurance, that only the Master can reach this point 
                if (!MasterFlag) return;

                mainInProgress = false;
                bool exitFlag;
                
                while (!SyncABORT) // Master Solver continues monitoring
                {
                    if (SyncPoint != null)
                    {
                        if (AutoExitFlag && !assistInProgress) SyncABORT = true;
                        else if (theStopwatch.ElapsedMilliseconds >= SyncPeriod)
                        {
                            theStopwatch.Restart();

                            SyncEventArgs sea = new SyncEventArgs();

                            SyncPoint?.Invoke(this, sea);
                            
                            if (sea.SyncPause) SyncPauseMRE.Reset();
                            else SyncPauseMRE.Set();

                            SyncPAUSE = sea.SyncPause;
                            SyncABORT = sea.SyncAbort;
                        }
                    }
                    else if (!assistInProgress)
                    {
                        SyncABORT = true;
                    }
                }

                theStopwatch.Stop();

                SyncPAUSE = false;
                SyncPauseMRE.Set();
                SyncAbortMRE.WaitOne(); // Master should waits for the stopping of all worker threads
            }

            /// <summary>
            /// Uses the FreeRows array. Params.:
            /// int Length, Endex |
            /// int targetY, selectedRowInd, FreeRowsEndex |
            /// int bPos, bNeg |
            /// int[] FreeRows, tmpRowSaturation, tmpClmSaturation |
            /// bool[] occupiedRows, occupiedColumns, occupiedPosDiagonals, occupiedNegDiagonals
            /// </summary>
            /// <returns></returns>
            private bool DamDetect()
            {
                int row, yi;

                // Before-loop
                for (int i = 0; i < selectedRowInd; i++)
                {
                    row = FreeRows[i];

                    // Decrement vacancies of the row due to the new column-occupancy
                    // only if cell (row, targetY) is not lies on occupied diagonals 
                    if (!occupiedPosDiagonals[targetY - row + RTCEndex] &&
                        !occupiedNegDiagonals[targetY + row] &&
                        ++tmpRowSaturation[row] == RTCLength) return true; // >>>>>>>>> DAM IS DETECTED! >>>>>>>>>

                    // Decrement vacancies of the row and the yi column due to the new positive diagonal-occupancy
                    // only if cell (row, yi) is not lies on any occupied negative diagonal or column
                    yi = row + bPos;

                    if (yi >= 0 && yi < RTCLength &&
                        !occupiedColumns[yi] &&
                        !occupiedNegDiagonals[yi + row] &&
                        (++tmpRowSaturation[row] == RTCLength | ++tmpClmSaturation[yi] == RTCLength)) return true; // >>>>>>>>> DAM IS DETECTED! >>>>>>>>>

                    // Decrement vacancies of the row and the yi column due to the new negative diagonal-occupancy
                    // only if cell (row, yi) is not lies on any occupied positive diagonal or column
                    yi = -row + bNeg;

                    if (yi >= 0 && yi < RTCLength &&
                        !occupiedColumns[yi] &&
                        !occupiedPosDiagonals[yi - row + RTCEndex] &&
                        (++tmpRowSaturation[row] == RTCLength | ++tmpClmSaturation[yi] == RTCLength)) return true; // >>>>>>>>> DAM IS DETECTED! >>>>>>>>>
                }

                // After-loop
                for (int i = selectedRowInd + 1; i <= FreeRowsEndex; i++)
                {
                    row = FreeRows[i];

                    // Decrement vacancies of the row due to the new column-occupancy
                    // only if cell (row, targetY) is not lies on occupied diagonals 
                    if (!occupiedPosDiagonals[targetY - row + RTCEndex] &&
                        !occupiedNegDiagonals[targetY + row] &&
                        ++tmpRowSaturation[row] == RTCLength) return true; // >>>>>>>>> DAM IS DETECTED! >>>>>>>>>

                    // Decrement vacancies of the row and the yi column due to the new positive diagonal-occupancy
                    // only if cell (row, yi) is not lies on any occupied negative diagonal or column
                    yi = row + bPos;

                    if (yi >= 0 && yi < RTCLength &&
                        !occupiedColumns[yi] &&
                        !occupiedNegDiagonals[yi + row] &&
                        (++tmpRowSaturation[row] == RTCLength | ++tmpClmSaturation[yi] == RTCLength)) return true; // >>>>>>>>> DAM IS DETECTED! >>>>>>>>>

                    // Decrement vacancies of the row and the yi column due to the new negative diagonal-occupancy
                    // only if cell (row, yi) is not lies on any occupied positive diagonal or column
                    yi = -row + bNeg;

                    if (yi >= 0 && yi < RTCLength &&
                        !occupiedColumns[yi] &&
                        !occupiedPosDiagonals[yi - row + RTCEndex] &&
                        (++tmpRowSaturation[row] == RTCLength | ++tmpClmSaturation[yi] == RTCLength)) return true; // >>>>>>>>> DAM IS DETECTED! >>>>>>>>>
                }

                return false;
            }
            
        } // end class Solver //////////////////////////////////////////////////////////////////////////////////////////////////////////////

        // x and row are used for rows
        // y and clm are used for columns
        // bPos is used for positive diagonals defined as y = x + bPos and passing through the point (xi, yi), where bPos = yi - xi
        // bNeg is used for negative diagonals defined as y = -x + bNeg and passing through the point (xi, yi), where bNeg = yi + xi

        // --- *** SHARED GROUP *** ----------------------------------------------------------------------------
        private static bool inProgressFlag = false;
        private static bool mainInProgress = false;
        private static bool assistInProgress = false;
        public static bool IsInProgress { get { return inProgressFlag; } }
        public static bool IsMainInProgress { get { return mainInProgress; } }

        private static int totalThreadsNum;
        private static int ThreadsAvailable;
        private static int ZeroNumLevel;
        private static int[] levelThreadsNum;
        
        private static Solver[] SolversData;
        private static IList<Solver> solversList;
        public static IList<Solver> SolversList { get { return solversList; } }

        private static bool[] ActiveThreads;
        private static int threadSeat;

        // --- Sync group ---
        private static bool SyncPAUSE = false;
        private static bool SyncABORT = false;
        private static ManualResetEvent SyncPauseMRE = new ManualResetEvent(false);
        private static ManualResetEvent SyncAbortMRE = new ManualResetEvent(true); // forces to wait of stopping of background threads
        private static readonly Stopwatch theStopwatch = new Stopwatch();
        private static int SyncPeriod;

        /// <summary>
        /// If there are any subscribers of the SyncPoint event then:
        /// true: Master solver will track the SyncABORT flag to exit
        /// false: Master solver will immediately exit after passing all tree
        /// If there are no subscribers of the SyncPoint event then Master solver will immediately exit after passing all tree
        /// </summary>
        public static bool AutoExitFlag = false;
        
        // --- Solutions lists ---
        private static List<int[][]> SolutionsList;
        private static IList<int[][]> roSolutions;
        public static IList<int[][]> Solutions { get { return roSolutions; } }

        // --- Solutions counter ---
        private static bool JustCountFlag = false;
        private static int AutoPauseCount = 100 * 1000 * 1000; // is used only if JustCountFlag is false; zero or negative values disable auto-pause
        private static BigInteger UniverseC = BigInteger.Zero;
        private static long GalaxyC = 0;

        public static bool IsJustCount { get { return JustCountFlag; } }

        public static BigInteger SolutionsCount
        {
            get
            {
                BigInteger bI = BigInteger.Zero;

                if (JustCountFlag)
                {
                    bI += UniverseC;
                    bI += GalaxyC;
                }
                else if (SolutionsList != null) bI = SolutionsList.Count;

                return bI;
            }
        }

        // --- Constants --------------------------
        private const int Int32Size = sizeof(int);
        private const int Int32PosArraySize = 4 * sizeof(int);

        // --- Runtime constants ---
        private static int RTCLength;
        private static int RTCEndex;
        private static int Int32ArraySize;
        private static int DiagArrsLength;
        private static int BoolArraySize;
        private static int BoolDiagArrsSize;
        // --- end of constants -------------------
        
        /// <summary>
        /// 
        /// </summary>
        /// <param name="absLevel">Absolute level</param>
        /// <param name="ID">zero based index for a new thread</param>
        /// <returns>true if a new thread is permitted</returns>
        private static bool ReserveASeat(int absLevel, out int ID)
        {
            ID = -1;
            bool found = false;

            lock (ActiveThreads)
            {
                // Addition of the Master Solver does not decrement the ThreadsAvailable
                if (ThreadsAvailable > 1 && levelThreadsNum[absLevel] > 0)
                {
                    int startInd = threadSeat;

                    for (int i = threadSeat; i < ActiveThreads.Length; i++)
                    {
                        if (!ActiveThreads[i])
                        {
                            found = true;
                            threadSeat = i;
                            break;
                        }
                    }

                    if (!found) for (int i = 1; i < startInd; i++)
                    {
                        if (!ActiveThreads[i])
                        {
                            found = true;
                            threadSeat = i;
                            break;
                        }
                    }

                    if (found)
                    {
                        ActiveThreads[threadSeat] = true;
                        ID = threadSeat;

                        threadSeat++;
                        ThreadsAvailable--;
                        levelThreadsNum[absLevel]--;

                        if (!assistInProgress)
                        {
                            SyncAbortMRE.Reset();
                            assistInProgress = true;
                        } 
                    }
                }
            }

            return found;
        }

        private static void EmptyASeat(int absLevel, int ID)
        {
            SolversData[ID] = null;

            lock (ActiveThreads)
            {
                ActiveThreads[ID] = false;
                levelThreadsNum[absLevel]++;
                ThreadsAvailable++;

                // Allow the master thread return from the Solver.Run()
                if (ThreadsAvailable == totalThreadsNum)
                {
                    assistInProgress = false;
                    SyncAbortMRE.Set();
                } 
            }
        }
        
        private static void SaveSolution(int[][] positions, int[] penult, int[] final)
        {
            lock(SolutionsList)
            {
                // Auto-pause
                if (AutoPauseCount > 0 && SolutionsList.Count >= AutoPauseCount)
                {
                    SyncPAUSE = true;
                    SyncPauseMRE.Reset();
                }

                // copy positions
                int[][] newSolution = new int[RTCLength][];
                int num = RTCLength - 2;

                for (int i = 0; i < num; i++)
                {
                    newSolution[i] = new int[4];
                    Buffer.BlockCopy(positions[i], 0, newSolution[i], 0, Int32PosArraySize);
                }

                newSolution[num] = new int[4];
                Buffer.BlockCopy(penult, 0, newSolution[num], 0, Int32PosArraySize);

                newSolution[++num] = new int[4];
                Buffer.BlockCopy(final, 0, newSolution[num], 0, Int32PosArraySize);

                SolutionsList.Add(newSolution);
            }
        }

        private static bool DDLFlag = true;
        private static bool DDLReset = true;
        private static int DDLmin = int.MaxValue, DDLmax = int.MinValue, DDLsum = 0, DDLcounter = 1;
        private static object DDLock = new object();
        private static void LogDamDetectionLevel(int absLevel)
        {
            lock(DDLock)
            {
                if (absLevel < DDLmin) DDLmin = absLevel;
                if (absLevel > DDLmax) DDLmax = absLevel;
                
                try
                {
                    checked
                    {
                        if (DDLReset) DDLReset = false;
                        else DDLcounter++;

                        DDLsum += absLevel;
                    }
                }
                catch (OverflowException ex)
                {
                    DDLsum = 0;
                    DDLcounter = 1;

                    DDLReset = true;
                }
            }
        }

        private static void ResetDDL()
        {
            DDLmin = int.MaxValue;
            DDLmax = int.MinValue;
            DDLsum = 0;
            DDLcounter = 1;
            DDLReset = true;
        }
        // --- end of SHARED GROUP -----------------------------------------------------------------------------------------------------

        private int[][] Positions; 
        private static int Levels; // Initially is equal to the Length and is decremented with each new queen adding

        public IList<int[]> GetInitialPositions()
        {
            int num = RTCLength - Levels;

            if (num == 0) return null;

            List<int[]> roPositions = new List<int[]>(num);
            int[] tmp;

            for (int i = 0; i < num; i++)
            {
                tmp = new int[4];
                Buffer.BlockCopy(Positions[i], 0, tmp, 0, Int32PosArraySize);
                roPositions.Add(tmp);
            }

            return roPositions.AsReadOnly();
        }

        // --- InitializeData(), AddQueen(), ... ---
        private bool[] occupiedPosDiagonals; // occupiedPosDiagonals[i] corresponds to bpos = i - Endex;
        private bool[] occupiedNegDiagonals; // occupiedNegDiagonals[i] corresponds to bneg = i;
        private bool[] occupiedColumns;
        private bool[] occupiedRows;
        
        private int[] FreeColumns; // contains indexes of not occupied columns
        private int[] FreeRows; // contains indexes of not occupied rows
        
        private int[] ColumnSaturationL0; // vacancies of each column/row at the 0 level
        private int[] RowSaturationL0;
        
        // --- PreDamDetect() ---
        private int[] tmpClmSaturation;
        private int[] tmpRowSaturation;
        
        private int targetY; // column to fill
        private int trialX; // trial row for the given column
        private int bPos, bNeg; // positive and negative diagonals respectively
        
        /// <summary>
        /// 
        /// </summary>
        /// <param name="length"></param>
        /// <param name="threadsNum"></param>
        /// <param name="threadsByLevel"></param>
        /// <param name="levelRatio"></param>
        /// <param name="syncPeriod">in ms</param>
        public QPSDamDetect(int length = 8, ThreadsSpread threadsDat = null, int[] threadsByLevel = null, int syncPeriod = 100)
        {
            if (inProgressFlag) return; // >>>>>>>>> To prevent unintentional interruption >>>>>>>>>

            if (length < 2) length = 2;
            
            Levels = length;

            SolutionsList = new List<int[][]>(length);
            roSolutions = SolutionsList.AsReadOnly();

            // --- ini runtime constants ---
            RTCLength = length;
            RTCEndex = length - 1;

            Int32ArraySize = RTCLength * Int32Size;
            BoolArraySize = RTCLength * sizeof(bool);

            DiagArrsLength = RTCLength + RTCEndex;
            BoolDiagArrsSize = DiagArrsLength * sizeof(bool);

            // --- ini position arrays ---
            Positions = new int[RTCLength][];

            occupiedPosDiagonals = new bool[DiagArrsLength]; // range from -RTCEndex to RTCEndex
            occupiedNegDiagonals = new bool[DiagArrsLength]; // range from 0 to 2*RTCEndex
            occupiedColumns = new bool[RTCLength];
            occupiedRows = new bool[RTCLength];

            ColumnSaturationL0 = new int[RTCLength];
            RowSaturationL0 = new int[RTCLength];

            // --- Set threads ---
            SetThreads(threadsDat, threadsByLevel, syncPeriod);
        }

        public class ThreadsSpread
        {
            public enum Law { Linear, Exponential }

            private Law spread;

            private int totNumber;
            private int iniNumber;
            private int zeroLevel;
            private double ratio;

            private bool useZeroLevel;
            private bool forceShift;

            public Law SpreadLaw { get { return spread; } }

            public int TotalNumber { get { return totNumber; } }
            public int IniNumber { get { return iniNumber; } }
            public int ZeroNumberLevel { get { return zeroLevel; } }
            public double LevelRatio { get { return ratio; } }

            public bool IsZeroLevelUsed { get { return useZeroLevel; } }
            public bool ForceShift { get { return forceShift; } }

            /// <summary>
            /// Sets the number of available threads N at each level depending on the level index L: N = f(L)
            /// </summary>
            /// <param name="spreadLaw">Linear: N = -levelRatio * L + iniNum | Exponential: N = iniNum / levelRatio^L</param>
            /// <param name="totNum">Total number of threads; does not influence on the spreading</param>
            /// <param name="iniNum">f(0) = iniNum</param>
            /// <param name="zeroNumLevel">N = 0 for L >= zeroNumLevel; if more than zero then it defines the levelRatio</param>
            /// <param name="levelRatio">Defines the function f(L); if zeroNumLevel > 0 then levelRatio is set accordingly to the zeroNumLevel</param>
            /// <param name="forceShift">If true then indicates that shift will be performed regardless of the IsZeroLevelUsed</param>
            public ThreadsSpread(Law spreadLaw = Law.Exponential, int totNum = 0, int iniNum = 1, int zeroNumLevel = 0, double levelRatio = 2, bool forceShift = false)
            {
                // SpreadLaw
                spread = spreadLaw;
                
                // TotalNumber
                if (totNum < 1) totNumber = 1;
                else totNumber = totNum;

                // IniNumber
                if (iniNum < 1) iniNumber = 1;
                else iniNumber = iniNum;

                // ZeroNumberLevel
                zeroLevel = zeroNumLevel;

                if (zeroNumLevel > 0) useZeroLevel = true;
                else useZeroLevel = false;

                // LevelRatio
                if (!useZeroLevel && spreadLaw == Law.Exponential && levelRatio <= 0) ratio = 1;
                else ratio = levelRatio;

                // Try to define the zero-level
                if (!useZeroLevel) zeroLevel = FindZeroLevel(spreadLaw, iniNumber, ratio);

                // Force shift
                this.forceShift = forceShift;
            }

            public int[] GetArray(int length)
            {
                int[] spreadArr = new int[length];
                
                if (useZeroLevel)
                {
                    int zLevel = zeroLevel < length ? zeroLevel : length;

                    if (iniNumber == 1) for (int i = 0; i < zLevel; i++) spreadArr[i] = 1;
                    else if (zeroLevel == 1) spreadArr[0] = iniNumber;
                    else // iniNumber > 1 & zeroLevel > 1
                    {
                        switch (spread)
                        {
                            case Law.Linear:
                                ratio = (iniNumber - 1.0) / (zeroLevel - 1);

                                for (int i = 0; i < zLevel; i++) spreadArr[i] = (int)(iniNumber - ratio * i);

                                break;

                            case Law.Exponential:
                                ratio = Math.Pow(iniNumber, 1.0 / (zeroLevel - 1));

                                for (int i = 0; i < zLevel; i++) spreadArr[i] = (int)(iniNumber / Math.Pow(ratio, i));

                                break;
                        }
                    }
                }
                else // use specified ratio 
                {
                    int n;

                    switch (spread)
                    {
                        case Law.Linear:
                            for (int i = 0; i < length; i++)
                            {
                                n = (int)(iniNumber - ratio * i);

                                if (n > 0) spreadArr[i] = n;
                                else break;
                            }
                            break;

                        case Law.Exponential:
                            for (int i = 0; i < length; i++)
                            {
                                n = (int)(iniNumber / Math.Pow(ratio, i));

                                if (n > 0) spreadArr[i] = n;
                                else break;
                            }
                            break;
                    }
                } // end if (useZeroLevel)

                return spreadArr;
            }

            public static int FindZeroLevel(Law spreadLaw, int iniNum, double ratio)
            {
                int zeroLevel = -1;

                if (iniNum > 0)
                {
                    switch (spreadLaw)
                    {
                        case Law.Linear:
                            if (ratio > 0) zeroLevel = (int)Math.Ceiling((iniNum - 1.0) / ratio);
                            break;

                        case Law.Exponential:
                            if (ratio > 1) zeroLevel = (int)Math.Ceiling(Math.Log(iniNum) / Math.Log(ratio));
                            break;
                    }
                }
                
                return zeroLevel;
            }

            public static int FindOne(Law spreadLaw, int totNum = 0, int iniNum = 0, int zeroLevel = 0)
            {
                int theSought = 0;
                bool FoundFlag = false;
                double ratio;

                if (totNum < 1 && iniNum > 0 && zeroLevel > 0)
                {
                    if (iniNum == 1) for (int i = 0; i < zeroLevel; i++) theSought++;
                    else if (zeroLevel == 1) theSought = iniNum;
                    else // (iniNum > 1 & zeroLevel > 1) is true
                    {
                        switch (spreadLaw)
                        {
                            case Law.Linear:
                                ratio = (iniNum - 1.0) / (zeroLevel - 1);

                                for (int i = 0; i < zeroLevel; i++) theSought += (int)(iniNum - ratio * i);
                                break;

                            case Law.Exponential:
                                ratio = Math.Pow(iniNum, 1.0 / (zeroLevel - 1));

                                for (int i = 0; i < zeroLevel; i++) theSought += (int)(iniNum / Math.Pow(ratio, i));
                                break;
                        }
                    }
                }
                else if (iniNum < 1 && totNum > 0 && zeroLevel > 0)
                {
                    if (zeroLevel == 1) theSought = totNum;
                    else if (totNum >= zeroLevel)
                    {
                        iniNum = totNum - (zeroLevel - 1) + 1; // +1 due to initial decrement in the while-loop

                        switch (spreadLaw)
                        {
                            case Law.Linear:
                                while (!FoundFlag && --iniNum > 0)
                                {
                                    theSought = iniNum;
                                    ratio = (iniNum - 1.0) / (zeroLevel - 1);

                                    FoundFlag = true;

                                    for (int i = 1; i < zeroLevel; i++)
                                    {
                                        theSought += (int)(iniNum - ratio * i);

                                        if (theSought > totNum) { FoundFlag = false; break; }
                                    }
                                }

                                theSought = iniNum;
                                break;

                            case Law.Exponential:
                                while (!FoundFlag && --iniNum > 0)
                                {
                                    theSought = iniNum;
                                    ratio = Math.Pow(iniNum, 1.0 / (zeroLevel - 1));

                                    FoundFlag = true;

                                    for (int i = 1; i < zeroLevel; i++)
                                    {
                                        theSought += (int)(iniNum / Math.Pow(ratio, i));

                                        if (theSought > totNum) { FoundFlag = false; break; }
                                    }
                                }

                                theSought = iniNum;
                                break;
                        } // end switch (spreadLaw)
                    }
                    // else (zeroLevel > totNum ) is true and thus there is no positive iniNum to satisfy this condition
                }
                else if (zeroLevel < 1 && totNum > 0 && iniNum > 0)
                {
                    if (totNum == iniNum) theSought = 1;
                    else if (totNum > iniNum)
                    {
                        zeroLevel = totNum - iniNum + 1 + 1; // +1 due to initial decrement in the while-loop

                        switch (spreadLaw)
                        {
                            case Law.Linear:
                                while (!FoundFlag && --zeroLevel > 1)
                                {
                                    theSought = iniNum;
                                    ratio = (iniNum - 1.0) / (zeroLevel - 1);

                                    FoundFlag = true;

                                    for (int i = 1; i < zeroLevel; i++)
                                    {
                                        theSought += (int)(iniNum - ratio * i);

                                        if (theSought > totNum) { FoundFlag = false; break; }
                                    }
                                }

                                theSought = zeroLevel;
                                break;

                            case Law.Exponential:
                                while (!FoundFlag && --zeroLevel > 1)
                                {
                                    theSought = iniNum;
                                    ratio = Math.Pow(iniNum, 1.0 / (zeroLevel - 1));

                                    FoundFlag = true;

                                    for (int i = 1; i < zeroLevel; i++)
                                    {
                                        theSought += (int)(iniNum / Math.Pow(ratio, i));

                                        if (theSought > totNum) { FoundFlag = false; break; }
                                    }
                                }

                                theSought = zeroLevel;
                                break;
                        } // switch (spreadLaw)
                    }
                }

                return theSought;
            }
        }

        private ThreadsSpread TSData = null;

        private void SetThreads(ThreadsSpread Dat, int[] levelNum, int syncPeriod)
        {
            if (Dat != null)
            {
                TSData = Dat;
                totalThreadsNum = Dat.TotalNumber;
                levelThreadsNum = Dat.GetArray(RTCLength);
                ZeroNumLevel = Dat.ZeroNumberLevel > 0 ? Dat.ZeroNumberLevel : int.MaxValue;
            }
            else if (levelNum != null)
            {
                levelThreadsNum = new int[RTCLength];

                int length = levelNum.Length < RTCLength ? levelNum.Length : RTCLength;

                Buffer.BlockCopy(levelNum, 0, levelThreadsNum, 0, length * Int32Size);

                totalThreadsNum = levelThreadsNum.Sum();

                ZeroNumLevel = int.MaxValue; // TODO: loop to define first zero
            }
            else
            {
                ZeroNumLevel = 1;
                totalThreadsNum = 1;
            }

            ThreadsAvailable = totalThreadsNum; // Addition of the Master Solver does not decrements the ThreadsAvailable

            ActiveThreads = new bool[totalThreadsNum];
            SolversData = new Solver[totalThreadsNum];
            solversList = Array.AsReadOnly(SolversData);

            if (syncPeriod < 10) SyncPeriod = 10;
            else SyncPeriod = syncPeriod;
        }
        
        public int AddQueens(int[][] Additions)
        {
            tmpClmSaturation = new int[RTCLength];
            tmpRowSaturation = new int[RTCLength];

            int added = RTCLength - Levels;
            int index = added;

            int Num = Additions.Length;
            if (Num > Levels) Num = Levels;
            
            int row, clm, bInd;

            for (int i = 0; i < Num; i++)
            {
                if (Additions[i] == null) continue;

                row = Additions[i][0];
                clm = Additions[i][1];

                // check for the row and column are within board and are not occupied
                if (row >= 0 && clm >= 0 && row < RTCLength && clm < RTCLength &&
                    !occupiedRows[row] && !occupiedColumns[clm])
                    
                {
                    bNeg = clm + row;

                    // check for the negative-diagonal occupancy
                    if (!occupiedNegDiagonals[bNeg])
                    {
                        bPos = clm - row;
                        bInd = bPos + RTCEndex;

                        // check for the positive-diagonal occupancy
                        if (!occupiedPosDiagonals[bInd])
                        {
                            // Prepare data for the PreDamDetect
                            if (i > 0)
                            {
                                Buffer.BlockCopy(RowSaturationL0, 0, tmpRowSaturation, 0, Int32ArraySize);
                                Buffer.BlockCopy(ColumnSaturationL0, 0, tmpClmSaturation, 0, Int32ArraySize);
                            }
                            
                            targetY = clm;
                            occupiedRows[row] = true;

                            if (PreDamDetect())
                            {
                                occupiedRows[row] = false;
                                continue;
                            }

                            // Save the result if none of the dams were found
                            Buffer.BlockCopy(tmpRowSaturation, 0, RowSaturationL0, 0, Int32ArraySize);
                            Buffer.BlockCopy(tmpClmSaturation, 0, ColumnSaturationL0, 0, Int32ArraySize);

                            // Add the new position
                            Positions[index] = new int[4] { row, clm, bPos, bNeg };

                            // Occupy the cell (row, clm)
                            // occupiedRows[row] = true;
                            occupiedColumns[clm] = true;
                            occupiedPosDiagonals[bInd] = true;
                            occupiedNegDiagonals[bNeg] = true;

                            index++;
                        }
                    }
                }
            }

            added = index - added;
            Levels -= added;

            return added;
        }

        private void RefineData()
        {
            // --- Fill the FreeColumns and FreeRows arrays ---
            FreeColumns = new int[Levels];
            FreeRows = new int[Levels];

            int rowInd = 0;
            int clmInd = 0;

            for (int i = 0; i < RTCLength; i++)
            {
                if (!occupiedRows[i]) FreeRows[rowInd++] = i;
                if (!occupiedColumns[i]) FreeColumns[clmInd++] = i;
            }

            ArrayShuffle(ref FreeRows);

            // --- Shift levelThreadsNum ----
            if (Levels < RTCLength && TSData != null && TSData.ZeroNumberLevel > 1)
            {
                if (TSData.IsZeroLevelUsed && !TSData.ForceShift) // Method I: ZeroNumberLevel is fixed
                {
                    int relLevel0 = Levels - RTCLength + TSData.ZeroNumberLevel;

                    if (relLevel0 > 1)
                    {
                        ThreadsSpread Dat = new ThreadsSpread(TSData.SpreadLaw, TSData.TotalNumber, TSData.IniNumber, relLevel0);

                        Buffer.BlockCopy(Dat.GetArray(Levels), 0, levelThreadsNum, (RTCLength - Levels) * Int32Size, Levels * Int32Size);
                    }
                    else levelThreadsNum[RTCLength - Levels] = TSData.IniNumber;
                }
                else // Method II: ZeroNumberLevel will be shifted by number of added queens
                {
                    ZeroNumLevel += RTCLength - Levels;
                    Buffer.BlockCopy(levelThreadsNum, 0, levelThreadsNum, (RTCLength - Levels) * Int32Size, Levels * Int32Size);
                }
            }
        }

        public void Start(bool justCount = false)
        {
            if (inProgressFlag) return; // >>>>>>>>> To prevent unintentional interruption >>>>>>>>>
            else inProgressFlag = true;

            RefineData();

            if (justCount)
            {
                JustCountFlag = true;

                UniverseC = BigInteger.Zero;
                GalaxyC = 0;
            }
            else
            {
                JustCountFlag = false;

                SolutionsList.Clear();
            }
            
            ResetDDL();
            
            SyncPauseMRE.Reset();
            SyncAbortMRE.Set();

            SyncPAUSE = false;
            SyncABORT = false;

            int absLevel = RTCLength - Levels; // is equal to current index of thr Positions array; -1 if queens were not added preliminary

            Solver MasterSolver = new Solver(0, -1, absLevel, Positions, null,
                                             occupiedPosDiagonals, occupiedNegDiagonals, occupiedRows, occupiedColumns,
                                             Levels, FreeRows, FreeColumns,
                                             RowSaturationL0, ColumnSaturationL0, master: true);

            SolversData[0] = MasterSolver;
            ActiveThreads[0] = true;
            threadSeat = 1;

            MasterSolver.Run(null);
            inProgressFlag = false;
        }

        public static void Clear()
        {
            if (inProgressFlag) return; // >>>>>>>>> To prevent unintentional interruption >>>>>>>>>

            if (SolutionsList != null)
            {
                SolutionsList.Clear();
                GC.Collect();
            }
        }

        private static void ArrayShuffle(ref int[] Arr)
        {
            int Length = Arr.Length;
            
            int[] shuffledArr = new int[Length];
            Random rnd = new Random();
            
            int dynLength = Length--;
            int iRnd;

            for (int i = 0; i < Length; i++)
            {
                iRnd = rnd.Next(dynLength);
                shuffledArr[i] = Arr[iRnd];

                if (iRnd != --dynLength) Arr[iRnd] = Arr[dynLength];
            }

            shuffledArr[Length] = Arr[0];

            Arr = shuffledArr;
        } 

        /// <summary>
        /// Does not use the FreeRows array. Params.:
        /// int Length, Endex |
        /// int targetY |
        /// int bPos, bNeg |
        /// bool[] occupiedRows, occupiedColumns, occupiedPosDiagonals, occupiedNegDiagonals
        /// </summary>
        /// <returns></returns>
        private bool PreDamDetect()
        {
            int yi;

            // Before-loop
            for (int row = 0; row < RTCLength; row++)
            {
                if (occupiedRows[row]) continue;

                // Decrement vacancies of the row due to the new column-occupancy
                // only if cell (row, targetY) is not lies on occupied diagonals 
                if (!occupiedPosDiagonals[targetY - row + RTCEndex] &&
                    !occupiedNegDiagonals[targetY + row] &&
                    ++tmpRowSaturation[row] == 0) return true; // >>>>>>>>> DAM IS DETECTED! >>>>>>>>>

                // Decrement vacancies of the row and the yi column due to the new positive diagonal-occupancy
                // only if cell (row, yi) is not lies on any occupied negative diagonal or column
                yi = row + bPos;

                if (yi >= 0 && yi < RTCLength &&
                    !occupiedColumns[yi] &&
                    !occupiedNegDiagonals[yi + row] &&
                    (++tmpRowSaturation[row] == 0 | ++tmpClmSaturation[yi] == 0)) return true; // >>>>>>>>> DAM IS DETECTED! >>>>>>>>>

                // Decrement vacancies of the row and the yi column due to the new negative diagonal-occupancy
                // only if cell (row, yi) is not lies on any occupied positive diagonal or column
                yi = -row + bNeg;

                if (yi >= 0 && yi < RTCLength &&
                    !occupiedColumns[yi] &&
                    !occupiedPosDiagonals[yi - row + RTCEndex] &&
                    (++tmpRowSaturation[row] == 0 | ++tmpClmSaturation[yi] == 0)) return true; // >>>>>>>>> DAM IS DETECTED! >>>>>>>>>
            }

            return false;
        }

        public static int[][] GenerateRandom(int length)
        {
            int[][] Pos = new int[length][];
            int x, y;
            Random rnd = new Random();

            for (int i = 0; i < length; i++)
            {
                x = rnd.Next(length);
                y = rnd.Next(length);

                Pos[i] = new int[4] { x, y, y - x, y + x };
            }

            return Pos;
        }

        public static int[][] GenerateRooks(int length)
        {
            int[][] Pos = new int[length][];
            int[] Xarr = new int[length];
            int x;

            for (int i = 0; i < length; i++) Xarr[i] = i;

            ArrayShuffle(ref Xarr);

            for (int i = 0; i < length; i++)
            {
                x = Xarr[i];
                Pos[i] = new int[4] { x, i, i - x, i + x };
            }

            return Pos;
        }

        // --- IO Group -----------------------------------------------------------------------------------
        public class IOConsole
        {
            private static StringBuilder PosVisual;
            private static StringBuilder PosVisSub;
            private static StringBuilder Header;
            private static string RTCHeader;
            private static bool isRTCHsealed;

            private static bool ShowSolutions = false; // false: show intermediates
            private static bool ShowPosVisual = false; // false: don't build PosVisual
            private static bool OrderByRows = true; // true: line = row ; false: line = column

            private static int DispInd = 0;
            private static int firstLine;
            private static int lastLine;

            // --- clipboards -----------------------------
            private static int[][] Snapshot;

            private static bool[] oRows;
            private static bool[] oClms;
            private static bool[] oPosDiags;
            private static bool[] oNegDiags;

            private static bool[] EraserOrto;
            private static bool[] EraserDiag;
            // --- end of clipboards ----------------------

            public static int IOdisplayIndex
            {
                get { return DispInd; }
                set
                {
                    if (ShowSolutions)
                    {
                        if (value < 0) DispInd = 0;
                        else if (value >= Solutions.Count) DispInd = Solutions.Count - 1;
                        else DispInd = value;
                    }
                    else // show intermediates
                    {
                        if (value < 0) DispInd = 0;
                        else if (value >= totalThreadsNum) DispInd = totalThreadsNum - 1;
                        else DispInd = value;
                    }
                }
            }

            public static int IOfirstLine
            {
                get { return firstLine; }
                set
                {
                    if (value < 0) firstLine = 0;
                    else if (value > lastLine) firstLine = lastLine;
                    else firstLine = value;
                }
            }

            public static int IOlastLine
            {
                get { return lastLine; }
                set
                {
                    if (value < firstLine) lastLine = firstLine;
                    else if (value >= RTCLength) lastLine = RTCEndex;
                    else lastLine = value;
                }
            }

            // --- System IO ------------------------------
            private static string DefaultPath;
            private static string IOexceptionStr;
            private static bool IOexceptionFlag;

            public IOConsole()
            {
                PosVisual = new StringBuilder(RTCLength * (4 * 6 + 2 + RTCLength + 2));
                PosVisSub = new StringBuilder(RTCLength);
                Header = new StringBuilder(6 * 45 + 3 * 11 + 3);

                ShowSolutions = false;

                DispInd = 0;

                firstLine = 0;
                lastLine = 0;

                RTCHeader = string.Format("Length..............: {0}\n\r", RTCLength);
                RTCHeader += string.Format("Initially added.....: {0}\n\r", RTCLength - Levels);
                RTCHeader += string.Format("Total threads.......: {0}\n\r", totalThreadsNum);
                isRTCHsealed = false;

                PosVisSub.Length = RTCLength;
                for (int i = 0; i < RTCLength; i++) PosVisSub[i] = '+';

                Header.AppendFormat("Running threads.....: {0}\n\r", 0);
                Header.AppendFormat("Solutions found.....: {0}\n\r", 0);
                Header.AppendFormat("Display.............: {0}\n\r", "Interim");
                Header.AppendFormat("Displaying slot.....: {0}\n\r", 0);
                Header.AppendFormat("Displaying lines....: {0}-{1}\n\n\r", firstLine, lastLine);

                Header.AppendFormat("DDMin: {0,4}   DDMax: {1,4}   DDAvg: {2,4}\n\n\r", -1, -1, -1);
                Header.Append("  Row   Clm  bPos  bNeg");

                // --- ini clipboards ----------------------------
                Snapshot = new int[RTCLength][];
                for (int i = 0; i < RTCLength; i++) Snapshot[i] = new int[4];
                
                oRows = new bool[RTCLength];
                oClms = new bool[RTCLength];
                oPosDiags = new bool[DiagArrsLength];
                oNegDiags = new bool[DiagArrsLength];

                EraserOrto = new bool[RTCLength];
                EraserDiag = new bool[DiagArrsLength];

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
                    Console.WriteLine("You will not be able to save solutions:\n\r");
                    Console.WriteLine(IOexceptionStr);

                    Console.WriteLine("\n\n\r>>> Press any key to continue <<<");
                    Console.ReadKey();
                }
                else
                {
                    DefaultPath = Path.Combine(DefaultPath, "Solutions");

                    if (!Directory.Exists(DefaultPath))
                    {
                        Directory.CreateDirectory(DefaultPath);
                    }
                }
            }
            
            private static void PosVisualBuild()
            {
                bool[] Sequencer;
                bool[] Normals;
                int[] Pose;
                int normal, ind;
                int bPos;

                if (OrderByRows)
                {
                    Sequencer = oRows;
                    Normals = oClms;
                    ind = 1;
                }
                else // Order by Columns
                {
                    Sequencer = oClms;
                    Normals = oRows;
                    ind = 0;
                }

                if (ShowPosVisual)
                {
                    for (int i = firstLine; i <= lastLine; i++)
                    {
                        if (Sequencer[i])
                        {
                            Pose = Snapshot[i];
                            normal = Pose[ind];

                            PosVisSub[normal] = 'Q';
                            PosVisual.AppendFormat("{0,5} {1,5} {2,5} {3,5}  {4}\n\r", Pose[0], Pose[1], Pose[2], Pose[3], PosVisSub);
                            PosVisSub[normal] = '+';
                        }
                        else
                        {
                            PosVisual.Append("    .     .     .     .  ");

                            for (int yj = 0; yj < RTCLength; yj++)
                            {
                                bPos = OrderByRows ? yj - i : i - yj;

                                if (Normals[yj] || oPosDiags[bPos + RTCEndex] || oNegDiags[yj + i]) PosVisual.Append('+');
                                else PosVisual.Append('-');
                            }

                            PosVisual.AppendLine(); // \n\r for Windows
                        }
                    }
                }
                else // output just digits
                {
                    for (int i = firstLine; i <= lastLine; i++)
                    {
                        if (Sequencer[i])
                        {
                            Pose = Snapshot[i];

                            PosVisual.AppendFormat("{0,5} {1,5} {2,5} {3,5}\n\r", Pose[0], Pose[1], Pose[2], Pose[3]);
                        }
                        else PosVisual.Append("    .     .     .     .\n\r");
                    }
                }
            }

            private static string WriteAllToFile()
            {
                List<int[]> SList = new List<int[]>(Solutions.Count);

                foreach (int[][] S in Solutions)
                {
                    // If no queens were initially added 
                    // then S array is already sorted by columns p[1]
                    SList.Add(S.OrderBy(p => p[1]).Select(p => p[0]).ToArray());
                }

                string errStr = "";
                
                string name = string.Format("All-{0}-#{1}{2}", RTCLength, Solutions.Count, ".lqps");

                name = Path.Combine(DefaultPath, name);

                FileStream fStream = null;
                BinaryWriter BW = null;

                try
                {
                    fStream = new FileStream(name, FileMode.Create);

                    BW = new BinaryWriter(fStream);

                    int length = SList[0].Length;

                    BW.Write(SList.Count);
                    BW.Write(length);

                    foreach (int[] S in SList)
                    {
                        for (int i = 0; i < length; i++)
                        {
                            BW.Write(S[i]);
                        }
                    }
                }
                catch (Exception e)
                {
                    errStr = e.ToString();
                }
                finally
                {
                    if (BW != null) BW.Close();
                    if (fStream != null) fStream.Close();
                }

                return errStr;
            }

            private static string WriteToFile(int index)
            {
                string errStr = "";

                IList<int[]> PosList;

                string date = DateTime.UtcNow.ToString("yyyy-MMdd_H-mm-ss");
                string name = string.Format("{0}_{1}{2}", RTCLength, date, ".qps");

                name = Path.Combine(DefaultPath, name);

                FileStream fStream = null;

                bool binOk = true;

                try
                {
                    PosList = Solutions[index];

                    BinaryFormatter binaryFmt = new BinaryFormatter();

                    fStream = new FileStream(name, FileMode.Create);

                    binaryFmt.Serialize(fStream, PosList);
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
                        PosList = Solutions[index];

                        txtStream = File.CreateText(name);
                        
                        txtStream.WriteLine("   {0} Queens Puzzle Solution", RTCLength);
                        txtStream.WriteLine("-----------------------------------");
                        txtStream.WriteLine("    Row     Clm    bPos    bNeg");

                        foreach (int[] pos in PosList)
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

            private static void HaltedIO(object o, SyncEventArgs e)
            {
                string status, entity;
                string input = "", inputSub;
                int runNum = 0, ind;

                while (input != "X")
                {
                    PosVisual.Clear();
                    if (CopyPositions()) PosVisualBuild();

                    runNum = 0;
                    foreach (Solver slv in SolversList) if (slv != null) runNum++;
                    
                    if (e.SyncAbort) status = " <<< CANCELED >>>\n\n\r";
                    else if (e.SyncPause) status = " < < PAUSE > >\n\n\r";
                    else if (!IsMainInProgress && runNum == 1) status = " <<< COMPLETE >>>\n\n\r";
                    else status = " >>> SEEKING >>>\n\n\r";

                    entity = ShowSolutions ? "Solution" : "Interim";

                    Header.Clear();
                    Header.Append(status);
                    Header.AppendFormat("Running threads.....: {0}\n\r", runNum);
                    Header.AppendFormat("Solutions found.....: {0}\n\r", SolutionsCount.ToString("N0"));
                    Header.AppendFormat("0 Display...........: {0}\n\r", entity);
                    Header.AppendFormat("1 Displaying slot...: {0}\n\r", DispInd);
                    Header.AppendFormat("2 Displaying lines..: {0}-{1}\n\n\r", firstLine, lastLine);

                    Header.AppendFormat("DDMin: {0,4}   DDMax: {1,4}   DDAvg: {2,4}\n\n\r", e.DDMinLevel, e.DDMaxLevel, e.DDAverageLevel);

                    if (OrderByRows) Header.Append("  Row   Clm  bPos  bNeg  Rows by lines");
                    else Header.Append("  Row   Clm  bPos  bNeg  Columns by lines");

                    Console.Clear();
                    Console.WriteLine(RTCHeader);
                    Console.WriteLine(Header);
                    Console.WriteLine(PosVisual);
                    Console.WriteLine("\n\r---------");

                    if (e.SyncPause)
                    {
                        Console.WriteLine("\n\rEnter: \"index:value\" | \"save:slot\" | \"resume\" | \"cancel\"");
                        
                    }
                    else
                    {
                        Console.WriteLine("\n\rEnter: \"index:value\" | \"save:slot\" | \"pause\"");
                    }

                    Console.Write("\n\r>> ");
                    input = Console.ReadLine();
                    input.Trim();

                    ind = input.IndexOf(':');

                    if (ind < 0) inputSub = input;
                    else if (ind > 0 && ind != input.Length - 1) inputSub = input.Substring(0, ind);
                    else inputSub = "";
                    
                    switch (inputSub)
                    {
                        case "pause":
                            e.SyncPause = true;
                            input = "X";
                            break;

                        case "resume":
                            e.SyncPause = false;
                            input = "X";
                            break;

                        case "cancel":
                            if (e.SyncPause) e.SyncAbort = true;
                            input = "X";
                            break;

                        case "0":
                            if (ind > 0)
                            {
                                inputSub = input.Substring(++ind);

                                if (Solutions.Count > 0 &&
                                   (inputSub == "s" || inputSub == "S" || inputSub == "solution" || inputSub == "Solution")) ShowSolutions = true;
                                else if (inputSub == "i" || inputSub == "I" || inputSub == "interim" || inputSub == "Interim") ShowSolutions = false;

                                IOdisplayIndex = IOdisplayIndex;
                            }
                            
                            if (!e.SyncPause) input = "X";
                            break;

                        case "1":
                            if (ind > 0)
                            {
                                inputSub = input.Substring(++ind);

                                if (int.TryParse(inputSub, out ind)) IOdisplayIndex = ind;
                            }
                            
                            if (!e.SyncPause) input = "X";
                            break;

                        case "2":
                            if (ind > 0)
                            {
                                inputSub = input.Substring(++ind);
                                ind = inputSub.IndexOf('-');

                                if (ind < 0)
                                {
                                    if (int.TryParse(inputSub, out ind) && ind >= 0 && ind <= RTCEndex)
                                    {
                                        firstLine = ind;
                                        lastLine = ind;
                                    }
                                }
                                else if (ind > 0 && ind != inputSub.Length - 1)
                                {
                                    int top, bottom;

                                    if (int.TryParse(inputSub.Substring(0, ind), out top) &&
                                        int.TryParse(inputSub.Substring(++ind), out bottom) &&
                                        top <= bottom && top >= 0 && bottom <= RTCEndex)
                                    {
                                        firstLine = top;
                                        lastLine = bottom;
                                    }
                                }
                            }
                            
                            if (!e.SyncPause) input = "X";
                            break;

                        case "save":
                            if (ind > 0)
                            {
                                inputSub = input.Substring(++ind);
                                if (!int.TryParse(inputSub, out ind)) ind = -1;
                            }
                            else ind = IOdisplayIndex;

                            string err = "";

                            if (ind >= 0 && ind < Solutions.Count)
                            {
                                err = WriteToFile(ind);

                                if (err == "") Console.WriteLine("Successfully saved: {0}", ind);
                                else Console.WriteLine("Failed to save:\n\r{0}", err);

                                Console.WriteLine("\n\r >> Press any key to continue <<");
                                Console.ReadKey();
                            }
                            else if (inputSub.Contains("All") || inputSub.Contains("all"))
                            {
                                err = WriteAllToFile();

                                if (err == "") Console.WriteLine("Successfully saved: All");
                                else Console.WriteLine("Failed to save:\n\r{0}", err);

                                Console.WriteLine("\n\r >> Press any key to continue <<");
                                Console.ReadKey();
                            }

                            if (!e.SyncPause) input = "X";
                            break;

                        default:
                            if (!e.SyncPause) input = "X";
                            break;
                    }
                }
            }

            private static bool InputSwitch(ref bool pauseFlag)
            {
                bool EnterFlag = pauseFlag ? true : false;

                if (Console.KeyAvailable)
                {
                    ConsoleKeyInfo kInfo = Console.ReadKey(true);

                    switch (kInfo.Key)
                    {
                        case ConsoleKey.Enter:
                            EnterFlag = true;
                            break;

                        case ConsoleKey.Escape:
                        case ConsoleKey.P:
                            pauseFlag = true;
                            break;

                        case ConsoleKey.I:
                            ShowSolutions = false;
                            IOdisplayIndex = IOdisplayIndex;
                            break;

                        case ConsoleKey.S:
                            if (Solutions.Count > 0)
                            {
                                ShowSolutions = true;
                                IOdisplayIndex = IOdisplayIndex;
                            }
                            
                            break;

                        case ConsoleKey.V:
                            ShowPosVisual = !ShowPosVisual;
                            break;

                        case ConsoleKey.Tab:
                            OrderByRows = !OrderByRows;
                            break;

                        case ConsoleKey.LeftArrow:
                            IOdisplayIndex--;
                            break;

                        case ConsoleKey.RightArrow:
                            IOdisplayIndex++;
                            break;

                        case ConsoleKey.UpArrow:
                            if ((kInfo.Modifiers & ConsoleModifiers.Shift) == 0) // Scroll Up
                            {
                                if (IOfirstLine != 0)
                                {
                                    IOfirstLine--;
                                    IOlastLine--;
                                }
                            }
                            else if ((kInfo.Modifiers & ConsoleModifiers.Control) == 0) IOfirstLine--; // + Shift - add upper line
                            else IOfirstLine++; // + Ctrl + Shift - remove upper line

                            break;

                        case ConsoleKey.DownArrow:
                            if ((kInfo.Modifiers & ConsoleModifiers.Shift) == 0) // Scroll Down
                            {
                                if (IOlastLine != RTCEndex)
                                {
                                    IOlastLine++;
                                    IOfirstLine++;
                                }
                            }
                            else if ((kInfo.Modifiers & ConsoleModifiers.Control) == 0) IOlastLine++; // + Shift - add lower line
                            else IOlastLine--; // + Ctrl + Shift - remove lower line

                            break;

                        case ConsoleKey.Home:
                            int d = IOlastLine - IOfirstLine;
                            IOfirstLine = 0;
                            IOlastLine = d;
                            break;

                        case ConsoleKey.End:
                            int n = IOlastLine - IOfirstLine;
                            IOlastLine = RTCEndex;
                            IOfirstLine = RTCEndex - n;
                            break;

                        case ConsoleKey.PageUp:
                            if ((kInfo.Modifiers & ConsoleModifiers.Shift) == 0) IOfirstLine = 0;
                            else IOfirstLine = IOlastLine;
                            break;

                        case ConsoleKey.PageDown:
                            if ((kInfo.Modifiers & ConsoleModifiers.Shift) == 0) IOlastLine = RTCEndex;
                            else IOlastLine = IOfirstLine;
                            break;
                    }
                }

                return EnterFlag;
            }

            private static bool CopyPositions()
            {
                IList<IList<int>> posList;

                if (ShowSolutions) posList = Solutions[DispInd];
                else posList = SolversData[DispInd]?.Interims;

                if (posList != null)
                {
                    // erase oArrays
                    Buffer.BlockCopy(EraserOrto, 0, oRows, 0, BoolArraySize);
                    Buffer.BlockCopy(EraserOrto, 0, oClms, 0, BoolArraySize);

                    Buffer.BlockCopy(EraserDiag, 0, oPosDiags, 0, BoolDiagArrsSize);
                    Buffer.BlockCopy(EraserDiag, 0, oNegDiags, 0, BoolDiagArrsSize);

                    IList<int> Pos = posList[0];
                    int index = 0;
                    int row, clm;

                    while (Pos != null)
                    {
                        row = Pos[0];
                        clm = Pos[1];

                        if (OrderByRows) Pos.CopyTo(Snapshot[row], 0);
                        else Pos.CopyTo(Snapshot[clm], 0);

                        oRows[row] = true;
                        oClms[clm] = true;
                        oPosDiags[clm - row + RTCEndex] = true;
                        oNegDiags[clm + row] = true;

                        if (++index == RTCLength) break;

                        Pos = posList[index];
                    }

                    return true;
                }
                else return false;
            }
            
            public void SyncHandler(object o, SyncEventArgs e)
            {
                if (InputSwitch(ref e.SyncPause))
                {
                    HaltedIO(o, e);

                    return; // >>>>>>>>> >>>>>>>>>
                } 

                PosVisual.Clear();
                if (CopyPositions()) PosVisualBuild();

                int runNum = 0;
                foreach (Solver slv in SolversList) if (slv != null) runNum++;

                if (!isRTCHsealed)
                {
                    RTCHeader += string.Format("Just count..........: {0}\n\r", IsJustCount);
                    isRTCHsealed = true;
                }

                string status;
                
                if (e.SyncAbort) status = " <<< CANCELED >>>\n\n\r";
                else if (e.SyncPause) status = " < < PAUSE > >\n\n\r";
                else if (!IsMainInProgress && runNum == 1) status = " <<< COMPLETE >>>\n\n\r";
                else status = " >>> SEEKING >>>\n\n\r";

                string entity = ShowSolutions ? "Solution" : "Interim";

                Header.Clear();
                Header.Append(status);
                Header.AppendFormat("Running threads.....: {0}\n\r", runNum);
                Header.AppendFormat("Solutions found.....: {0}\n\r", SolutionsCount.ToString("N0"));
                Header.AppendFormat("Display.............: {0}\n\r", entity);
                Header.AppendFormat("Displaying slot.....: {0}\n\r", DispInd);
                Header.AppendFormat("Displaying lines....: {0}-{1}\n\n\r", firstLine, lastLine);

                Header.AppendFormat("DDMin: {0,4}   DDMax: {1,4}   DDAvg: {2,4}\n\n\r", e.DDMinLevel, e.DDMaxLevel, e.DDAverageLevel);

                if (OrderByRows) Header.Append("  Row   Clm  bPos  bNeg  Rows by lines");
                else Header.Append("  Row   Clm  bPos  bNeg  Columns by lines");

                Console.Clear();
                Console.WriteLine(RTCHeader);
                Console.WriteLine(Header);
                Console.WriteLine(PosVisual);
            }
        }// end of public class IOConsole ////////////////////////////////////////////////////////////////////////////////////////
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace QueensPuzzle
{
    public class QPSDamDetect
    {
        // x and row are used for rows
        // y and clm are used for columns

        private int Length = 8; // Sets the field size
        private int Levels = 8; // Initially is equal to the Length and is decremented with each new queen adding
        private int Endex = 7; // Equal to Length - 1

        private int[] FreeColumns;
        private int[] FreeRows;

        private bool[] occupiedPosDiagonals; // occupiedPosDiagonals[i] corresponds to bpos = i - Endex;
        private bool[] occupiedNegDiagonals; // occupiedNegDiagonals[i] corresponds to bneg = i;
        private bool[] occupiedColumns;
        private bool[] occupiedRows;

        private int[][] ColumnsVacancies;
        private int[][] RowsVacancies;

        private int FreeRowsEndex;
        private int[] SelectedRows; // is used to restore the FreeRows upon a return to a higher level

        private int[][] Positions;
        private List<int[][]> SolutionsList;
        
        private int totalThreadsNum;
        private double levelThreadRatio;
        private int[] levelThreadsNum;

        private int Int32Size = sizeof(int);
        private int Int32ArraySize;

        /// <summary>
        /// Is called from the MainSolve()
        /// </summary>
        private void InitializeData()
        {
            FreeColumns = new int[Length];
            FreeRows = new int[Length];

            ColumnsVacancies = new int[Endex][];
            RowsVacancies = new int[Endex][];

            int[] tmpVacancies = new int[Length];
            for (int i = 0; i < Length; i++) tmpVacancies[i] = Length;
                    
            for (int i = 0; i < Endex; i++)
            {
                FreeColumns[i] = i;
                FreeRows[i] = i;

                ColumnsVacancies[i] = new int[Length];
                Buffer.BlockCopy(tmpVacancies, 0, ColumnsVacancies[i], 0, Int32ArraySize);

                RowsVacancies[i] = new int[Length];
                Buffer.BlockCopy(tmpVacancies, 0, RowsVacancies[i], 0, Int32ArraySize);
            }
            FreeColumns[Endex] = Endex;
            FreeRows[Endex] = Endex;
            
            FreeRowsEndex = Length - 1;
            SelectedRows = new int[FreeRowsEndex];

            Positions = new int[Length][];

            occupiedPosDiagonals = new bool[2 * Endex];
            occupiedNegDiagonals = new bool[2 * Endex];
            occupiedColumns = new bool[Length];
            occupiedRows = new bool[Length];

            levelThreadsNum = new int[Length];

            Int32ArraySize = Length * Int32Size;
        }

        private void SetThreads(int threadsNum = 1, int[] threadsByLevel = null, double levelRatio = 2)
        {
            int w, io;
            ThreadPool.GetMaxThreads(out w, out io);

            if (threadsNum < 1) totalThreadsNum = 1;
            else if (threadsNum > w) totalThreadsNum = w;
            else totalThreadsNum = threadsNum;

            if (threadsByLevel == null) // use levelRatio to fill the levelThreadsNum array
            {
                if (levelRatio < 1)
                {
                    levelThreadRatio = 1;

                    for (int i = 0; i < Levels; i++) levelThreadsNum[i] = totalThreadsNum;
                }
                else
                {
                    levelThreadRatio = levelRatio;

                    levelThreadsNum[0] = totalThreadsNum;

                    for (int i = 1; i < Levels; i++) levelThreadsNum[i] = (int)(totalThreadsNum / (i * levelThreadRatio));
                }
            }
            else
            {
                Buffer.BlockCopy(threadsByLevel, 0, levelThreadsNum, 0, Levels * Int32Size);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="length"></param>
        /// <param name="threadsNum"></param>
        /// <param name="threadsByLevel"></param>
        /// <param name="levelRatio"></param>
        public QPSDamDetect(int length = 8, int threadsNum = 1, int[] threadsByLevel = null, double levelRatio = 2)
        {
            if (length < 1) length = 1;

            Length = length;
            Levels = length;
            Endex = length - 1;

            SetThreads(threadsNum, threadsByLevel, levelRatio);
        }

        /// <summary>
        /// Is called from the MainSolve()
        /// </summary>
        /// <param name="clm"></param>
        /// <param name="bpos"></param>
        /// <param name="bneg"></param>
        /// <param name="XarrLength"></param>
        /// <param name="selectedInd"></param>
        /// <param name="tmpArr"></param>
        /// <param name="ArrSize"></param>
        /// <returns></returns>
        private bool DamDetect(int clm, int bpos, int bneg, int FreeRowsLength, int selectedInd, int[] tmpArr)
        {
            int row;
            int yi;

            Buffer.BlockCopy(RowsVacancies, 0, tmpArr, 0, Int32ArraySize);
            
            // Before-loop
            for (int i = 0; i < selectedInd; i++)
            {
                row = FreeRows[i];

                // Decrement vacancies of the row due to the new column-occupancy
                // only if cell (row, clm) is not lies on occupied diagonals 
                if (!occupiedPosDiagonals[clm - row + Endex] &&
                    !occupiedNegDiagonals[clm + row] &&
                    (--tmpArr[row] == 0)) return true; // >>>>>>>>> DAM IS DETECTED! >>>>>>>>>

                // Decrement vacancies of the row due to the new positive diagonal-occupancy
                // only if cell (row, yi) is not lies on any occupied negative diagonal or column
                yi = row + bpos;

                if (yi >= 0 && yi < Length && 
                    !occupiedColumns[yi] && 
                    !occupiedNegDiagonals[yi + row] &&
                    --tmpArr[row] == 0) return true; // >>>>>>>>> DAM IS DETECTED! >>>>>>>>>

                // Decrement vacancies of the row due to the new negative diagonal-occupancy
                // only if cell (row, yi) is not lies on any occupied negative diagonal
                yi = -row + bneg;
                
                if (yi >= 0 && yi < Length && 
                    !occupiedColumns[yi] &&
                    !occupiedPosDiagonals[yi - row + Endex] &&
                    --tmpArr[row] == 0) return true; // >>>>>>>>> DAM IS DETECTED! >>>>>>>>>
            }

            // After-loop
            for (int i = selectedInd + 1; i < FreeRowsLength; i++)
            {
                row = FreeRows[i];

                // Decrement vacancies of the row due to the new column-occupancy
                // only if cell (row, clm) is not lies on occupied diagonals 
                if (!occupiedPosDiagonals[clm - row + Endex] &&
                    !occupiedNegDiagonals[clm + row] &&
                    --tmpArr[row] == 0) return true; // >>>>>>>>> DAM IS DETECTED! >>>>>>>>>

                // Decrement vacancies of the row due to the new positive diagonal-occupancy
                // only if cell (row, yi) is not lies on any occupied negative diagonal or column
                yi = row + bpos;

                if (yi >= 0 && yi < Length &&
                    !occupiedColumns[yi] &&
                    !occupiedNegDiagonals[yi + row] &&
                    --tmpArr[row] == 0) return true; // >>>>>>>>> DAM IS DETECTED! >>>>>>>>>

                // Decrement vacancies of the row due to the new negative diagonal-occupancy
                // only if cell (row, yi) is not lies on any occupied negative diagonal
                yi = -row + bneg;

                if (yi >= 0 && yi < Length &&
                    !occupiedColumns[yi] &&
                    !occupiedPosDiagonals[yi - row + Endex] &&
                    --tmpArr[row] == 0) return true; // >>>>>>>>> DAM IS DETECTED! >>>>>>>>>
            }

            // No dams were detected
            // Occupy the cell
            row = FreeRows[selectedInd];

            occupiedColumns[clm] = true;
            occupiedRows[row] = true;
            occupiedPosDiagonals[clm - row + Endex] = true;
            occupiedNegDiagonals[clm + row] = true;

            Buffer.BlockCopy(tmpArr, 0, RowsVacancies, 0, Int32ArraySize); // ????? How I will restore RowsVacancies upon a return to a higher level

            return false;
        }

        public void MainSolve()
        {
            InitializeData(length);

            int[] tmpColumnsVacancies = new int[Length];
            int[] tmpRowsVacancies = new int[Length];

            int freeYnum = Levels; // FreeColumns.Length;
            int freeXnum = Levels; // FreeRows.Length;
            int levelCounter = Levels;
            int targetY;
            int trialX;
            int bPos, bNeg;

            for (int yi = 0; yi < freeYnum; yi++)
            {
                targetY = FreeColumns[yi];

                for (int xj = 0; xj < freeXnum; xj++)
                {
                    trialX = FreeRows[xj];

                    bPos = targetY - trialX;
                    bNeg = targetY + trialX;

                    if (!occupiedPosDiagonals[bPos + Endex] && !occupiedNegDiagonals[bNeg])
                    {
                        if (levelCounter == 1) // New Solution is found!
                        {
                            // Save the Solution
                        }
                        else // levelsCounter > 1
                        {
                            DamDetect();
                        }
                    }
                }
            }
        }
    }
}

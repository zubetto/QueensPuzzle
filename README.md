<p align="left">
  <img src="https://raw.githubusercontent.com/zubetto/QueensPuzzle/master/Solutions_pano01header.jpg"/>
</p>

# n-Queens [Completion] problem solver
The class _QPSDamDetect_ contains n-Queens [Completion] problem solver implemented as multithreaded randomized non-recursive backtracking algorithm with dam-pruning
  
The randomization stands for shuffling of the array of free rows before start of the solving and helps to avoid of "dead" patterns.
  
The dam stands for a column or row which are not occupied by any queen yet, 
but at the same time queen placed on any cell of that column or row will be under attack. With sequentially filling of at least one coordinate (columns in this case) the dam detection allows eliminate a searching branch in advance (in the pic. below, the next positions (38,21); (38,31); (38,35) are allowed but position (37,40) is rejected as it causes formation of the dam)  

<p align="left">
  <img src="https://raw.githubusercontent.com/zubetto/QueensPuzzle/master/QPS_DamDetect_L50.jpg" width="595" height="867"/>
</p>

## Solutions Distribution
Some interesting results were obtained, playing with this solver. The following plots represent distribution of the solutions number depending on the arrangement of a subset of queens. This distribution is built by iterating the possible permutations of such a subset and counting the number of all solutions containing the current permutation (i.e. by solving n-Queens Completion problem for each permutation).
In this particular case, subset consists of three queens that occupy the first three adjacent columns and only the permutations without overlaps (no one attacks each other) are enumerated. The subset length affects the resolution of the plot but not the general nature of the distribution.

<p align="left">
  <img src="https://raw.githubusercontent.com/zubetto/QueensPuzzle/master/N-Queens%20SolSpread%20Dom3_overlap-free.jpg" width="1200" height="2633"/>
</p>

[plot.ly data  8-12](https://plot.ly/create/?fid=Zubetto%3A5)  
[plot.ly data 13-17](https://plot.ly/create/?fid=Zubetto%3A7)
